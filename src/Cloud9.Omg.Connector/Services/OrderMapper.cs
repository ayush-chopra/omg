using System;
using System.Collections.Generic;
using System.Globalization;
using Cloud9.Omg.Connector.Configuration;
using Cloud9.Omg.Connector.Models;

namespace Cloud9.Omg.Connector.Services
{
    public sealed class OrderMapper
    {
        private static readonly IDictionary<string, string> StateCodes =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Alabama", "AL" }, { "Alaska", "AK" }, { "Arizona", "AZ" }, { "Arkansas", "AR" },
                { "California", "CA" }, { "Colorado", "CO" }, { "Connecticut", "CT" }, { "Delaware", "DE" },
                { "Florida", "FL" }, { "Georgia", "GA" }, { "Hawaii", "HI" }, { "Idaho", "ID" },
                { "Illinois", "IL" }, { "Indiana", "IN" }, { "Iowa", "IA" }, { "Kansas", "KS" },
                { "Kentucky", "KY" }, { "Louisiana", "LA" }, { "Maine", "ME" }, { "Maryland", "MD" },
                { "Massachusetts", "MA" }, { "Michigan", "MI" }, { "Minnesota", "MN" }, { "Mississippi", "MS" },
                { "Missouri", "MO" }, { "Montana", "MT" }, { "Nebraska", "NE" }, { "Nevada", "NV" },
                { "New Hampshire", "NH" }, { "New Jersey", "NJ" }, { "New Mexico", "NM" }, { "New York", "NY" },
                { "North Carolina", "NC" }, { "North Dakota", "ND" }, { "Ohio", "OH" }, { "Oklahoma", "OK" },
                { "Oregon", "OR" }, { "Pennsylvania", "PA" }, { "Rhode Island", "RI" },
                { "South Carolina", "SC" }, { "South Dakota", "SD" }, { "Tennessee", "TN" }, { "Texas", "TX" },
                { "Utah", "UT" }, { "Vermont", "VT" }, { "Virginia", "VA" }, { "Washington", "WA" },
                { "West Virginia", "WV" }, { "Wisconsin", "WI" }, { "Wyoming", "WY" },
                { "District of Columbia", "DC" }
            };

        private readonly ConnectorSettings _settings;

        public OrderMapper(ConnectorSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public string GetCloud9OrderNumber(OmgOrder order)
        {
            return _settings.OrderNumberPrefix + order.Id;
        }

        public Cloud9ShipJob Map(OmgOrder order)
        {
            if (order == null)
            {
                throw new ArgumentNullException(nameof(order));
            }

            Require(order.ShippingContact.FullName, "shipping contact");
            Require(order.ShippingAddress.Address1, "shipping address");
            Require(order.ShippingAddress.City, "shipping city");
            Require(order.ShippingAddress.State, "shipping state");
            Require(order.ShippingAddress.PostalCode, "shipping postal code");

            var country = NormalizeCountry(order.ShippingAddress.Country);
            if (!string.Equals(country, "US", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Order " + order.Id + " is international. Customs mapping must be approved before it can be submitted automatically.");
            }

            return new Cloud9ShipJob
            {
                OrderNumber = GetCloud9OrderNumber(order),
                SourceSystemId = "OMG_COMPANY",
                Action = string.Empty,
                ShopCode = _settings.Cloud9ShopCode,
                CarrierScac = string.IsNullOrWhiteSpace(_settings.Cloud9ShopCode) ? _settings.Cloud9CarrierScac : string.Empty,
                CarrierServiceCode = string.IsNullOrWhiteSpace(_settings.Cloud9ShopCode)
                    ? _settings.Cloud9CarrierServiceCode
                    : string.Empty,
                PaymentType = _settings.Cloud9PaymentType,
                PaymentAccount = string.Empty,
                PaymentAccountPostalCode = string.Empty,
                FromContact = _settings.ShipFrom.Contact,
                FromCompany = _settings.ShipFrom.Company,
                FromAddress = _settings.ShipFrom.Address1,
                FromAddress2 = _settings.ShipFrom.Address2,
                FromCity = _settings.ShipFrom.City,
                FromState = _settings.ShipFrom.State,
                FromPostalCode = _settings.ShipFrom.PostalCode,
                FromCountry = _settings.ShipFrom.Country,
                FromPhone = _settings.ShipFrom.Phone,
                ToContact = order.ShippingContact.FullName,
                ToCompany = order.ShippingAddress.Company,
                ToAddress = order.ShippingAddress.Address1,
                ToAddress2 = order.ShippingAddress.Address2,
                ToCity = order.ShippingAddress.City,
                ToState = NormalizeState(order.ShippingAddress.State),
                ToPostalCode = order.ShippingAddress.PostalCode,
                ToCountry = country,
                ToPhone = order.ShippingContact.Phone,
                ShipNotifyEmail = order.ShippingContact.Email,
                Packages = new List<Cloud9JobPackage>
                {
                    new Cloud9JobPackage
                    {
                        Length = Number(_settings.DefaultPackage.Length),
                        Width = Number(_settings.DefaultPackage.Width),
                        Height = Number(_settings.DefaultPackage.Height),
                        Weight = Number(_settings.DefaultPackage.Weight),
                        PackagingType = _settings.DefaultPackage.PackagingType,
                        DeclaredValue = order.GrandTotal.ToString("0.00", CultureInfo.InvariantCulture),
                        DcisType = string.Empty,
                        ExtraHandling = "false",
                        NonStandardContainer = "false",
                        Reference2 = order.Id,
                        Reference3 = string.Empty
                    }
                }
            };
        }

        private static string NormalizeCountry(string country)
        {
            if (string.IsNullOrWhiteSpace(country) ||
                string.Equals(country, "US", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(country, "USA", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(country, "United States", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(country, "United States of America", StringComparison.OrdinalIgnoreCase))
            {
                return "US";
            }

            return country.Trim().ToUpperInvariant();
        }

        private static string NormalizeState(string state)
        {
            string code;
            return StateCodes.TryGetValue(state.Trim(), out code) ? code : state.Trim().ToUpperInvariant();
        }

        private static string Number(decimal value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private static void Require(string value, string field)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException("OMG order is missing " + field + ".");
            }
        }
    }
}
