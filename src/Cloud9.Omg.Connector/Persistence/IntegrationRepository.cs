using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Cloud9.Omg.Connector.Models;
using LiteDB;

namespace Cloud9.Omg.Connector.Persistence
{
    public sealed class IntegrationRepository : IIntegrationRepository, IDisposable
    {
        private static readonly TimeSpan ProcessingLease = TimeSpan.FromMinutes(5);
        private readonly object _gate = new object();
        private readonly LiteDatabase _database;

        public IntegrationRepository(string databasePath)
        {
            if (string.IsNullOrWhiteSpace(databasePath))
            {
                throw new ArgumentException("Database path is required.", nameof(databasePath));
            }

            var directory = Path.GetDirectoryName(databasePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            _database = new LiteDatabase(new ConnectionString
            {
                Filename = databasePath,
                Connection = ConnectionType.Shared,
                Upgrade = true
            });

        }

        private ILiteCollection<SyncStateDocument> SyncState
        {
            get { return _database.GetCollection<SyncStateDocument>("sync_state"); }
        }

        private ILiteCollection<OrderDocument> Orders
        {
            get { return _database.GetCollection<OrderDocument>("orders"); }
        }

        private ILiteCollection<ShipmentEventDocument> ShipmentEvents
        {
            get { return _database.GetCollection<ShipmentEventDocument>("shipment_events"); }
        }

        public DateTimeOffset? GetCursor(string key)
        {
            lock (_gate)
            {
                var state = SyncState.FindById(key);
                DateTimeOffset value;
                return state != null && DateTimeOffset.TryParse(
                           state.Value,
                           CultureInfo.InvariantCulture,
                           DateTimeStyles.AssumeUniversal,
                           out value)
                    ? value.ToUniversalTime()
                    : (DateTimeOffset?)null;
            }
        }

        public void SetCursor(string key, DateTimeOffset value)
        {
            lock (_gate)
            {
                SyncState.Upsert(new SyncStateDocument
                {
                    Id = key,
                    Value = value.UtcDateTime.ToString("o", CultureInfo.InvariantCulture),
                    UpdatedAt = UtcNow()
                });
            }
        }

        public bool UpsertOrder(OmgOrder order, string cloud9OrderNumber)
        {
            if (order == null)
            {
                throw new ArgumentNullException(nameof(order));
            }

            lock (_gate)
            {
                var existing = Orders.FindById(order.Id);
                var updatedAt = order.UpdatedAt.UtcDateTime.ToString("o", CultureInfo.InvariantCulture);
                var changed = existing == null ||
                              !string.Equals(existing.OmgUpdatedAt, updatedAt, StringComparison.Ordinal);
                var retryable = existing != null &&
                                (string.Equals(existing.Status, "failed", StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(existing.Status, "pending", StringComparison.OrdinalIgnoreCase));

                if (existing == null)
                {
                    existing = new OrderDocument
                    {
                        OmgOrderId = order.Id,
                        CreatedAt = UtcNow(),
                        AttemptCount = 0,
                        Status = "pending"
                    };
                }

                existing.Cloud9OrderNumber = cloud9OrderNumber;
                existing.OmgUpdatedAt = updatedAt;
                existing.RawJson = order.RawJson;
                existing.UpdatedAt = UtcNow();
                if (changed)
                {
                    existing.Status = "pending";
                    existing.AttemptCount = 0;
                    existing.LastError = null;
                }

                Orders.Upsert(existing);
                return changed || retryable;
            }
        }

        public IReadOnlyList<OrderRecord> GetRetryableOrders(int limit)
        {
            lock (_gate)
            {
                return Orders.FindAll()
                    .Where(order =>
                        (string.Equals(order.Status, "pending", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(order.Status, "failed", StringComparison.OrdinalIgnoreCase)) &&
                        order.AttemptCount < 10)
                    .OrderBy(order => order.UpdatedAt, StringComparer.Ordinal)
                    .Take(limit)
                    .Select(ToOrderRecord)
                    .ToList();
            }
        }

        public OrderRecord FindOrderByCloud9Number(string cloud9OrderNumber)
        {
            lock (_gate)
            {
                var order = Orders.FindOne(item => item.Cloud9OrderNumber == cloud9OrderNumber);
                return order == null ? null : ToOrderRecord(order);
            }
        }

        public void MarkOrderSubmitted(string omgOrderId)
        {
            UpdateOrderStatus(omgOrderId, "submitted", null, true);
        }

        public void MarkOrderSkipped(string omgOrderId, string reason)
        {
            UpdateOrderStatus(omgOrderId, "skipped", Truncate(reason, 2000), false);
        }

        public void MarkOrderFailed(string omgOrderId, string error)
        {
            UpdateOrderStatus(omgOrderId, "failed", Truncate(error, 2000), false);
        }

        public EventReservation ReserveShipmentEvent(
            string eventKey,
            string omgOrderId,
            string trackingNumber,
            long costCents,
            string payloadJson)
        {
            lock (_gate)
            {
                var existing = ShipmentEvents.FindById(eventKey);
                if (existing != null)
                {
                    DateTimeOffset lastUpdated;
                    var staleProcessing = string.Equals(existing.Status, "processing", StringComparison.OrdinalIgnoreCase) &&
                                          DateTimeOffset.TryParse(
                                              existing.UpdatedAt,
                                              CultureInfo.InvariantCulture,
                                              DateTimeStyles.AssumeUniversal,
                                              out lastUpdated) &&
                                          lastUpdated < DateTimeOffset.UtcNow.Subtract(ProcessingLease);
                    var failed = string.Equals(existing.Status, "failed", StringComparison.OrdinalIgnoreCase);
                    if (!failed && !staleProcessing)
                    {
                        return new EventReservation { IsNew = false, ExistingStatus = existing.Status };
                    }

                    var priorStatus = existing.Status;
                    existing.Status = "processing";
                    existing.AttemptCount++;
                    existing.LastError = null;
                    existing.UpdatedAt = UtcNow();
                    existing.CostCents = costCents;
                    existing.PayloadJson = payloadJson;
                    ShipmentEvents.Update(existing);
                    return new EventReservation { IsNew = true, ExistingStatus = priorStatus };
                }

                ShipmentEvents.Insert(new ShipmentEventDocument
                {
                    EventKey = eventKey,
                    OmgOrderId = omgOrderId,
                    TrackingNumber = trackingNumber,
                    CostCents = costCents,
                    PayloadJson = payloadJson,
                    Status = "processing",
                    AttemptCount = 1,
                    CreatedAt = UtcNow(),
                    UpdatedAt = UtcNow()
                });
                return new EventReservation { IsNew = true };
            }
        }

        public void MarkShipmentCompleted(string eventKey, string omgShipmentId)
        {
            UpdateShipmentEvent(eventKey, "completed", omgShipmentId, null);
        }

        public void MarkShipmentManualReview(string eventKey, string reason)
        {
            UpdateShipmentEvent(eventKey, "manual_review", null, reason);
        }

        public void MarkShipmentFailed(string eventKey, string error)
        {
            UpdateShipmentEvent(eventKey, "failed", null, error);
        }

        public void Dispose()
        {
            _database.Dispose();
        }

        private void UpdateOrderStatus(string omgOrderId, string status, string error, bool submitted)
        {
            lock (_gate)
            {
                var order = Orders.FindById(omgOrderId);
                if (order == null)
                {
                    return;
                }

                order.Status = status;
                order.AttemptCount++;
                order.LastError = error;
                order.UpdatedAt = UtcNow();
                if (submitted)
                {
                    order.Cloud9SubmittedAt = UtcNow();
                }

                Orders.Update(order);
            }
        }

        private void UpdateShipmentEvent(string eventKey, string status, string omgShipmentId, string error)
        {
            lock (_gate)
            {
                var shipment = ShipmentEvents.FindById(eventKey);
                if (shipment == null)
                {
                    return;
                }

                shipment.Status = status;
                if (!string.IsNullOrWhiteSpace(omgShipmentId))
                {
                    shipment.OmgShipmentId = omgShipmentId;
                }

                shipment.LastError = Truncate(error, 2000);
                shipment.UpdatedAt = UtcNow();
                ShipmentEvents.Update(shipment);
            }
        }

        private static OrderRecord ToOrderRecord(OrderDocument order)
        {
            return new OrderRecord
            {
                OmgOrderId = order.OmgOrderId,
                Cloud9OrderNumber = order.Cloud9OrderNumber,
                OmgUpdatedAt = order.OmgUpdatedAt,
                Status = order.Status,
                RawJson = order.RawJson
            };
        }

        private static string UtcNow()
        {
            return DateTimeOffset.UtcNow.UtcDateTime.ToString("o", CultureInfo.InvariantCulture);
        }

        private static string Truncate(string value, int maximumLength)
        {
            return value == null || value.Length <= maximumLength ? value : value.Substring(0, maximumLength);
        }

    }
}
