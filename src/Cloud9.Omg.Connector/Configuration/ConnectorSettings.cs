using System;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace Cloud9.Omg.Connector.Configuration
{
    public sealed class ConnectorSettings
    {
        public bool Enabled { get; set; }
        public string OmgStoreType { get; set; }
        public string OmgBaseUrl { get; set; }
        public string OmgApplicationToken { get; set; }
        public string Cloud9BaseUrl { get; set; }
        public string Cloud9UserId { get; set; }
        public string Cloud9Password { get; set; }
        public int Cloud9LocationId { get; set; }
        public string Cloud9ShopCode { get; set; }
        public string Cloud9CarrierScac { get; set; }
        public string Cloud9CarrierServiceCode { get; set; }
        public string Cloud9PaymentType { get; set; }
        public string CallbackListenUrl { get; set; }
        public string Cloud9CallbackToken { get; set; }
        public string DatabasePath { get; set; }
        public int PollIntervalMinutes { get; set; }
        public int PollOverlapSeconds { get; set; }
        public int InitialLookbackHours { get; set; }
        public int PageSize { get; set; }
        public string OrderNumberPrefix { get; set; }
        public ISet<string> EligibleOrderStatuses { get; set; }
        public bool SendShippingConfirmation { get; set; }
        public AddressSettings ShipFrom { get; set; }
        public PackageSettings DefaultPackage { get; set; }

        public ConnectorSettings()
        {
        }

        public static ConnectorSettings Load()
        {
            var settings = new ConnectorSettings
            {
                Enabled = GetBool("Enabled", false),
                OmgStoreType = Get("OmgStoreType", "company"),
                OmgBaseUrl = Get("OmgBaseUrl", string.Empty).TrimEnd('/'),
                OmgApplicationToken = Get("OmgApplicationToken", string.Empty),
                Cloud9BaseUrl = Get("Cloud9BaseUrl", string.Empty).TrimEnd('/'),
                Cloud9UserId = Get("Cloud9UserId", string.Empty),
                Cloud9Password = Get("Cloud9Password", string.Empty),
                Cloud9LocationId = GetInt("Cloud9LocationId", 0),
                Cloud9ShopCode = Get("Cloud9ShopCode", string.Empty),
                Cloud9CarrierScac = Get("Cloud9CarrierScac", string.Empty).ToUpperInvariant(),
                Cloud9CarrierServiceCode = Get("Cloud9CarrierServiceCode", string.Empty),
                Cloud9PaymentType = Get("Cloud9PaymentType", "PRE").ToUpperInvariant(),
                CallbackListenUrl = EnsureTrailingSlash(Get("CallbackListenUrl", "http://localhost:8090/")),
                Cloud9CallbackToken = Get("Cloud9CallbackToken", string.Empty),
                DatabasePath = ResolvePath(Get("DatabasePath", "storage\\omg-connector.db")),
                PollIntervalMinutes = GetInt("PollIntervalMinutes", 15),
                PollOverlapSeconds = GetInt("PollOverlapSeconds", 300),
                InitialLookbackHours = GetInt("InitialLookbackHours", 24),
                PageSize = GetInt("PageSize", 100),
                OrderNumberPrefix = Get("OrderNumberPrefix", "OMG-"),
                EligibleOrderStatuses = new HashSet<string>(
                    Get("EligibleOrderStatuses", "new,paid")
                        .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(status => status.Trim()),
                    StringComparer.OrdinalIgnoreCase),
                SendShippingConfirmation = GetBool("SendShippingConfirmation", false),
                ShipFrom = new AddressSettings
                {
                    Contact = Get("ShipFromContact", string.Empty),
                    Company = Get("ShipFromCompany", string.Empty),
                    Address1 = Get("ShipFromAddress1", string.Empty),
                    Address2 = Get("ShipFromAddress2", string.Empty),
                    City = Get("ShipFromCity", string.Empty),
                    State = Get("ShipFromState", string.Empty).ToUpperInvariant(),
                    PostalCode = Get("ShipFromPostalCode", string.Empty),
                    Country = Get("ShipFromCountry", "US").ToUpperInvariant(),
                    Phone = Get("ShipFromPhone", string.Empty)
                },
                DefaultPackage = new PackageSettings
                {
                    Weight = GetDecimal("DefaultPackageWeight", 1m),
                    Length = GetDecimal("DefaultPackageLength", 10m),
                    Width = GetDecimal("DefaultPackageWidth", 8m),
                    Height = GetDecimal("DefaultPackageHeight", 4m),
                    PackagingType = Get("DefaultPackagingType", "CP")
                }
            };

            settings.Validate();
            return settings;
        }

        public void Validate()
        {
            if (!Enabled)
            {
                return;
            }

            if (!string.Equals(OmgStoreType, "company", StringComparison.OrdinalIgnoreCase))
            {
                throw new ConfigurationErrorsException(
                    "Only OMG Company Stores are supported; Pop-up Stores and Websites do not have a confirmed public shipment-writeback endpoint.");
            }

            RequireHttps(OmgBaseUrl, "OmgBaseUrl");
            RequireHttps(Cloud9BaseUrl, "Cloud9BaseUrl");
            Require(OmgApplicationToken, "OmgApplicationToken");
            Require(Cloud9UserId, "Cloud9UserId");
            Require(Cloud9Password, "Cloud9Password");
            Require(Cloud9CallbackToken, "Cloud9CallbackToken");
            Require(ShipFrom.Contact, "ShipFromContact");
            Require(ShipFrom.Address1, "ShipFromAddress1");
            Require(ShipFrom.City, "ShipFromCity");
            Require(ShipFrom.State, "ShipFromState");
            Require(ShipFrom.PostalCode, "ShipFromPostalCode");
            Require(ShipFrom.Country, "ShipFromCountry");

            if (Cloud9LocationId <= 0)
            {
                throw new ConfigurationErrorsException("Cloud9LocationId must be positive.");
            }

            if (Cloud9CallbackToken.Length < 32)
            {
                throw new ConfigurationErrorsException("Cloud9CallbackToken must contain at least 32 characters.");
            }

            if (string.IsNullOrWhiteSpace(Cloud9ShopCode) &&
                (string.IsNullOrWhiteSpace(Cloud9CarrierScac) || string.IsNullOrWhiteSpace(Cloud9CarrierServiceCode)))
            {
                throw new ConfigurationErrorsException(
                    "Configure Cloud9ShopCode or both Cloud9CarrierScac and Cloud9CarrierServiceCode.");
            }

            if (PollIntervalMinutes < 1 || PollIntervalMinutes > 1440 ||
                PollOverlapSeconds < 0 || PollOverlapSeconds > 3600 ||
                InitialLookbackHours < 1 || InitialLookbackHours > 720 ||
                PageSize < 1 || PageSize > 500)
            {
                throw new ConfigurationErrorsException("Polling configuration is outside the allowed range.");
            }

            if (DefaultPackage.Weight <= 0 || DefaultPackage.Length <= 0 ||
                DefaultPackage.Width <= 0 || DefaultPackage.Height <= 0)
            {
                throw new ConfigurationErrorsException("Default package measurements must be positive.");
            }

            if (EligibleOrderStatuses == null || EligibleOrderStatuses.Count == 0)
            {
                throw new ConfigurationErrorsException("EligibleOrderStatuses must contain at least one OMG order status.");
            }
        }

        private static string Get(string key, string defaultValue)
        {
            var environmentValue = Environment.GetEnvironmentVariable("C9OMG_" + key);
            if (!string.IsNullOrWhiteSpace(environmentValue))
            {
                return environmentValue.Trim();
            }

            var configuredValue = ConfigurationManager.AppSettings[key];
            return configuredValue == null ? defaultValue : configuredValue.Trim();
        }

        private static bool GetBool(string key, bool defaultValue)
        {
            bool parsed;
            var raw = Get(key, defaultValue.ToString(CultureInfo.InvariantCulture));
            if (!bool.TryParse(raw, out parsed))
            {
                throw new ConfigurationErrorsException(key + " must be true or false.");
            }

            return parsed;
        }

        private static int GetInt(string key, int defaultValue)
        {
            int parsed;
            var raw = Get(key, defaultValue.ToString(CultureInfo.InvariantCulture));
            if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
            {
                throw new ConfigurationErrorsException(key + " must be an integer.");
            }

            return parsed;
        }

        private static decimal GetDecimal(string key, decimal defaultValue)
        {
            decimal parsed;
            var raw = Get(key, defaultValue.ToString(CultureInfo.InvariantCulture));
            if (!decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out parsed))
            {
                throw new ConfigurationErrorsException(key + " must be a number.");
            }

            return parsed;
        }

        private static string ResolvePath(string path)
        {
            return Path.IsPathRooted(path) ? path : Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path));
        }

        private static string EnsureTrailingSlash(string value)
        {
            return value.EndsWith("/", StringComparison.Ordinal) ? value : value + "/";
        }

        private static void Require(string value, string key)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ConfigurationErrorsException(key + " is required when the connector is enabled.");
            }
        }

        private static void RequireHttps(string value, string key)
        {
            Uri uri;
            if (!Uri.TryCreate(value, UriKind.Absolute, out uri) || uri.Scheme != Uri.UriSchemeHttps)
            {
                throw new ConfigurationErrorsException(key + " must be an absolute HTTPS URL.");
            }
        }
    }

    public sealed class AddressSettings
    {
        public string Contact { get; set; }
        public string Company { get; set; }
        public string Address1 { get; set; }
        public string Address2 { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string PostalCode { get; set; }
        public string Country { get; set; }
        public string Phone { get; set; }
    }

    public sealed class PackageSettings
    {
        public decimal Weight { get; set; }
        public decimal Length { get; set; }
        public decimal Width { get; set; }
        public decimal Height { get; set; }
        public string PackagingType { get; set; }
    }
}
