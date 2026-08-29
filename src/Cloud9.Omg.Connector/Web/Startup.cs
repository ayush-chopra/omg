using System.Web.Http;
using Newtonsoft.Json.Serialization;
using Owin;

namespace Cloud9.Omg.Connector.Web
{
    public sealed class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            var configuration = new HttpConfiguration();
            configuration.MapHttpAttributeRoutes();
            configuration.Formatters.Remove(configuration.Formatters.XmlFormatter);
            configuration.Formatters.JsonFormatter.SerializerSettings.ContractResolver =
                new CamelCasePropertyNamesContractResolver();
            configuration.EnsureInitialized();
            app.UseWebApi(configuration);
        }
    }
}
