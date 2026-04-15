using Newtonsoft.Json.Linq;
using Provider.Model;

namespace Provider.Interfaces
{
    public interface IHttpClientProvider
    {
        /// <summary>
        /// Sends an authenticated POST request using On-Behalf-Of (OBO) token acquisition.
        /// Serializes the payload as JSON, attaches a Bearer token, and returns the response as a JObject.
        /// Arrays are auto-wrapped as { "items": [...] }.
        /// </summary>
        /// <param name="url">The full API URL to POST to.</param>
        /// <param name="tokenConfig">Token acquisition configuration (client ID, tenant, scopes, certificate).</param>
        /// <param name="userToken">The caller's user token for OBO authentication.</param>
        /// <param name="payload">The request payload object (serialized as JSON).</param>
        /// <param name="resourceName">Logical name for logging and telemetry metrics (e.g., caller method name).</param>
        /// <returns>The parsed JSON response as a JObject.</returns>
        Task<JObject> PostWithOboAuthAsync(
            string url,
            TokenAcquisitionConfig tokenConfig,
            string userToken,
            object payload,
            string resourceName = "");
    }
}
