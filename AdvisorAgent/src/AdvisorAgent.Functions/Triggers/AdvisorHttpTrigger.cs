using System.Net;
using System.Text.Json;
using AdvisorAgent.Core.Models;
using AdvisorAgent.Functions.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace AdvisorAgent.Functions.Triggers;

/// <summary>
/// HTTP entry points and SignalR negotiate for the Advisor Agent.
/// </summary>
public sealed class AdvisorHttpTrigger
{
    private readonly ILogger<AdvisorHttpTrigger> _logger;

    public AdvisorHttpTrigger(ILogger<AdvisorHttpTrigger> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// SignalR negotiate endpoint – returns connection info for the "advisor" hub.
    /// POST /api/negotiate
    /// </summary>
    [Function("negotiate")]
    public HttpResponseData Negotiate(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "negotiate")] HttpRequestData req,
        [SignalRConnectionInfoInput(HubName = "advisor")] string connectionInfo)
    {
        _logger.LogInformation("[SignalR] Negotiate called");
        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json");
        response.WriteString(connectionInfo);
        return response;
    }

    /// <summary>
    /// Main entry point – starts the advisor orchestration.
    /// POST /api/advisor/orchestrate
    /// Body: { "userId": "...", "prompt": "...", "sessionId": "..." }
    /// </summary>
    [Function("AdvisorOrchestrate")]
    public async Task<HttpResponseData> StartOrchestration(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "advisor/orchestrate")] HttpRequestData req,
        [DurableClient] DurableTaskClient durableClient)
    {
        var input = await req.ReadFromJsonAsync<AdvisorOrchestratorInput>();
        if (input is null || string.IsNullOrWhiteSpace(input.Prompt))
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteAsJsonAsync(new { error = "A prompt is required." });
            return bad;
        }

        input.SessionId ??= Guid.NewGuid().ToString("N");

        // Extract ARM access token from Authorization header if not in body
        if (string.IsNullOrEmpty(input.AccessToken))
        {
            if (req.Headers.TryGetValues("Authorization", out var authValues))
            {
                var authHeader = authValues.FirstOrDefault();
                if (authHeader?.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) == true)
                {
                    input.AccessToken = authHeader["Bearer ".Length..];
                }
            }
        }

        _logger.LogInformation("[HTTP] POST /api/advisor/orchestrate — SessionId: {SessionId}, UserId: {UserId}, HasToken: {HasToken}, Prompt: {Prompt}",
            input.SessionId, input.UserId, !string.IsNullOrEmpty(input.AccessToken),
            input.Prompt?.Length > 150 ? input.Prompt[..150] + "…" : input.Prompt);

        var instanceId = await durableClient.ScheduleNewOrchestrationInstanceAsync(
            "AdvisorOrchestratorMain", input);

        _logger.LogInformation("[HTTP] Orchestration scheduled — InstanceId: {InstanceId}, SessionId: {SessionId}", instanceId, input.SessionId);

        var response = req.CreateResponse(HttpStatusCode.Accepted);
        await response.WriteAsJsonAsync(new
        {
            instanceId,
            sessionId = input.SessionId,
            statusQueryGetUri = $"/api/advisor/status/{instanceId}"
        });

        return response;
    }

    /// <summary>
    /// Poll orchestration status.
    /// GET /api/advisor/status/{instanceId}
    /// </summary>
    [Function("AdvisorStatus")]
    public async Task<HttpResponseData> GetStatus(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "advisor/status/{instanceId}")] HttpRequestData req,
        [DurableClient] DurableTaskClient durableClient,
        string instanceId)
    {
        var metadata = await durableClient.GetInstanceAsync(instanceId, getInputsAndOutputs: true);

        if (metadata is null)
        {
            var notFound = req.CreateResponse(HttpStatusCode.NotFound);
            await notFound.WriteAsJsonAsync(new { error = $"Instance {instanceId} not found." });
            return notFound;
        }

        _logger.LogInformation("[HTTP] GET /api/advisor/status/{InstanceId} — Status: {Status}", instanceId, metadata.RuntimeStatus);

        // Parse the serialized output so it's returned as a proper JSON object
        object? output = null;
        if (metadata.SerializedOutput is not null)
        {
            try
            {
                output = JsonSerializer.Deserialize<AdvisorAgentResponse>(metadata.SerializedOutput);
            }
            catch (JsonException)
            {
                output = metadata.SerializedOutput;
            }
        }

        // Parse custom status for real-time progress
        object? customStatus = null;
        if (metadata.SerializedCustomStatus is not null)
        {
            try
            {
                customStatus = JsonSerializer.Deserialize<OrchestrationProgress>(metadata.SerializedCustomStatus);
            }
            catch (JsonException)
            {
                customStatus = metadata.SerializedCustomStatus;
            }
        }

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new
        {
            instanceId = metadata.InstanceId,
            runtimeStatus = metadata.RuntimeStatus.ToString(),
            createdAt = metadata.CreatedAt,
            lastUpdatedAt = metadata.LastUpdatedAt,
            customStatus,
            output
        });

        return response;
    }

    /// <summary>
    /// Health check endpoint.
    /// GET /api/advisor/health
    /// </summary>
    [Function("AdvisorHealth")]
    public async Task<HttpResponseData> HealthCheck(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "advisor/health")] HttpRequestData req)
    {
        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteStringAsync("Healthy");
        return response;
    }
}
