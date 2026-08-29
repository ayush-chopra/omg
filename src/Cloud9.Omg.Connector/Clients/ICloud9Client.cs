using System.Threading;
using System.Threading.Tasks;
using Cloud9.Omg.Connector.Models;

namespace Cloud9.Omg.Connector.Clients
{
    public interface ICloud9Client
    {
        Task AddShipJobAsync(Cloud9ShipJob job, CancellationToken cancellationToken);
    }
}
