using Cloud9.Omg.Connector.Models;
using Cloud9.Omg.Connector.Services;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Cloud9.Omg.Connector.Tests
{
    public sealed class OrderMapperTests
    {
        [Fact]
        public void MapsCompanyStoreOrderToCloud9AddShipJob()
        {
            var settings = TestSettings.Create();
            var mapper = new OrderMapper(settings);
            var order = OmgOrder.FromJson(JObject.Parse(@"{
                'order_id': 12345,
                'status': 'new',
                'updated_at': '2026-08-30T10:00:00Z',
                'grand_total': '85.50',
                'shipping_contact': {
                    'first_name': 'Jane', 'last_name': 'Doe',
                    'email': 'jane@example.test', 'phone': '555-0100'
                },
                'shipping_address': {
                    'company': 'Example Co', 'first_address': '100 Main St',
                    'second_address': 'Suite 2', 'city': 'Austin',
                    'state': 'Texas', 'country': 'United States', 'zip': '78701'
                },
                'line_items': [{ 'id': 77, 'quantity': 2 }]
            }"));

            var result = mapper.Map(order);

            Assert.Equal("OMG-12345", result.OrderNumber);
            Assert.Equal("DEFAULT", result.ShopCode);
            Assert.Equal(string.Empty, result.CarrierScac);
            Assert.Equal("Jane Doe", result.ToContact);
            Assert.Equal("TX", result.ToState);
            Assert.Equal("US", result.ToCountry);
            Assert.Equal("85.50", result.Packages[0].DeclaredValue);
        }
    }
}
