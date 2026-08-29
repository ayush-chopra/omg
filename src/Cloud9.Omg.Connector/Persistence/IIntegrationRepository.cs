using System;
using System.Collections.Generic;
using Cloud9.Omg.Connector.Models;

namespace Cloud9.Omg.Connector.Persistence
{
    public interface IIntegrationRepository
    {
        DateTimeOffset? GetCursor(string key);
        void SetCursor(string key, DateTimeOffset value);
        bool UpsertOrder(OmgOrder order, string cloud9OrderNumber);
        IReadOnlyList<OrderRecord> GetRetryableOrders(int limit);
        OrderRecord FindOrderByCloud9Number(string cloud9OrderNumber);
        void MarkOrderSubmitted(string omgOrderId);
        void MarkOrderSkipped(string omgOrderId, string reason);
        void MarkOrderFailed(string omgOrderId, string error);
        EventReservation ReserveShipmentEvent(
            string eventKey,
            string omgOrderId,
            string trackingNumber,
            long costCents,
            string payloadJson);
        void MarkShipmentCompleted(string eventKey, string omgShipmentId);
        void MarkShipmentManualReview(string eventKey, string reason);
        void MarkShipmentFailed(string eventKey, string error);
    }
}
