using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Cloud9.Omg.Connector.Clients;
using Cloud9.Omg.Connector.Configuration;
using Cloud9.Omg.Connector.Models;
using Cloud9.Omg.Connector.Persistence;
using Cloud9.Omg.Connector.Support;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Cloud9.Omg.Connector.Services
{
    public sealed class ShipmentCallbackService
    {
        private readonly ConnectorSettings _settings;
        private readonly IOmgClient _omgClient;
        private readonly IIntegrationRepository _repository;
        private readonly IConnectorLog _log;

        public ShipmentCallbackService(
            ConnectorSettings settings,
            IOmgClient omgClient,
            IIntegrationRepository repository,
            IConnectorLog log)
        {
            _settings = settings;
            _omgClient = omgClient;
            _repository = repository;
            _log = log;
        }

        public async Task<CallbackResult> ProcessAsync(
            Cloud9ShipJobCallback callback,
            CancellationToken cancellationToken)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            if (string.IsNullOrWhiteSpace(callback.OrderNumber))
            {
                throw new InvalidOperationException("Cloud9 callback is missing orderNumber.");
            }

            var orderRecord = _repository.FindOrderByCloud9Number(callback.OrderNumber);
            if (orderRecord == null)
            {
                throw new InvalidOperationException("No synchronized OMG order matches " + callback.OrderNumber + ".");
            }

            var packages = callback.Packages ?? new List<Cloud9CallbackPackage>();
            var firstTrackingNumber = packages.Select(package => package.TrackingNumber)
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
            var eventKey = Hash(callback.OrderNumber + "|" + firstTrackingNumber + "|" +
                                callback.IsReturn + "|" + callback.VoidDate);
            var costCents = ParseCostCents(callback.Cost);
            var payloadJson = JsonConvert.SerializeObject(callback);
            var reservation = _repository.ReserveShipmentEvent(
                eventKey,
                orderRecord.OmgOrderId,
                firstTrackingNumber,
                costCents,
                payloadJson);

            if (!reservation.IsNew)
            {
                return new CallbackResult("duplicate", "Callback was already processed as " + reservation.ExistingStatus + ".");
            }

            try
            {
                if (!string.IsNullOrWhiteSpace(callback.VoidDate))
                {
                    return Manual(eventKey, "Voided Cloud9 shipments require manual review; OMG has no confirmed public void-shipment operation.");
                }

                if (IsTrue(callback.IsReturn))
                {
                    return Manual(eventKey, "Return shipments require manual review; this workflow only writes outbound tracking to OMG.");
                }

                if (packages.Count != 1 || string.IsNullOrWhiteSpace(firstTrackingNumber))
                {
                    return Manual(eventKey, "Multi-package or missing-tracking callbacks require line-item allocation before OMG writeback.");
                }

                var order = orderRecord.ToOrder();
                if (order.LineItems.Count == 0 || order.LineItems.Any(item => item.Quantity <= 0))
                {
                    return Manual(eventKey, "OMG order has no valid line-item quantities for shipment writeback.");
                }

                var existingShipments = await _omgClient.GetShipmentsAsync(order.Id, cancellationToken).ConfigureAwait(false);
                var existing = existingShipments.FirstOrDefault(shipment =>
                    string.Equals(Value(shipment, "tracking_number", "trackingNumber"), firstTrackingNumber,
                        StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    var existingId = Value(existing, "id", "shipment_id");
                    _repository.MarkShipmentCompleted(eventKey, existingId);
                    return new CallbackResult("completed", "Tracking already existed in OMG; callback recorded idempotently.");
                }

                var remainingLineItems = RemainingLineItems(order, existingShipments);
                if (remainingLineItems.Count == 0)
                {
                    return Manual(
                        eventKey,
                        "OMG reports all order quantities as already shipped under other tracking numbers; automatic writeback was stopped.");
                }

                var request = new OmgShipmentRequest
                {
                    TrackingNumber = firstTrackingNumber,
                    ShipDate = ParseShipDate(callback.ShipDate).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    ShippingMethod = string.IsNullOrWhiteSpace(callback.ServiceType)
                        ? callback.CarrierScac
                        : callback.ServiceType,
                    Note = "Created from Cloud9 ship job callback.",
                    SendShippingConfirmation = _settings.SendShippingConfirmation,
                    LineItems = remainingLineItems
                };

                var created = await _omgClient.CreateShipmentAsync(order.Id, request, cancellationToken)
                    .ConfigureAwait(false);
                var shipmentId = Value(created, "id", "shipment_id");
                _repository.MarkShipmentCompleted(eventKey, shipmentId);
                _log.Info("Pushed tracking " + firstTrackingNumber + " to OMG order " + order.Id + ".");
                return new CallbackResult(
                    "completed",
                    "Tracking was written to OMG. Cost was retained internally because OMG exposes no confirmed shipment-cost field.");
            }
            catch (Exception exception)
            {
                _repository.MarkShipmentFailed(eventKey, exception.Message);
                _log.Error("Shipment callback failed for " + callback.OrderNumber + ".", exception);
                throw;
            }
        }

        private CallbackResult Manual(string eventKey, string reason)
        {
            _repository.MarkShipmentManualReview(eventKey, reason);
            _log.Info(reason);
            return new CallbackResult("manual_review", reason);
        }

        private static long ParseCostCents(string value)
        {
            decimal amount;
            return decimal.TryParse(value, NumberStyles.Currency, CultureInfo.InvariantCulture, out amount)
                ? decimal.ToInt64(decimal.Round(amount * 100m, 0, MidpointRounding.AwayFromZero))
                : 0L;
        }

        private static DateTime ParseShipDate(string value)
        {
            DateTime parsed;
            if (!DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out parsed))
            {
                throw new InvalidOperationException("Cloud9 callback contains an invalid shipDate.");
            }

            return parsed;
        }

        private static bool IsTrue(string value)
        {
            return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "y", StringComparison.OrdinalIgnoreCase);
        }

        private static List<OmgShipmentLineItem> RemainingLineItems(
            OmgOrder order,
            IEnumerable<JObject> existingShipments)
        {
            var shippedQuantities = new Dictionary<long, int>();
            foreach (var shipment in existingShipments)
            {
                var items = shipment.GetValue("line_items", StringComparison.OrdinalIgnoreCase) as JArray;
                if (items == null)
                {
                    continue;
                }

                foreach (var itemToken in items.OfType<JObject>())
                {
                    long itemId;
                    int quantity;
                    if (!long.TryParse(Value(itemToken, "id", "line_item_id"), NumberStyles.Integer,
                            CultureInfo.InvariantCulture, out itemId) ||
                        !int.TryParse(Value(itemToken, "quantity"), NumberStyles.Integer,
                            CultureInfo.InvariantCulture, out quantity) || quantity <= 0)
                    {
                        continue;
                    }

                    int alreadyShipped;
                    shippedQuantities.TryGetValue(itemId, out alreadyShipped);
                    shippedQuantities[itemId] = checked(alreadyShipped + quantity);
                }
            }

            return order.LineItems.Select(item =>
                {
                    int alreadyShipped;
                    shippedQuantities.TryGetValue(item.Id, out alreadyShipped);
                    return new OmgShipmentLineItem
                    {
                        Id = item.Id,
                        Quantity = Math.Max(0, item.Quantity - alreadyShipped)
                    };
                })
                .Where(item => item.Quantity > 0)
                .ToList();
        }

        private static string Hash(string value)
        {
            using (var algorithm = SHA256.Create())
            {
                return BitConverter.ToString(algorithm.ComputeHash(Encoding.UTF8.GetBytes(value)))
                    .Replace("-", string.Empty)
                    .ToLowerInvariant();
            }
        }

        private static string Value(JObject json, params string[] names)
        {
            foreach (var name in names)
            {
                var token = json.GetValue(name, StringComparison.OrdinalIgnoreCase);
                if (token != null && token.Type != JTokenType.Null)
                {
                    return token.ToString();
                }
            }

            return string.Empty;
        }
    }

    public sealed class CallbackResult
    {
        public CallbackResult(string status, string message)
        {
            Status = status;
            Message = message;
        }

        public string Status { get; private set; }
        public string Message { get; private set; }
    }
}
