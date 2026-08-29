using System;
using System.Threading;
using System.Threading.Tasks;
using Cloud9.Omg.Connector.Web;
using Microsoft.Owin.Hosting;

namespace Cloud9.Omg.Connector.Hosting
{
    public sealed class ConnectorRuntime : IDisposable
    {
        private readonly CompositionRoot _root;
        private readonly CancellationTokenSource _stopping = new CancellationTokenSource();
        private IDisposable _webHost;
        private Timer _timer;

        public ConnectorRuntime(CompositionRoot root)
        {
            _root = root ?? throw new ArgumentNullException(nameof(root));
        }

        public void Start()
        {
            CompositionRoot.Current = _root;
            _webHost = WebApp.Start<Startup>(_root.Settings.CallbackListenUrl);
            _timer = new Timer(
                RunScheduledSync,
                null,
                TimeSpan.Zero,
                TimeSpan.FromMinutes(_root.Settings.PollIntervalMinutes));
            _root.Log.Info("Connector started at " + _root.Settings.CallbackListenUrl + ".");
        }

        public void Dispose()
        {
            _stopping.Cancel();
            if (_timer != null)
            {
                _timer.Dispose();
            }

            if (_webHost != null)
            {
                _webHost.Dispose();
            }

            _stopping.Dispose();
            _root.Dispose();
            CompositionRoot.Current = null;
        }

        private void RunScheduledSync(object state)
        {
            Task.Run(async () =>
            {
                try
                {
                    await _root.OrderSyncService.RunAsync(_stopping.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Normal during shutdown.
                }
                catch (Exception exception)
                {
                    _root.Log.Error("Scheduled synchronization failed.", exception);
                }
            });
        }
    }
}
