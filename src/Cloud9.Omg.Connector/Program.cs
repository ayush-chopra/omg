using System;
using System.Linq;
using System.Net;
using System.Threading;
using Cloud9.Omg.Connector.Configuration;
using Cloud9.Omg.Connector.Hosting;

namespace Cloud9.Omg.Connector
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            try
            {
                var settings = ConnectorSettings.Load();
                var root = new CompositionRoot(settings);

                if (args.Any(argument => string.Equals(argument, "--run-once", StringComparison.OrdinalIgnoreCase)))
                {
                    try
                    {
                        root.OrderSyncService.RunAsync(CancellationToken.None).GetAwaiter().GetResult();
                        return 0;
                    }
                    finally
                    {
                        root.Dispose();
                    }
                }

                var runtime = new ConnectorRuntime(root);
                using (runtime)
                using (var stopped = new ManualResetEventSlim(false))
                {
                    ConsoleCancelEventHandler stopHandler = (sender, eventArgs) =>
                    {
                        eventArgs.Cancel = true;
                        stopped.Set();
                    };

                    Console.CancelKeyPress += stopHandler;
                    try
                    {
                        runtime.Start();
                        Console.WriteLine("Cloud9 OMG Connector is running. Press Ctrl+C to stop.");
                        stopped.Wait();
                    }
                    finally
                    {
                        Console.CancelKeyPress -= stopHandler;
                    }
                }

                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(exception);
                return 1;
            }
        }
    }
}
