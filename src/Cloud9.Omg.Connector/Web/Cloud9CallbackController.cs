using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using Cloud9.Omg.Connector.Models;

namespace Cloud9.Omg.Connector.Web
{
    [RoutePrefix("api/cloud9")]
    public sealed class Cloud9CallbackController : ApiController
    {
        private const long MaximumRequestBytes = 2 * 1024 * 1024;

        [HttpPost]
        [Route("ship-job-callback")]
        public async Task<IHttpActionResult> Post(
            Cloud9ShipJobCallback callback,
            CancellationToken cancellationToken)
        {
            var root = CompositionRoot.Current;
            if (root == null || !root.Settings.Enabled)
            {
                return Content(HttpStatusCode.ServiceUnavailable, new { error = "Connector is disabled." });
            }

            if (!IsAuthorized(root.Settings.Cloud9CallbackToken))
            {
                return Unauthorized();
            }

            if (Request.Content.Headers.ContentLength.HasValue &&
                Request.Content.Headers.ContentLength.Value > MaximumRequestBytes)
            {
                return Content(HttpStatusCode.RequestEntityTooLarge, new { error = "Callback exceeds 2 MB." });
            }

            if (callback == null)
            {
                return BadRequest("A JSON callback body is required.");
            }

            try
            {
                var result = await root.ShipmentCallbackService.ProcessAsync(callback, cancellationToken)
                    .ConfigureAwait(false);
                if (string.Equals(result.Status, "manual_review", StringComparison.OrdinalIgnoreCase))
                {
                    return Content(HttpStatusCode.Accepted, result);
                }

                return Ok(result);
            }
            catch (InvalidOperationException exception)
            {
                root.Log.Error("Cloud9 callback could not be processed.", exception);
                return Content(HttpStatusCode.Conflict, new { error = exception.Message });
            }
            catch (Exception exception)
            {
                root.Log.Error("Unexpected Cloud9 callback failure.", exception);
                return InternalServerError();
            }
        }

        private bool IsAuthorized(string expectedToken)
        {
            var suppliedToken = GetHeader("X-Cloud9-Callback-Token");
            if (string.IsNullOrWhiteSpace(suppliedToken) && Request.Headers.Authorization != null &&
                string.Equals(Request.Headers.Authorization.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase))
            {
                suppliedToken = Request.Headers.Authorization.Parameter;
            }

            if (string.IsNullOrWhiteSpace(suppliedToken))
            {
                suppliedToken = Request.GetQueryNameValuePairs()
                    .Where(pair => string.Equals(pair.Key, "token", StringComparison.OrdinalIgnoreCase))
                    .Select(pair => pair.Value)
                    .FirstOrDefault();
            }

            return FixedTimeEquals(expectedToken, suppliedToken);
        }

        private string GetHeader(string name)
        {
            IEnumerable<string> values;
            return Request.Headers.TryGetValues(name, out values) ? values.FirstOrDefault() : null;
        }

        private static bool FixedTimeEquals(string expected, string supplied)
        {
            if (string.IsNullOrEmpty(expected) || string.IsNullOrEmpty(supplied))
            {
                return false;
            }

            byte[] expectedHash;
            byte[] suppliedHash;
            using (var sha256 = SHA256.Create())
            {
                expectedHash = sha256.ComputeHash(Encoding.UTF8.GetBytes(expected));
                suppliedHash = sha256.ComputeHash(Encoding.UTF8.GetBytes(supplied));
            }

            var difference = 0;
            for (var index = 0; index < expectedHash.Length; index++)
            {
                difference |= expectedHash[index] ^ suppliedHash[index];
            }

            return difference == 0;
        }
    }
}
