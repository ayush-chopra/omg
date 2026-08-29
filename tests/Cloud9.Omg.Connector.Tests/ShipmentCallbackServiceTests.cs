using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cloud9.Omg.Connector.Clients;
using Cloud9.Omg.Connector.Models;
using Cloud9.Omg.Connector.Persistence;
using Cloud9.Omg.Connector.Services;
using Cloud9.Omg.Connector.Support;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Cloud9.Omg.Connector.Tests
{
    public sealed class ShipmentCallbackServiceTests
    {
        [Fact]
        public async Task WritesSingleTrackingNumberToOmgAndKeepsCostInternally()
        {
            var settings = TestSettings.Create();
            var repository = new InMemoryRepository();
            var order = CreateOrder();
            repository.UpsertOrder(order, "OMG-12345");
            repository.MarkOrderSubmitted(order.Id);
            var omg = new FakeOmgClient();
            var service = new ShipmentCallbackService(settings, omg, repository, new NullLog());

            var result = await service.ProcessAsync(new Cloud9ShipJobCallback
            {
                OrderNumber = "OMG-12345",
                ShipDate = "8/30/2026",
                CarrierScac = "UPS",
                ServiceType = "UPS Ground",
                Cost = "53.58",
                IsReturn = string.Empty,
                VoidDate = string.Empty,
                Packages = new List<Cloud9CallbackPackage>
                {
                    new Cloud9CallbackPackage { TrackingNumber = "1Z999" }
                }
            }, CancellationToken.None);

            Assert.Equal("completed", result.Status);
            Assert.NotNull(omg.CreatedShipment);
            Assert.Equal("1Z999", omg.CreatedShipment.TrackingNumber);
            Assert.Equal(77L, omg.CreatedShipment.LineItems[0].Id);
            Assert.DoesNotContain("53.58", omg.CreatedShipment.Note);
            Assert.Equal(5358L, repository.ReservedCostCents);
        }

        [Fact]
        public async Task SendsMultiPackageCallbackToManualReview()
        {
            var settings = TestSettings.Create();
            var repository = new InMemoryRepository();
            var order = CreateOrder();
            repository.UpsertOrder(order, "OMG-12345");
            var omg = new FakeOmgClient();
            var service = new ShipmentCallbackService(settings, omg, repository, new NullLog());

            var result = await service.ProcessAsync(new Cloud9ShipJobCallback
            {
                OrderNumber = "OMG-12345",
                ShipDate = "8/30/2026",
                Cost = "10.00",
                Packages = new List<Cloud9CallbackPackage>
                {
                    new Cloud9CallbackPackage { TrackingNumber = "TRACK-1" },
                    new Cloud9CallbackPackage { TrackingNumber = "TRACK-2" }
                }
            }, CancellationToken.None);

            Assert.Equal("manual_review", result.Status);
            Assert.Null(omg.CreatedShipment);
        }

        [Fact]
        public async Task WritesOnlyQuantitiesRemainingAfterPriorOmgShipments()
        {
            var settings = TestSettings.Create();
            var repository = new InMemoryRepository();
            var order = CreateOrder();
            repository.UpsertOrder(order, "OMG-12345");
            var omg = new FakeOmgClient();
            omg.ExistingShipments.Add(JObject.Parse(@"{
                'id': 88,
                'tracking_number': 'OLD-TRACKING',
                'line_items': [{ 'id': 77, 'quantity': 1 }]
            }"));
            var service = new ShipmentCallbackService(settings, omg, repository, new NullLog());

            var result = await service.ProcessAsync(new Cloud9ShipJobCallback
            {
                OrderNumber = "OMG-12345",
                ShipDate = "8/30/2026",
                CarrierScac = "UPS",
                Cost = "12.00",
                Packages = new List<Cloud9CallbackPackage>
                {
                    new Cloud9CallbackPackage { TrackingNumber = "NEW-TRACKING" }
                }
            }, CancellationToken.None);

            Assert.Equal("completed", result.Status);
            Assert.Equal(1, omg.CreatedShipment.LineItems[0].Quantity);
        }

        private static OmgOrder CreateOrder()
        {
            return OmgOrder.FromJson(JObject.Parse(@"{
                'order_id': 12345,
                'status': 'new',
                'updated_at': '2026-08-30T10:00:00Z',
                'grand_total': '85.50',
                'shipping_contact': {
                    'first_name': 'Jane', 'last_name': 'Doe',
                    'email': 'jane@example.test', 'phone': '555-0100'
                },
                'shipping_address': {
                    'first_address': '100 Main St', 'city': 'Austin',
                    'state': 'TX', 'country': 'US', 'zip': '78701'
                },
                'line_items': [{ 'id': 77, 'quantity': 2 }]
            }"));
        }

        private sealed class FakeOmgClient : IOmgClient
        {
            public OmgShipmentRequest CreatedShipment { get; private set; }
            public List<JObject> ExistingShipments { get; } = new List<JObject>();

            public Task<IReadOnlyList<JObject>> GetUpdatedOrdersAsync(
                DateTimeOffset from, DateTimeOffset to, int page, int pageSize, CancellationToken cancellationToken)
            {
                throw new NotSupportedException();
            }

            public Task<OmgOrder> GetOrderAsync(string orderId, CancellationToken cancellationToken)
            {
                throw new NotSupportedException();
            }

            public Task<IReadOnlyList<JObject>> GetShipmentsAsync(string orderId, CancellationToken cancellationToken)
            {
                return Task.FromResult<IReadOnlyList<JObject>>(ExistingShipments);
            }

            public Task<JObject> CreateShipmentAsync(
                string orderId, OmgShipmentRequest shipment, CancellationToken cancellationToken)
            {
                CreatedShipment = shipment;
                return Task.FromResult(JObject.Parse("{ 'id': 9001 }"));
            }
        }

        private sealed class NullLog : IConnectorLog
        {
            public void Info(string message)
            {
            }

            public void Error(string message, Exception exception)
            {
            }
        }

        private sealed class InMemoryRepository : IIntegrationRepository
        {
            private OrderRecord _order;
            private readonly Dictionary<string, string> _eventStatuses = new Dictionary<string, string>();

            public long ReservedCostCents { get; private set; }

            public DateTimeOffset? GetCursor(string key)
            {
                return null;
            }

            public void SetCursor(string key, DateTimeOffset value)
            {
            }

            public bool UpsertOrder(OmgOrder order, string cloud9OrderNumber)
            {
                _order = new OrderRecord
                {
                    OmgOrderId = order.Id,
                    Cloud9OrderNumber = cloud9OrderNumber,
                    OmgUpdatedAt = order.UpdatedAt.ToString("o"),
                    RawJson = order.RawJson,
                    Status = "pending"
                };
                return true;
            }

            public IReadOnlyList<OrderRecord> GetRetryableOrders(int limit)
            {
                return new List<OrderRecord>();
            }

            public OrderRecord FindOrderByCloud9Number(string cloud9OrderNumber)
            {
                return _order != null && _order.Cloud9OrderNumber == cloud9OrderNumber ? _order : null;
            }

            public void MarkOrderSubmitted(string omgOrderId)
            {
                if (_order != null)
                {
                    _order.Status = "submitted";
                }
            }

            public void MarkOrderSkipped(string omgOrderId, string reason)
            {
                if (_order != null)
                {
                    _order.Status = "skipped";
                }
            }

            public void MarkOrderFailed(string omgOrderId, string error)
            {
            }

            public EventReservation ReserveShipmentEvent(
                string eventKey,
                string omgOrderId,
                string trackingNumber,
                long costCents,
                string payloadJson)
            {
                string status;
                if (_eventStatuses.TryGetValue(eventKey, out status))
                {
                    return new EventReservation { IsNew = false, ExistingStatus = status };
                }

                ReservedCostCents = costCents;
                _eventStatuses[eventKey] = "processing";
                return new EventReservation { IsNew = true };
            }

            public void MarkShipmentCompleted(string eventKey, string omgShipmentId)
            {
                _eventStatuses[eventKey] = "completed";
            }

            public void MarkShipmentManualReview(string eventKey, string reason)
            {
                _eventStatuses[eventKey] = "manual_review";
            }

            public void MarkShipmentFailed(string eventKey, string error)
            {
                _eventStatuses[eventKey] = "failed";
            }
        }
    }
}
