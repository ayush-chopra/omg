using Cloud9.Omg.Connector.Clients;
using Cloud9.Omg.Connector.Configuration;
using Cloud9.Omg.Connector.Persistence;
using Cloud9.Omg.Connector.Services;
using Cloud9.Omg.Connector.Support;

namespace Cloud9.Omg.Connector
{
    public sealed class CompositionRoot
    {
        public CompositionRoot(ConnectorSettings settings)
        {
            Settings = settings;
            Log = new ConsoleConnectorLog();
            Repository = new IntegrationRepository(settings.DatabasePath);
            OmgClient = new OmgClient(settings);
            Cloud9Client = new Cloud9Client(settings);
            var mapper = new OrderMapper(settings);
            OrderSyncService = new OrderSyncService(settings, OmgClient, Cloud9Client, Repository, mapper, Log);
            ShipmentCallbackService = new ShipmentCallbackService(settings, OmgClient, Repository, Log);
        }

        public static CompositionRoot Current { get; set; }

        public ConnectorSettings Settings { get; private set; }
        public IConnectorLog Log { get; private set; }
        public IntegrationRepository Repository { get; private set; }
        public OmgClient OmgClient { get; private set; }
        public Cloud9Client Cloud9Client { get; private set; }
        public OrderSyncService OrderSyncService { get; private set; }
        public ShipmentCallbackService ShipmentCallbackService { get; private set; }

        public void Dispose()
        {
            Cloud9Client.Dispose();
            OmgClient.Dispose();
            Repository.Dispose();
        }
    }
}
