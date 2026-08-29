using System;
using System.Collections.Generic;
using System.IO;
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
    public sealed class OrderSyncServiceTests : IDisposable
    {
        private readonly string _databasePath = Path.Combine(
            Path.GetTempPath(),
            "omg-sync-test-" + Guid.NewGuid().ToString("N") + ".db");

        [Fact]
        public async Task DoesNotResubmitUnchangedOrderDuringOverlapWindow()
        {
            var settings = TestSettings.Create(_databasePath);
            var order = CreateOrder("new");
            var omg = new FakeOmgClient(order);
            var cloud9 = new FakeCloud9Client();
            using (var repository = new IntegrationRepository(_databasePath))
            {
                var service = new OrderSyncService(
                    settings,
                    omg,
                    cloud9,
                    repository,
                    new OrderMapper(settings),
                    new NullLog());

                await service.RunAsync(CancellationToken.None);
                await service.RunAsync(CancellationToken.None);

                Assert.Equal(1, cloud9.SubmissionCount);
            }
        }

        [Fact]
        public async Task DoesNotSubmitOrderWhoseStatusIsNotConfigured()
        {
            var settings = TestSettings.Create(_databasePath);
            var order = CreateOrder("canceled");
            var cloud9 = new FakeCloud9Client();
            using (var repository = new IntegrationRepository(_databasePath))
            {
                var service = new OrderSyncService(
                    settings,
                    new FakeOmgClient(order),
                    cloud9,
                    repository,
                    new OrderMapper(settings),
                    new NullLog());

                await service.RunAsync(CancellationToken.None);

                Assert.Equal(0, cloud9.SubmissionCount);
                Assert.Equal("skipped", repository.FindOrderByCloud9Number("OMG-12345").Status);
            }
        }

        public void Dispose()
        {
            DeleteIfPresent(_databasePath);
            DeleteIfPresent(_databasePath + "-log");
        }

        private static OmgOrder CreateOrder(string status)
        {
            return OmgOrder.FromJson(JObject.Parse(@"{
                'order_id': 12345,
                'status': '" + status + @"',
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

        private static void DeleteIfPresent(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        private sealed class FakeOmgClient : IOmgClient
        {
            private readonly OmgOrder _order;

            public FakeOmgClient(OmgOrder order)
            {
                _order = order;
            }

            public Task<IReadOnlyList<JObject>> GetUpdatedOrdersAsync(
                DateTimeOffset from,
                DateTimeOffset to,
                int page,
                int pageSize,
                CancellationToken cancellationToken)
            {
                IReadOnlyList<JObject> orders = page == 1
                    ? new List<JObject> { JObject.Parse("{ 'order_id': 12345 }") }
                    : new List<JObject>();
                return Task.FromResult(orders);
            }

            public Task<OmgOrder> GetOrderAsync(string orderId, CancellationToken cancellationToken)
            {
                return Task.FromResult(_order);
            }

            public Task<IReadOnlyList<JObject>> GetShipmentsAsync(string orderId, CancellationToken cancellationToken)
            {
                throw new NotSupportedException();
            }

            public Task<JObject> CreateShipmentAsync(
                string orderId,
                OmgShipmentRequest shipment,
                CancellationToken cancellationToken)
            {
                throw new NotSupportedException();
            }
        }

        private sealed class FakeCloud9Client : ICloud9Client
        {
            public int SubmissionCount { get; private set; }

            public Task AddShipJobAsync(Cloud9ShipJob job, CancellationToken cancellationToken)
            {
                SubmissionCount++;
                return Task.CompletedTask;
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
    }
}
