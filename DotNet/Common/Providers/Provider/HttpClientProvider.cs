using InfraService.OpenTelemetryProvider;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Provider.Interfaces;
using Provider.Model;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;

namespace Provider
{
    public class HttpClientProvider : IHttpClientProvider
    {
        private readonly ITokenProvider _tokenProvider;
        private readonly HttpClient _httpClient;
        private readonly ILogger<HttpClientProvider> _logger;
        private readonly IMetricsProvider _metricsProvider;

        public HttpClientProvider(
            ITokenProvider tokenProvider,
            IHttpClientFactory httpClientFactory,
            ILogger<HttpClientProvider> logger,
            IMetricsProvider metricsProvider)
        {
            _tokenProvider = tokenProvider;
            _httpClient = httpClientFactory.CreateClient(nameof(HttpClientProvider));
            _logger = logger;
            _metricsProvider = metricsProvider;
        }

        /// <inheritdoc />
        public async Task<JObject> PostWithOboAuthAsync(
            string url,
            TokenAcquisitionConfig tokenConfig,
            string userToken,
            object payload,
            string resourceName = "")
        {
            var latencyMetric = $"{resourceName}LatencyMetric";
            var availabilityMetric = $"{resourceName}AvailabilityMetric";
            var failureMetric = $"{resourceName}FailureMetric";

            _logger.LogInformation("PostWithOboAuthAsync | Resource: {Resource} | URL: {Url}", resourceName, url);

            var stopwatch = Stopwatch.StartNew();

            try
            {
                var bearerToken = await _tokenProvider.GetJwtTokenOnBehalfOfUserWithCertificateAssertion(
                    tokenConfig, userToken);

                var requestContent = JsonConvert.SerializeObject(payload, new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore
                });

                using var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
                request.Content = new StringContent(requestContent, Encoding.UTF8, "application/json");

                _logger.LogInformation("PostWithOboAuthAsync | Resource: {Resource} | Payload: {Payload}", resourceName, requestContent);

                using var response = await _httpClient.SendAsync(request);

                stopwatch.Stop();

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("PostWithOboAuthAsync | Resource: {Resource} | {StatusCode} | {Error}",
                        resourceName, response.StatusCode, errorContent);

                    _metricsProvider.RecordLatencySLI(latencyMetric, stopwatch.ElapsedMilliseconds);
                    _metricsProvider.TrackAvailabilityMetric(failureMetric, 1, null);

                    throw new HttpRequestException(
                        $"API returned {response.StatusCode}: {errorContent}");
                }

                var result = await response.Content.ReadAsStringAsync();

                _logger.LogInformation("PostWithOboAuthAsync | Resource: {Resource} | Success ({StatusCode}) | {ElapsedMs}ms",
                    resourceName, response.StatusCode, stopwatch.ElapsedMilliseconds);

                _metricsProvider.RecordLatencySLI(latencyMetric, stopwatch.ElapsedMilliseconds);
                _metricsProvider.TrackAvailabilityMetric(availabilityMetric, 1, null);

                // Auto-detect response format: array → wrap in JObject, object → return as-is
                var trimmedResult = result.TrimStart();
                JObject parsedResult;
                if (trimmedResult.StartsWith('['))
                {
                    var jsonArray = JArray.Parse(result);
                    parsedResult = new JObject { ["items"] = jsonArray };
                }
                else
                {
                    parsedResult = JObject.Parse(result);
                }
                return parsedResult;
            }
            catch (HttpRequestException)
            {
                throw; // already logged and tracked above
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "PostWithOboAuthAsync | Resource: {Resource} | Unexpected error", resourceName);

                _metricsProvider.RecordLatencySLI(latencyMetric, stopwatch.ElapsedMilliseconds);
                _metricsProvider.TrackAvailabilityMetric(failureMetric, 1, ex);

                throw;
            }
        }
    }
}
