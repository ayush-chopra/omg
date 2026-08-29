using Cloud9.Omg.Connector.Configuration;
using System;
using System.Collections.Generic;

namespace Cloud9.Omg.Connector.Tests
{
    internal static class TestSettings
    {
        public static ConnectorSettings Create(string databasePath = "test.db")
        {
            return new ConnectorSettings
            {
                Enabled = true,
                OmgStoreType = "company",
                OmgBaseUrl = "https://company-store.example.test",
                OmgApplicationToken = "omg-token",
                Cloud9BaseUrl = "https://cloud9.example.test/v9.2/ShipService",
                Cloud9UserId = "api@example.test",
                Cloud9Password = "secret",
                Cloud9LocationId = 100,
                Cloud9ShopCode = "DEFAULT",
                Cloud9CarrierScac = string.Empty,
                Cloud9CarrierServiceCode = string.Empty,
                Cloud9PaymentType = "PRE",
                CallbackListenUrl = "http://localhost:8090/",
                Cloud9CallbackToken = "12345678901234567890123456789012",
                DatabasePath = databasePath,
                PollIntervalMinutes = 15,
                PollOverlapSeconds = 300,
                InitialLookbackHours = 24,
                PageSize = 100,
                OrderNumberPrefix = "OMG-",
                EligibleOrderStatuses = new HashSet<string>(new[] { "new", "paid" }, StringComparer.OrdinalIgnoreCase),
                SendShippingConfirmation = false,
                ShipFrom = new AddressSettings
                {
                    Contact = "Shipping Desk",
                    Company = "Cloud9 Express",
                    Address1 = "1 Warehouse Road",
                    Address2 = string.Empty,
                    City = "Dallas",
                    State = "TX",
                    PostalCode = "75201",
                    Country = "US",
                    Phone = ""
                },
                DefaultPackage = new PackageSettings
                {
                    Weight = 1m,
                    Length = 10m,
                    Width = 8m,
                    Height = 4m,
                    PackagingType = "CP"
                }
            };
        }
    }
}
