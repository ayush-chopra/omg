using System.ServiceProcess;

namespace Cloud9.Omg.Connector.Hosting
{
    public sealed class ConnectorWindowsService : ServiceBase
    {
        private readonly ConnectorRuntime _runtime;

        public ConnectorWindowsService(ConnectorRuntime runtime)
        {
            _runtime = runtime;
            ServiceName = "Cloud9OmgConnector";
            CanStop = true;
            AutoLog = true;
        }

        protected override void OnStart(string[] args)
        {
            _runtime.Start();
        }

        protected override void OnStop()
        {
            _runtime.Dispose();
        }
    }
}
