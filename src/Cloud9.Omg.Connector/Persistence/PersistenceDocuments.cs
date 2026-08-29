using LiteDB;

namespace Cloud9.Omg.Connector.Persistence
{
    internal sealed class SyncStateDocument
    {
        [BsonId]
        public string Id { get; set; }

        public string Value { get; set; }
        public string UpdatedAt { get; set; }
    }

    internal sealed class OrderDocument
    {
        [BsonId]
        public string OmgOrderId { get; set; }

        public string Cloud9OrderNumber { get; set; }
        public string OmgUpdatedAt { get; set; }
        public string Status { get; set; }
        public string RawJson { get; set; }
        public int AttemptCount { get; set; }
        public string Cloud9SubmittedAt { get; set; }
        public string LastError { get; set; }
        public string CreatedAt { get; set; }
        public string UpdatedAt { get; set; }
    }

    internal sealed class ShipmentEventDocument
    {
        [BsonId]
        public string EventKey { get; set; }

        public string OmgOrderId { get; set; }
        public string TrackingNumber { get; set; }
        public long CostCents { get; set; }
        public string PayloadJson { get; set; }
        public string Status { get; set; }
        public string OmgShipmentId { get; set; }
        public string LastError { get; set; }
        public int AttemptCount { get; set; }
        public string CreatedAt { get; set; }
        public string UpdatedAt { get; set; }
    }
}
