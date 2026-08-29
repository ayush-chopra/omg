using System.Collections.Generic;
using Newtonsoft.Json;

namespace Cloud9.Omg.Connector.Models
{
    public sealed class Cloud9AuthenticateRequest
    {
        [JsonProperty("userId")]
        public string UserId { get; set; }

        [JsonProperty("token")]
        public string Token { get; set; }
    }

    public sealed class Cloud9AuthenticateResponse : Cloud9Result
    {
        [JsonProperty("authToken")]
        public string AuthToken { get; set; }

        [JsonProperty("locationId")]
        public int LocationId { get; set; }

        [JsonProperty("ttl")]
        public int Ttl { get; set; }
    }

    public class Cloud9Result
    {
        [JsonProperty("isSuccess")]
        public bool IsSuccess { get; set; }

        [JsonProperty("errorCode")]
        public int ErrorCode { get; set; }

        [JsonProperty("errorDesc")]
        public string ErrorDescription { get; set; }
    }

    public sealed class Cloud9UserInfo
    {
        [JsonProperty("authToken")]
        public string AuthToken { get; set; }

        [JsonProperty("userId")]
        public string UserId { get; set; }

        [JsonProperty("locationId")]
        public int LocationId { get; set; }
    }

    public sealed class AddShipJobRequest
    {
        [JsonProperty("ui")]
        public Cloud9UserInfo UserInfo { get; set; }

        [JsonProperty("job")]
        public Cloud9ShipJob Job { get; set; }
    }

    public sealed class Cloud9ShipJob
    {
        [JsonProperty("orderNumber")]
        public string OrderNumber { get; set; }

        [JsonProperty("sourceSysId")]
        public string SourceSystemId { get; set; }

        [JsonProperty("action")]
        public string Action { get; set; }

        [JsonProperty("shopCode")]
        public string ShopCode { get; set; }

        [JsonProperty("carrierScac")]
        public string CarrierScac { get; set; }

        [JsonProperty("carrierServiceCode")]
        public string CarrierServiceCode { get; set; }

        [JsonProperty("paymentType")]
        public string PaymentType { get; set; }

        [JsonProperty("paymentAccount")]
        public string PaymentAccount { get; set; }

        [JsonProperty("paymentAcctPostalCode")]
        public string PaymentAccountPostalCode { get; set; }

        [JsonProperty("fromContact")]
        public string FromContact { get; set; }

        [JsonProperty("fromCompany")]
        public string FromCompany { get; set; }

        [JsonProperty("fromAddress")]
        public string FromAddress { get; set; }

        [JsonProperty("fromAddress2")]
        public string FromAddress2 { get; set; }

        [JsonProperty("fromCity")]
        public string FromCity { get; set; }

        [JsonProperty("fromState")]
        public string FromState { get; set; }

        [JsonProperty("fromPostalCode")]
        public string FromPostalCode { get; set; }

        [JsonProperty("fromCountry")]
        public string FromCountry { get; set; }

        [JsonProperty("fromPhone")]
        public string FromPhone { get; set; }

        [JsonProperty("toContact")]
        public string ToContact { get; set; }

        [JsonProperty("toCompany")]
        public string ToCompany { get; set; }

        [JsonProperty("toAddress")]
        public string ToAddress { get; set; }

        [JsonProperty("toAddress2")]
        public string ToAddress2 { get; set; }

        [JsonProperty("toCity")]
        public string ToCity { get; set; }

        [JsonProperty("toState")]
        public string ToState { get; set; }

        [JsonProperty("toPostalCode")]
        public string ToPostalCode { get; set; }

        [JsonProperty("toCountry")]
        public string ToCountry { get; set; }

        [JsonProperty("toPhone")]
        public string ToPhone { get; set; }

        [JsonProperty("shipNotifyEmail")]
        public string ShipNotifyEmail { get; set; }

        [JsonProperty("jobPkgs")]
        public List<Cloud9JobPackage> Packages { get; set; }
    }

    public sealed class Cloud9JobPackage
    {
        [JsonProperty("length")]
        public string Length { get; set; }

        [JsonProperty("width")]
        public string Width { get; set; }

        [JsonProperty("height")]
        public string Height { get; set; }

        [JsonProperty("weight")]
        public string Weight { get; set; }

        [JsonProperty("packagingType")]
        public string PackagingType { get; set; }

        [JsonProperty("declaredValue")]
        public string DeclaredValue { get; set; }

        [JsonProperty("dcisType")]
        public string DcisType { get; set; }

        [JsonProperty("extraHandling")]
        public string ExtraHandling { get; set; }

        [JsonProperty("nonStdContainer")]
        public string NonStandardContainer { get; set; }

        [JsonProperty("ref2")]
        public string Reference2 { get; set; }

        [JsonProperty("ref3")]
        public string Reference3 { get; set; }
    }

    public sealed class Cloud9ShipJobCallback
    {
        [JsonProperty("shipDate")]
        public string ShipDate { get; set; }

        [JsonProperty("orderNumber")]
        public string OrderNumber { get; set; }

        [JsonProperty("carrierScac")]
        public string CarrierScac { get; set; }

        [JsonProperty("serviceType")]
        public string ServiceType { get; set; }

        [JsonProperty("cost")]
        public string Cost { get; set; }

        [JsonProperty("isReturn")]
        public string IsReturn { get; set; }

        [JsonProperty("voidDate")]
        public string VoidDate { get; set; }

        [JsonProperty("pkgs")]
        public List<Cloud9CallbackPackage> Packages { get; set; }
    }

    public sealed class Cloud9CallbackPackage
    {
        [JsonProperty("trackingNumber")]
        public string TrackingNumber { get; set; }
    }
}
