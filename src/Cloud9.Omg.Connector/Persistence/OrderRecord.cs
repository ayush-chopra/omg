using Cloud9.Omg.Connector.Models;

namespace Cloud9.Omg.Connector.Persistence
{
    public sealed class OrderRecord
    {
        public string OmgOrderId { get; set; }
        public string Cloud9OrderNumber { get; set; }
        public string OmgUpdatedAt { get; set; }
        public string Status { get; set; }
        public string RawJson { get; set; }

        public OmgOrder ToOrder()
        {
            return OmgOrder.FromJson(Newtonsoft.Json.Linq.JObject.Parse(RawJson));
        }
    }

    public sealed class EventReservation
    {
        public bool IsNew { get; set; }
        public string ExistingStatus { get; set; }
    }
}
