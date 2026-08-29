using System;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Cloud9.Omg.Connector.Configuration;
using Cloud9.Omg.Connector.Models;
using Newtonsoft.Json;

namespace Cloud9.Omg.Connector.Clients
{
    public sealed class Cloud9Client : ICloud9Client, IDisposable
    {
        private readonly ConnectorSettings _settings;
        private readonly HttpClient _httpClient;
        private readonly bool _ownsHttpClient;
        private readonly SemaphoreSlim _authenticationLock = new SemaphoreSlim(1, 1);
        private string _authToken;
        private DateTimeOffset _authExpiresAt;

        public Cloud9Client(ConnectorSettings settings, HttpClient httpClient = null)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _ownsHttpClient = httpClient == null;
            _httpClient = httpClient ?? new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(60);
        }

        public async Task AddShipJobAsync(Cloud9ShipJob job, CancellationToken cancellationToken)
        {
            if (job == null)
            {
                throw new ArgumentNullException(nameof(job));
            }

            var token = await GetAuthTokenAsync(false, cancellationToken).ConfigureAwait(false);
            var result = await PostShipJobAsync(job, token, cancellationToken).ConfigureAwait(false);

            if (!result.IsSuccess && result.ErrorCode == 102)
            {
                token = await GetAuthTokenAsync(true, cancellationToken).ConfigureAwait(false);
                result = await PostShipJobAsync(job, token, cancellationToken).ConfigureAwait(false);
            }

            if (!result.IsSuccess)
            {
                throw new ApiException(string.Format(
                    CultureInfo.InvariantCulture,
                    "Cloud9 AddShipJob failed ({0}): {1}",
                    result.ErrorCode,
                    result.ErrorDescription ?? "No error description returned."));
            }
        }

        public void Dispose()
        {
            _authenticationLock.Dispose();
            if (_ownsHttpClient)
            {
                _httpClient.Dispose();
            }
        }

        private async Task<string> GetAuthTokenAsync(bool forceRefresh, CancellationToken cancellationToken)
        {
            if (!forceRefresh && !string.IsNullOrWhiteSpace(_authToken) && _authExpiresAt > DateTimeOffset.UtcNow)
            {
                return _authToken;
            }

            await _authenticationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (!forceRefresh && !string.IsNullOrWhiteSpace(_authToken) && _authExpiresAt > DateTimeOffset.UtcNow)
                {
                    return _authToken;
                }

                var request = new Cloud9AuthenticateRequest
                {
                    UserId = _settings.Cloud9UserId,
                    Token = _settings.Cloud9Password
                };

                var response = await PostAsync<Cloud9AuthenticateResponse>(
                        _settings.Cloud9BaseUrl + "/Auth/Authenticate",
                        request,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (!response.IsSuccess || string.IsNullOrWhiteSpace(response.AuthToken))
                {
                    throw new ApiException(string.Format(
                        CultureInfo.InvariantCulture,
                        "Cloud9 authentication failed ({0}): {1}",
                        response.ErrorCode,
                        response.ErrorDescription ?? "No error description returned."));
                }

                _authToken = response.AuthToken;
                var safeLifetimeSeconds = Math.Max(60, response.Ttl - 120);
                _authExpiresAt = DateTimeOffset.UtcNow.AddSeconds(safeLifetimeSeconds);
                return _authToken;
            }
            finally
            {
                _authenticationLock.Release();
            }
        }

        private Task<Cloud9Result> PostShipJobAsync(
            Cloud9ShipJob job,
            string authToken,
            CancellationToken cancellationToken)
        {
            var request = new AddShipJobRequest
            {
                UserInfo = new Cloud9UserInfo
                {
                    AuthToken = authToken,
                    UserId = _settings.Cloud9UserId,
                    LocationId = _settings.Cloud9LocationId
                },
                Job = job
            };

            return PostAsync<Cloud9Result>(
                _settings.Cloud9BaseUrl + "/Data/AddShipJob",
                request,
                cancellationToken);
        }

        private async Task<T> PostAsync<T>(string url, object payload, CancellationToken cancellationToken)
        {
            var json = JsonConvert.SerializeObject(payload);
            for (var attempt = 1; attempt <= 3; attempt++)
            {
                try
                {
                    using (var content = new StringContent(json, Encoding.UTF8, "application/json"))
                    using (var response = await _httpClient.PostAsync(url, content, cancellationToken).ConfigureAwait(false))
                    {
                        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        if (response.IsSuccessStatusCode)
                        {
                            var parsed = JsonConvert.DeserializeObject<T>(body);
                            if (parsed == null)
                            {
                                throw new ApiException("Cloud9 returned an empty or invalid JSON response.");
                            }

                            return parsed;
                        }

                        if (attempt < 3 && ((int)response.StatusCode == 429 || (int)response.StatusCode >= 500))
                        {
                            await Task.Delay(TimeSpan.FromSeconds(attempt * 2), cancellationToken).ConfigureAwait(false);
                            continue;
                        }

                        // Cloud9 returns expired-auth details as HTTP 400 with errorCode 102.
                        // Preserve structured error responses so AddShipJob can refresh once.
                        if (!string.IsNullOrWhiteSpace(body))
                        {
                            try
                            {
                                var errorResult = JsonConvert.DeserializeObject<T>(body);
                                if (errorResult != null)
                                {
                                    return errorResult;
                                }
                            }
                            catch (JsonException)
                            {
                                // Fall through to the status-only error below.
                            }
                        }

                        throw new ApiException(string.Format(
                            CultureInfo.InvariantCulture,
                            "Cloud9 API returned HTTP {0}.",
                            (int)response.StatusCode));
                    }
                }
                catch (HttpRequestException exception)
                {
                    if (attempt == 3)
                    {
                        throw new ApiException("Unable to reach Cloud9 after three attempts.", exception);
                    }

                    await Task.Delay(TimeSpan.FromSeconds(attempt * 2), cancellationToken).ConfigureAwait(false);
                }
            }

            throw new ApiException("Unable to reach Cloud9.");
        }
    }
}
