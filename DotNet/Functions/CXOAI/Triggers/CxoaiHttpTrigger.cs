using CXOAI.Functions.Models;
using CXOAI.Memory;
using InfraService.OpenTelemetryProvider;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Net;
using System.Text.Json;

namespace CXOAI.Functions.Triggers;

public class CxoaiHttpTrigger
{
    private readonly ILogger<CxoaiHttpTrigger> _logger;
    private readonly ILogger _debugLogger;
    private readonly IMetricsProvider? _metricsProvider;

    public CxoaiHttpTrigger(ILogger<CxoaiHttpTrigger> logger, ILoggerFactory loggerFactory, IMetricsProvider? metricsProvider = null)
    {
        _logger = logger;
        _debugLogger = loggerFactory.CreateLogger("CXOAI.Debug.Sensitive");
        _metricsProvider = metricsProvider;
    }

    /// <summary>
    /// POST /api/orchestrate
    /// Body: { "userId": "...", "prompt": "...", "userContext": { "entityName": "Walmart" } }
    /// Returns: { "instanceId": "...", "statusQueryGetUri": "..." }
    /// </summary>
    [Function("StartOrchestration")]
    public async Task<HttpResponseData> StartOrchestration(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "orchestrate")] HttpRequestData req,
        [DurableClient] DurableTaskClient client)
    {
        using var latency = _metricsProvider?.LatencyMeasureOperation(MetricNames.HttpTrigger,
            new KeyValuePair<string, object?>(MetricNames.TagOperationName, "StartOrchestration"));
        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            // Deserialize the full UI payload to capture Favorites, Recents, nested filters, etc.
            var uiPayload = await JsonSerializer.DeserializeAsync<UIPayload>(req.Body, options);

            if (uiPayload is null || string.IsNullOrWhiteSpace(uiPayload.UserId) || string.IsNullOrWhiteSpace(uiPayload.Prompt)
                || string.IsNullOrWhiteSpace(uiPayload.SessionId))
            {
                var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                await badReq.WriteStringAsync("Request body must include 'UserId', 'Prompt', and 'SessionId'.");
                return badReq;
            }

            // Decode URL-encoded prompt (UI may send "Give%20me%20a%20quick%20summary...")
            var decodedPrompt = Uri.UnescapeDataString(uiPayload.Prompt);

            // Map UIPayload → OrchestratorInput with entity resolution
            var input = new OrchestratorInput
            {
                UserId = uiPayload.UserId,
                Prompt = decodedPrompt,
                SessionId = uiPayload.SessionId,
                RequestId = uiPayload.RequestId,
                UserContext = uiPayload.ToUserContext(decodedPrompt)
            };

            // Extract Bearer token from Authorization header and forward through the pipeline
            if (req.Headers.TryGetValues("Authorization", out var authHeaders))
            {
                var authHeader = authHeaders.FirstOrDefault() ?? string.Empty;
                input.AccessToken = authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                    ? authHeader.Substring(7)
                    : authHeader;
            }

            using (_logger.BeginScope(new Dictionary<string, object>
            {
                ["SessionId"] = input.SessionId,
                ["RequestId"] = input.RequestId ?? "N/A",
                ["StepName"] = "StartOrchestration"
            }))
            {
                _logger.LogInformation("Starting orchestration, promptLength={PromptLength}", input.Prompt.Length);
                _debugLogger.LogDebug("Orchestration prompt: userId={UserId}, prompt={Prompt}", input.UserId, input.Prompt);

                if (input.UserContext is not null)
                {
                    var entityChanged = !string.Equals(uiPayload.EntityName, input.UserContext.EntityName, StringComparison.OrdinalIgnoreCase);
                    _logger.LogInformation(
                        "Context received: Entity={Entity}, Type={Type}, Filters={FilterCount}, FavCustomers={FavCust}, RecentCustomers={RecentCust}, OriginalEntity={OrigEntity}, EntityResolved={Resolved}",
                        input.UserContext.EntityName ?? "none",
                        input.UserContext.EntityType ?? "none",
                        input.UserContext.GlobalLevelFilters?.Count ?? 0,
                        uiPayload.FavoriteCustomers?.Count ?? 0,
                        uiPayload.RecentCustomers?.Count ?? 0,
                        uiPayload.EntityName ?? "none",
                        entityChanged);
                }

                var instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
                    "OrchestratorMain", input);

                _logger.LogInformation("Orchestration started: {InstanceId}", instanceId);
                latency?.SetState(ActivityStatusCode.Ok);
                _metricsProvider?.TrackCounterMetric(MetricNames.HttpTrigger, 1,
                    new KeyValuePair<string, object?>(MetricNames.TagOperationName, "StartOrchestration"));
                _metricsProvider?.TrackAvailabilityMetric(MetricNames.HttpTrigger, 1, null,
                    new KeyValuePair<string, object?>(MetricNames.TagOperationName, "StartOrchestration"));

                return await client.CreateCheckStatusResponseAsync(req, instanceId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "StartOrchestration failed");
            latency?.SetState(ActivityStatusCode.Error);
            _metricsProvider?.TrackCounterMetric(MetricNames.HttpTrigger, 1,
                new KeyValuePair<string, object?>(MetricNames.TagOperationName, "StartOrchestration"));
            _metricsProvider?.TrackAvailabilityMetric(MetricNames.HttpTrigger, 1, ex,
                new KeyValuePair<string, object?>(MetricNames.TagOperationName, "StartOrchestration"));
            throw;
        }
    }

    /// <summary>
    /// POST /api/instances/{instanceId}/tasks/{taskId}/skills/{skillName}/input
    /// Body: "user input text"
    /// Used by the UI to provide user input when a skill suspends.
    /// </summary>
    [Function("RaiseUserInput")]
    public async Task<HttpResponseData> RaiseUserInput(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "instances/{instanceId}/tasks/{taskId}/skills/{skillName}/input")] HttpRequestData req,
        [DurableClient] DurableTaskClient client,
        string instanceId,
        string taskId,
        string skillName)
    {
        using var latencyInput = _metricsProvider?.LatencyMeasureOperation(MetricNames.HttpTrigger,
            new KeyValuePair<string, object?>(MetricNames.TagOperationName, "RaiseUserInput"));
        try
        {
            var userInput = await new StreamReader(req.Body).ReadToEndAsync();

            if (string.IsNullOrWhiteSpace(userInput))
            {
                var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                await badReq.WriteStringAsync("Request body must contain the user's input text.");
                return badReq;
            }

            using (_logger.BeginScope(new Dictionary<string, object>
            {
                ["SessionId"] = instanceId,
                ["StepName"] = "RaiseUserInput"
            }))
            {
                var eventName = $"UserInput_{taskId}_{skillName}";
                _logger.LogInformation("Raising '{EventName}' for instance '{InstanceId}', task '{TaskId}'", eventName, instanceId, taskId);

                await client.RaiseEventAsync(instanceId, eventName, userInput);
                latencyInput?.SetState(ActivityStatusCode.Ok);
                _metricsProvider?.TrackCounterMetric(MetricNames.HttpTrigger, 1,
                    new KeyValuePair<string, object?>(MetricNames.TagOperationName, "RaiseUserInput"));
                _metricsProvider?.TrackAvailabilityMetric(MetricNames.HttpTrigger, 1, null,
                    new KeyValuePair<string, object?>(MetricNames.TagOperationName, "RaiseUserInput"));

                var response = req.CreateResponse(HttpStatusCode.Accepted);
                await response.WriteStringAsync($"Event '{eventName}' raised for instance {instanceId}");
                return response;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RaiseUserInput failed for instance '{InstanceId}'", instanceId);
            latencyInput?.SetState(ActivityStatusCode.Error);
            _metricsProvider?.TrackCounterMetric(MetricNames.HttpTrigger, 1,
                new KeyValuePair<string, object?>(MetricNames.TagOperationName, "RaiseUserInput"));
            _metricsProvider?.TrackAvailabilityMetric(MetricNames.HttpTrigger, 1, ex,
                new KeyValuePair<string, object?>(MetricNames.TagOperationName, "RaiseUserInput"));
            throw;
        }
    }
}
