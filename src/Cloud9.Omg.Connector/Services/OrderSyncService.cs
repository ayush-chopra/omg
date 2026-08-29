using System;
using System.Threading;
using System.Threading.Tasks;
using Cloud9.Omg.Connector.Clients;
using Cloud9.Omg.Connector.Configuration;
using Cloud9.Omg.Connector.Models;
using Cloud9.Omg.Connector.Persistence;
using Cloud9.Omg.Connector.Support;
using Newtonsoft.Json.Linq;

namespace Cloud9.Omg.Connector.Services
{
    public sealed class OrderSyncService
    {
        private const string CursorKey = "company_orders_updated_at";
        private readonly ConnectorSettings _settings;
        private readonly IOmgClient _omgClient;
        private readonly ICloud9Client _cloud9Client;
        private readonly IIntegrationRepository _repository;
        private readonly OrderMapper _mapper;
        private readonly IConnectorLog _log;
        private readonly SemaphoreSlim _runLock = new SemaphoreSlim(1, 1);

        public OrderSyncService(
            ConnectorSettings settings,
            IOmgClient omgClient,
            ICloud9Client cloud9Client,
            IIntegrationRepository repository,
            OrderMapper mapper,
            IConnectorLog log)
        {
            _settings = settings;
            _omgClient = omgClient;
            _cloud9Client = cloud9Client;
            _repository = repository;
            _mapper = mapper;
            _log = log;
        }

        public async Task RunAsync(CancellationToken cancellationToken)
        {
            if (!_settings.Enabled)
            {
                _log.Info("Connector is disabled; scheduled synchronization was skipped.");
                return;
            }

            if (!await _runLock.WaitAsync(0, cancellationToken).ConfigureAwait(false))
            {
                _log.Info("A synchronization is already running; overlapping run was skipped.");
                return;
            }

            try
            {
                await RetryFailedOrdersAsync(cancellationToken).ConfigureAwait(false);

                var upperBound = DateTimeOffset.UtcNow;
                var cursor = _repository.GetCursor(CursorKey);
                var lowerBound = cursor.HasValue
                    ? cursor.Value.AddSeconds(-_settings.PollOverlapSeconds)
                    : upperBound.AddHours(-_settings.InitialLookbackHours);

                var page = 1;
                var fetched = 0;
                var hadFetchFailure = false;
                while (page <= 1000)
                {
                    var summaries = await _omgClient.GetUpdatedOrdersAsync(
                            lowerBound,
                            upperBound,
                            page,
                            _settings.PageSize,
                            cancellationToken)
                        .ConfigureAwait(false);

                    foreach (var summary in summaries)
                    {
                        var orderId = Value(summary, "order_id", "id");
                        if (string.IsNullOrWhiteSpace(orderId))
                        {
                            hadFetchFailure = true;
                            _log.Error("OMG order summary did not contain an order ID.", null);
                            continue;
                        }

                        try
                        {
                            var order = await _omgClient.GetOrderAsync(orderId, cancellationToken).ConfigureAwait(false);
                            await SubmitIfNeededAsync(order, cancellationToken).ConfigureAwait(false);
                            fetched++;
                        }
                        catch (Exception exception)
                        {
                            hadFetchFailure = true;
                            _log.Error("Failed to retrieve or stage OMG order " + orderId + ".", exception);
                        }
                    }

                    if (summaries.Count < _settings.PageSize)
                    {
                        break;
                    }

                    page++;
                }

                if (!hadFetchFailure)
                {
                    _repository.SetCursor(CursorKey, upperBound);
                }

                _log.Info("OMG synchronization finished; orders fetched: " + fetched + ".");
            }
            finally
            {
                _runLock.Release();
            }
        }

        private async Task RetryFailedOrdersAsync(CancellationToken cancellationToken)
        {
            foreach (var record in _repository.GetRetryableOrders(100))
            {
                await SubmitOrderAsync(record.ToOrder(), cancellationToken).ConfigureAwait(false);
            }
        }

        private async Task SubmitIfNeededAsync(OmgOrder order, CancellationToken cancellationToken)
        {
            var cloud9OrderNumber = _mapper.GetCloud9OrderNumber(order);
            var needsSubmission = _repository.UpsertOrder(order, cloud9OrderNumber);
            if (!_settings.EligibleOrderStatuses.Contains(order.Status))
            {
                var reason = "OMG order status '" + order.Status + "' is not configured for Cloud9 submission.";
                _repository.MarkOrderSkipped(order.Id, reason);
                _log.Info("Skipped OMG order " + order.Id + ": " + reason);
                return;
            }

            if (needsSubmission)
            {
                await SubmitOrderAsync(order, cancellationToken).ConfigureAwait(false);
            }
        }

        private async Task SubmitOrderAsync(OmgOrder order, CancellationToken cancellationToken)
        {
            try
            {
                await _cloud9Client.AddShipJobAsync(_mapper.Map(order), cancellationToken).ConfigureAwait(false);
                _repository.MarkOrderSubmitted(order.Id);
                _log.Info("Submitted OMG order " + order.Id + " to Cloud9 AddShipJob.");
            }
            catch (Exception exception)
            {
                _repository.MarkOrderFailed(order.Id, exception.Message);
                _log.Error("Cloud9 submission failed for OMG order " + order.Id + ".", exception);
            }
        }

        private static string Value(JObject json, params string[] names)
        {
            foreach (var name in names)
            {
                var token = json.GetValue(name, StringComparison.OrdinalIgnoreCase);
                if (token != null)
                {
                    return token.ToString();
                }
            }

            return string.Empty;
        }
    }
}
