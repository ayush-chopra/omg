using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Cloud9.Omg.Connector.Clients;
using Cloud9.Omg.Connector.Models;
using Xunit;

namespace Cloud9.Omg.Connector.Tests
{
    public sealed class Cloud9ClientTests
    {
        [Fact]
        public async Task RefreshesAuthenticationOnceWhenCloud9ReturnsError102()
        {
            var handler = new ExpiringTokenHandler();
            var client = new Cloud9Client(TestSettings.Create(), new HttpClient(handler));

            await client.AddShipJobAsync(new Cloud9ShipJob
            {
                OrderNumber = "OMG-12345"
            }, CancellationToken.None);

            Assert.Equal(2, handler.AuthenticationCalls);
            Assert.Equal(2, handler.AddShipJobCalls);
        }

        private sealed class ExpiringTokenHandler : HttpMessageHandler
        {
            public int AuthenticationCalls { get; private set; }
            public int AddShipJobCalls { get; private set; }

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                if (request.RequestUri.AbsolutePath.EndsWith("/Auth/Authenticate", StringComparison.Ordinal))
                {
                    AuthenticationCalls++;
                    return Json(HttpStatusCode.OK, "{ 'authToken': 'token-" + AuthenticationCalls +
                                                   "', 'locationId': 100, 'ttl': 3600, 'isSuccess': true, 'errorCode': 0 }");
                }

                AddShipJobCalls++;
                return AddShipJobCalls == 1
                    ? Json(HttpStatusCode.BadRequest,
                        "{ 'isSuccess': false, 'errorCode': 102, 'errorDesc': 'Auth token validation failed' }")
                    : Json(HttpStatusCode.OK, "{ 'isSuccess': true, 'errorCode': 0 }");
            }

            private static Task<HttpResponseMessage> Json(HttpStatusCode status, string body)
            {
                return Task.FromResult(new HttpResponseMessage(status)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json")
                });
            }
        }
    }
}
