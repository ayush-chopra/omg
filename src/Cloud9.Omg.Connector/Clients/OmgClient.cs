using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Cloud9.Omg.Connector.Configuration;
using Cloud9.Omg.Connector.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Cloud9.Omg.Connector.Clients
{
    public sealed class OmgClient : IOmgClient, IDisposable
    {
        private const int MaximumResponseCharacters = 2 * 1024 * 1024;
        private readonly ConnectorSettings _settings;
        private readonly HttpClient _httpClient;
        private readonly bool _ownsHttpClient;

        public OmgClient(ConnectorSettings settings, HttpClient httpClient = null)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _ownsHttpClient = httpClient == null;
            _httpClient = httpClient ?? new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(60);
        }

        public async Task<IReadOnlyList<JObject>> GetUpdatedOrdersAsync(
            DateTimeOffset from,
            DateTimeOffset to,
            int page,
            int pageSize,
            CancellationToken cancellationToken)
        {
            var query = string.Format(
                CultureInfo.InvariantCulture,
                "?updated_at_from={0}&updated_at_to={1}&page={2}&per_page={3}",
                Uri.EscapeDataString(from.UtcDateTime.ToString("o", CultureInfo.InvariantCulture)),
                Uri.EscapeDataString(to.UtcDateTime.ToString("o", CultureInfo.InvariantCulture)),
                page,
                pageSize);

            var root = await SendAsync(HttpMethod.Get, Endpoint("orders") + query, null, cancellationToken)
                .ConfigureAwait(false);

            return ExtractArray(root, "orders").OfType<JObject>().ToList();
        }

        public async Task<OmgOrder> GetOrderAsync(string orderId, CancellationToken cancellationToken)
        {
            var root = await SendAsync(
                    HttpMethod.Get,
                    Endpoint("orders/" + Uri.EscapeDataString(orderId)),
                    null,
                    cancellationToken)
                .ConfigureAwait(false);

            var json = root as JObject;
            if (json == null)
            {
                throw new ApiException("OMG order response was not a JSON object.");
            }

            var wrapped = json.GetValue("order", StringComparison.OrdinalIgnoreCase) as JObject;
            return OmgOrder.FromJson(wrapped ?? json);
        }

        public async Task<IReadOnlyList<JObject>> GetShipmentsAsync(
            string orderId,
            CancellationToken cancellationToken)
        {
            var root = await SendAsync(
                    HttpMethod.Get,
                    Endpoint("orders/" + Uri.EscapeDataString(orderId) + "/shipments"),
                    null,
                    cancellationToken)
                .ConfigureAwait(false);

            return ExtractArray(root, "shipments").OfType<JObject>().ToList();
        }

        public async Task<JObject> CreateShipmentAsync(
            string orderId,
            OmgShipmentRequest shipment,
            CancellationToken cancellationToken)
        {
            var root = await SendAsync(
                    HttpMethod.Post,
                    Endpoint("orders/" + Uri.EscapeDataString(orderId) + "/shipments"),
                    JsonConvert.SerializeObject(new { shipment }),
                    cancellationToken)
                .ConfigureAwait(false);

            var json = root as JObject;
            if (json == null)
            {
                throw new ApiException("OMG create-shipment response was not a JSON object.");
            }

            return json.GetValue("shipment", StringComparison.OrdinalIgnoreCase) as JObject ?? json;
        }

        public void Dispose()
        {
            if (_ownsHttpClient)
            {
                _httpClient.Dispose();
            }
        }

        private string Endpoint(string path)
        {
            return _settings.OmgBaseUrl + "/api/v2.7.0/" + path;
        }

        private async Task<JToken> SendAsync(
            HttpMethod method,
            string url,
            string jsonBody,
            CancellationToken cancellationToken)
        {
            for (var attempt = 1; attempt <= 3; attempt++)
            {
                using (var request = new HttpRequestMessage(method, url))
                {
                    request.Headers.Add("X-Application-Token", _settings.OmgApplicationToken);
                    request.Headers.Accept.ParseAdd("application/json");
                    if (jsonBody != null)
                    {
                        request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
                    }

                    try
                    {
                        using (var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false))
                        {
                            var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                            if (body.Length > MaximumResponseCharacters)
                            {
                                throw new ApiException("OMG response exceeded the 2 MB safety limit.");
                            }

                            if (response.IsSuccessStatusCode)
                            {
                                return string.IsNullOrWhiteSpace(body) ? new JObject() : JToken.Parse(body);
                            }

                            if (attempt < 3 && IsTransient(response.StatusCode))
                            {
                                await Task.Delay(TimeSpan.FromSeconds(attempt * 2), cancellationToken).ConfigureAwait(false);
                                continue;
                            }

                            throw new ApiException(string.Format(
                                CultureInfo.InvariantCulture,
                                "OMG API returned HTTP {0}: {1}",
                                (int)response.StatusCode,
                                SafeMessage(body)));
                        }
                    }
                    catch (HttpRequestException exception)
                    {
                        if (attempt == 3)
                        {
                            throw new ApiException("Unable to reach the OMG API after three attempts.", exception);
                        }

                        await Task.Delay(TimeSpan.FromSeconds(attempt * 2), cancellationToken).ConfigureAwait(false);
                    }
                }
            }

            throw new ApiException("Unable to reach the OMG API.");
        }

        private static JArray ExtractArray(JToken root, string propertyName)
        {
            var directArray = root as JArray;
            if (directArray != null)
            {
                return directArray;
            }

            var json = root as JObject;
            return json == null
                ? new JArray()
                : json.GetValue(propertyName, StringComparison.OrdinalIgnoreCase) as JArray ?? new JArray();
        }

        private static bool IsTransient(HttpStatusCode statusCode)
        {
            return statusCode == (HttpStatusCode)429 || (int)statusCode >= 500;
        }

        private static string SafeMessage(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                return "No error details returned.";
            }

            try
            {
                var json = JObject.Parse(body);
                var message = json.GetValue("message", StringComparison.OrdinalIgnoreCase) ??
                              json.GetValue("error", StringComparison.OrdinalIgnoreCase);
                return message == null ? "Error details omitted." : message.ToString();
            }
            catch (JsonException)
            {
                return "Non-JSON error response omitted.";
            }
        }
    }
}
