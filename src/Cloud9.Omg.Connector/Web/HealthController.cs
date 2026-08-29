using System.Web.Http;

namespace Cloud9.Omg.Connector.Web
{
    [RoutePrefix("health")]
    public sealed class HealthController : ApiController
    {
        [HttpGet]
        [Route("")]
        public IHttpActionResult Get()
        {
            var root = CompositionRoot.Current;
            return Ok(new
            {
                status = "ok",
                enabled = root != null && root.Settings.Enabled,
                framework = ".NET Framework 4.8"
            });
        }
    }
}
