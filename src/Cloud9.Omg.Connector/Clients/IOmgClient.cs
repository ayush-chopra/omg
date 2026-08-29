using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cloud9.Omg.Connector.Models;
using Newtonsoft.Json.Linq;

namespace Cloud9.Omg.Connector.Clients
{
    public interface IOmgClient
    {
        Task<IReadOnlyList<JObject>> GetUpdatedOrdersAsync(
            DateTimeOffset from,
            DateTimeOffset to,
            int page,
            int pageSize,
            CancellationToken cancellationToken);

        Task<OmgOrder> GetOrderAsync(string orderId, CancellationToken cancellationToken);

        Task<IReadOnlyList<JObject>> GetShipmentsAsync(string orderId, CancellationToken cancellationToken);

        Task<JObject> CreateShipmentAsync(
            string orderId,
            OmgShipmentRequest shipment,
            CancellationToken cancellationToken);
    }
}
