using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Cloud9.Omg.Connector.Models
{
    public sealed class OmgOrder
    {
        public string Id { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        public string Status { get; set; }
        public decimal GrandTotal { get; set; }
        public string ShippingMethod { get; set; }
        public OmgContact ShippingContact { get; set; }
        public OmgAddress ShippingAddress { get; set; }
        public List<OmgLineItem> LineItems { get; set; }
        public string RawJson { get; set; }

        public static OmgOrder FromJson(JObject json)
        {
            var contact = Object(json, "shipping_contact");
            var address = Object(json, "shipping_address");
            var lineItems = new List<OmgLineItem>();
            var lineItemArray = Array(json, "line_items");

            foreach (var token in lineItemArray)
            {
                var item = token as JObject;
                if (item == null)
                {
                    continue;
                }

                lineItems.Add(new OmgLineItem
                {
                    Id = RequiredLong(item, "id", "line_item_id"),
                    Quantity = Int(item, 0, "quantity")
                });
            }

            DateTimeOffset updatedAt;
            if (!DateTimeOffset.TryParse(String(json, "updated_at"), CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal, out updatedAt))
            {
                updatedAt = DateTimeOffset.UtcNow;
            }

            return new OmgOrder
            {
                Id = RequiredString(json, "order_id", "id"),
                UpdatedAt = updatedAt.ToUniversalTime(),
                Status = String(json, "status"),
                GrandTotal = Decimal(json, 0m, "grand_total", "total"),
                ShippingMethod = String(json, "shipping_method", "shipping_method_name"),
                ShippingContact = new OmgContact
                {
                    FirstName = String(contact, "first_name"),
                    LastName = String(contact, "last_name"),
                    Email = String(contact, "email"),
                    Phone = String(contact, "phone")
                },
                ShippingAddress = new OmgAddress
                {
                    Company = String(address, "company", "company_name"),
                    Address1 = String(address, "first_address", "address1", "address_1"),
                    Address2 = String(address, "second_address", "address2", "address_2"),
                    City = String(address, "city"),
                    State = String(address, "state", "province"),
                    Country = String(address, "country", "country_code"),
                    PostalCode = String(address, "zip", "postal_code", "zip_code")
                },
                LineItems = lineItems,
                RawJson = json.ToString(Formatting.None)
            };
        }

        private static JObject Object(JObject source, string name)
        {
            return source.GetValue(name, StringComparison.OrdinalIgnoreCase) as JObject ?? new JObject();
        }

        private static JArray Array(JObject source, string name)
        {
            return source.GetValue(name, StringComparison.OrdinalIgnoreCase) as JArray ?? new JArray();
        }

        private static string RequiredString(JObject source, params string[] names)
        {
            var value = String(source, names);
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException("OMG response is missing required field: " + string.Join(" or ", names));
            }

            return value;
        }

        private static long RequiredLong(JObject source, params string[] names)
        {
            long value;
            var raw = String(source, names);
            if (!long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) || value <= 0)
            {
                throw new InvalidOperationException("OMG response is missing required numeric field: " + string.Join(" or ", names));
            }

            return value;
        }

        private static string String(JObject source, params string[] names)
        {
            foreach (var name in names)
            {
                var token = source.GetValue(name, StringComparison.OrdinalIgnoreCase);
                if (token != null && token.Type != JTokenType.Null)
                {
                    return token.ToString().Trim();
                }
            }

            return string.Empty;
        }

        private static int Int(JObject source, int defaultValue, params string[] names)
        {
            int parsed;
            return int.TryParse(String(source, names), NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed)
                ? parsed
                : defaultValue;
        }

        private static decimal Decimal(JObject source, decimal defaultValue, params string[] names)
        {
            decimal parsed;
            return decimal.TryParse(String(source, names), NumberStyles.Number, CultureInfo.InvariantCulture, out parsed)
                ? parsed
                : defaultValue;
        }
    }

    public sealed class OmgContact
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }

        public string FullName
        {
            get { return (FirstName + " " + LastName).Trim(); }
        }
    }

    public sealed class OmgAddress
    {
        public string Company { get; set; }
        public string Address1 { get; set; }
        public string Address2 { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string Country { get; set; }
        public string PostalCode { get; set; }
    }

    public sealed class OmgLineItem
    {
        public long Id { get; set; }
        public int Quantity { get; set; }
    }

    public sealed class OmgShipmentRequest
    {
        [JsonProperty("tracking_number")]
        public string TrackingNumber { get; set; }

        [JsonProperty("ship_date")]
        public string ShipDate { get; set; }

        [JsonProperty("shipping_method")]
        public string ShippingMethod { get; set; }

        [JsonProperty("note")]
        public string Note { get; set; }

        [JsonProperty("send_shipping_confirmation")]
        public bool SendShippingConfirmation { get; set; }

        [JsonProperty("line_items")]
        public List<OmgShipmentLineItem> LineItems { get; set; }
    }

    public sealed class OmgShipmentLineItem
    {
        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("quantity")]
        public int Quantity { get; set; }
    }
}
