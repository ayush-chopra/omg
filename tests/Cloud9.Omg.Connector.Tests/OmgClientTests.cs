using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Cloud9.Omg.Connector.Clients;
using Cloud9.Omg.Connector.Models;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Cloud9.Omg.Connector.Tests
{
    public sealed class OmgClientTests
    {
        [Fact]
        public async Task CreateShipmentUsesRequiredWrapperHeaderAndNumericLineItemId()
        {
            var handler = new CapturingHandler();
            var httpClient = new HttpClient(handler);
            var settings = TestSettings.Create();
            var client = new OmgClient(settings, httpClient);

            await client.CreateShipmentAsync("12345", new OmgShipmentRequest
            {
                TrackingNumber = "1Z999",
                ShipDate = "2026-08-30",
                ShippingMethod = "UPS Ground",
                Note = "Created from Cloud9 ship job callback.",
                LineItems = new List<OmgShipmentLineItem>
                {
                    new OmgShipmentLineItem { Id = 77, Quantity = 2 }
                }
            }, CancellationToken.None);

            Assert.Equal("omg-token", handler.ApplicationToken);
            Assert.Equal(
                "https://company-store.example.test/api/v2.7.0/orders/12345/shipments",
                handler.RequestUri.ToString());
            var json = JObject.Parse(handler.Body);
            Assert.NotNull(json["shipment"]);
            Assert.Equal(JTokenType.Integer, json["shipment"]["line_items"][0]["id"].Type);
        }

        private sealed class CapturingHandler : HttpMessageHandler
        {
            public string ApplicationToken { get; private set; }
            public Uri RequestUri { get; private set; }
            public string Body { get; private set; }

            protected override async Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                IEnumerable<string> values;
                ApplicationToken = request.Headers.TryGetValues("X-Application-Token", out values)
                    ? string.Join(",", values)
                    : null;
                RequestUri = request.RequestUri;
                Body = await request.Content.ReadAsStringAsync();
                return new HttpResponseMessage(HttpStatusCode.Created)
                {
                    Content = new StringContent("{ 'id': 9001 }", Encoding.UTF8, "application/json")
                };
            }
        }
    }
}
