using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Azure.Core;
using CXOAI.ConfigurationStore;
using CXOAI.SkillFramework;
using CXOAI.StatusNotifier;
using CXOAI.Tools.Configuration;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CXOAI.Tools;

/// <summary>
/// Parsed data from an external agent's ReceiveUserInputRequest event.
/// </summary>
internal class ExternalUserInputRequest
{
    public required string SkillName { get; init; }
    public required string TaskId { get; init; }
    public required string Prompt { get; init; }
    public required string InstanceId { get; init; }
    public required string SessionId { get; init; }
    public JsonElement SkillResult { get; init; }
}

/// <summary>
/// Extended SignalR listener with two TaskCompletionSources:
/// <list type="bullet">
///   <item><see cref="CompletionTcs"/> — resolves when Agent B sends ReceiveCompleted</item>
///   <item><see cref="UserInputTcs"/> — resolves when Agent B sends ReceiveUserInputRequest</item>
/// </list>
/// The tool awaits <c>Task.WhenAny</c> to react to whichever comes first.
/// </summary>
internal sealed class DualTcsSignalRListener : IAsyncDisposable
{
    public required HubConnection Connection { get; init; }
    public required TaskCompletionSource<string> CompletionTcs { get; init; }
    public required TaskCompletionSource<ExternalUserInputRequest> UserInputTcs { get; init; }

    public async ValueTask DisposeAsync()
    {
        if (Connection.State != HubConnectionState.Disconnected)
        {
            try { await Connection.StopAsync(); }
            catch { /* best-effort cleanup */ }
        }
        await Connection.DisposeAsync();
    }
}

/// <summary>
/// Generic tool for delegating tasks to external CXOAI agent instances.
/// Extends <see cref="ToolBase"/> for SignalR notification relay.
///
/// <para><b>Approach 6 — Cooperative Input:</b> When the external agent requests user
/// input, this tool <em>disconnects</em> from the external agent's SignalR, stores
/// continuation state in <see cref="CXOAgentResponse.Payload"/>, and returns
/// <c>NeedsInputForUser = true</c>. The orchestrator's existing user-input loop
/// suspends at zero cost via <c>WaitForExternalEventAsync</c>, collects input from
/// the UI, and re-invokes this tool. On re-invocation, the tool reads the continuation
/// state, reconnects to the external agent's SignalR, and forwards the user's input.</para>
///
/// <para>This is safe because both orchestrators are suspended during the disconnect
/// window — Agent B is at <c>WaitForExternalEventAsync</c>, so zero events are missed.</para>
/// </summary>
public class ExternalAgentDelegator : ToolBase
{
    private readonly ExternalAgentConfig _config;
    private readonly HttpClient _httpClient;
    private readonly IUserAuthContext _authContext;
    private readonly TokenCredential _credential;
    private readonly ILogger<ExternalAgentDelegator> _logger;

    // Circuit breaker state
    private int _consecutiveFailures;
    private DateTime? _circuitOpenedAt;

    private static readonly TimeSpan InitialRetryDelay = TimeSpan.FromSeconds(2);

    private const string ConfigComponentName = "ToolConfiguration";
    private const string ConfigName = "ExternalAgentDelegator-EnvironmentSettings";

    public ExternalAgentDelegator(
        HttpClient httpClient,
        IUserAuthContext authContext,
        IToolStatusNotifier notifier,
        TokenCredential credential,
        ITreeConfigurationStoreProvider configStoreProvider,
        ILogger<ExternalAgentDelegator> logger) : base(notifier)
    {
        _httpClient = httpClient;
        _authContext = authContext;
        _credential = credential;
        _logger = logger;

        // Load config from the configuration store (ToolConfiguration / ExternalAgentDelegator-EnvironmentSettings)
        _config = LoadConfigFromStore(configStoreProvider);
    }

    private ExternalAgentConfig LoadConfigFromStore(ITreeConfigurationStoreProvider storeProvider)
    {
        try
        {
            var configurations = storeProvider.GetConfigurationsWithNames(
                ConfigComponentName,
                new List<string> { ConfigName },
                false).GetAwaiter().GetResult();

            var configEntry = configurations?.FirstOrDefault();
            if (configEntry?.Configuration != null)
            {
                var json = JObject.Parse(configEntry.Configuration);

                var config = new ExternalAgentConfig
                {
                    AgentId = json[nameof(ExternalAgentConfig.AgentId)]?.ToString() ?? "external-agent",
                    BaseUrl = (json[nameof(ExternalAgentConfig.BaseUrl)]?.ToString() ?? "").TrimEnd('/'),
                    OrchestrateEndpoint = json[nameof(ExternalAgentConfig.OrchestrateEndpoint)]?.ToString() ?? "/api/orchestrate",
                    NegotiateEndpoint = json[nameof(ExternalAgentConfig.NegotiateEndpoint)]?.ToString() ?? "/api/negotiate",
                    TimeoutSeconds = json[nameof(ExternalAgentConfig.TimeoutSeconds)]?.Value<int>() ?? 300,
                    MaxRetries = json[nameof(ExternalAgentConfig.MaxRetries)]?.Value<int>() ?? 3,
                    CircuitBreakerThreshold = json[nameof(ExternalAgentConfig.CircuitBreakerThreshold)]?.Value<int>() ?? 3,
                    CircuitBreakerRecoverySeconds = json[nameof(ExternalAgentConfig.CircuitBreakerRecoverySeconds)]?.Value<int>() ?? 60,
                    ManagedIdentityScope = json[nameof(ExternalAgentConfig.ManagedIdentityScope)]?.ToString(),
                    Description = json[nameof(ExternalAgentConfig.Description)]?.ToString()
                };

                _logger.LogInformation(
                    "ExternalAgentDelegator config loaded from store: AgentId={AgentId}, BaseUrl={BaseUrl}",
                    config.AgentId, config.BaseUrl);

                return config;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load ExternalAgentDelegator config from store, using defaults");
        }

        _logger.LogWarning("ExternalAgentDelegator-EnvironmentSettings not found in config store");
        return new ExternalAgentConfig
        {
            AgentId = "external-agent",
            BaseUrl = "",
            OrchestrateEndpoint = "/api/orchestrate"
        };
    }

    /// <summary>
    /// Delegates a task to the external agent and returns its result.
    /// All tool notifications from the external agent are relayed to the UI in real-time.
    ///
    /// <para><b>Mode 1 (fresh):</b> <c>Payload</c> is null → connect, POST /orchestrate, listen.</para>
    /// <para><b>Mode 2 (continuation):</b> <c>Payload</c> is set + <paramref name="userResponse"/> provided
    /// → reconnect, forward user input, listen.</para>
    /// </summary>
    [Description("Sends a message to an external agent and returns the response. " +
		// "Delegates a task to an external specialized agent for processing. " +
        // "Returns the agent's response. If the external agent needs user input, " +
        "Returns the agent's response including any data, confirmation prompts, or errors.")]
    public async Task<CXOAgentResponse> DelegateTaskAsync(
        [Description("The user's message to send to the external agent")]
        // [Description("The user's message task description/prompt to send to the external agent")]
        string prompt,
        [Description("The current session ID for SignalR event routing")]
        string sessionId,
        [Description("User's response to a previous input request (null for initial delegation)")]
        string? userResponse = null)
    {
        // ── Detect mode ──
        var continuation = GetContinuationIfMatch();
        var isContinuation = continuation != null && !string.IsNullOrEmpty(userResponse);

		// TODO: Fix continuation if it is not working
        if (continuation != null && !string.IsNullOrWhiteSpace(continuation.Value<string>("agentSessionId")))
        {
            sessionId = continuation.Value<string>("agentSessionId")!;
        }
        else if (string.IsNullOrWhiteSpace(sessionId))
        {
            sessionId = Guid.NewGuid().ToString("N")[..12];
        }

        // ── Circuit breaker check ──
        if (IsCircuitOpen())
        {
            _logger.LogWarning("Circuit breaker OPEN for agent {AgentId}", _config.AgentId);
            return ErrorResponse($"External agent '{_config.AgentId}' is temporarily unavailable (circuit breaker open).");
        }

        DualTcsSignalRListener? listener = null;
        var sw = Stopwatch.StartNew();
        try
        {
            var agentBaseUrl = isContinuation
                ? continuation!.Value<string>("agentBaseUrl")!
                : _config.BaseUrl;

            // ── Step 1: Pre-connect to external agent's SignalR ──
            //   Mode 1: Agent B hasn't started yet — safe
            //   Mode 2: Agent B is suspended at WaitForExternalEvent — no events to miss
            listener = await SetupDualTcsListenerAsync(sessionId, sw);

            if (isContinuation)
            {
                // ── MODE 2: Forward user input to Agent B, then listen ──
                await NotifyAsync($"Forwarding response to {_config.AgentId}...");

                await ForwardUserInputToAgentAsync(
                    agentBaseUrl,
                    continuation!.Value<string>("agentInstanceId")!,
                    continuation.Value<string>("pendingTaskId")!,
                    continuation.Value<string>("pendingSkillName")!,
                    userResponse!);
            }
            else
            {
                // ── MODE 1: Fresh delegation ──
                await NotifyAsync($"Delegating to {_config.AgentId}...");
                await TriggerOrchestrationAsync(agentBaseUrl, prompt, sessionId);
            }

            // ── Step 2: Await completion OR user input request ──
            using var timeoutCts = new CancellationTokenSource(
                TimeSpan.FromSeconds(_config.TimeoutSeconds));
            timeoutCts.Token.Register(() =>
            {
                listener.CompletionTcs.TrySetCanceled();
                listener.UserInputTcs.TrySetCanceled();
            });

            var winner = await Task.WhenAny(
                listener.CompletionTcs.Task,
                listener.UserInputTcs.Task);

            if (winner == listener.CompletionTcs.Task)
            {
                // ── Agent B completed ──
                var resultJson = await listener.CompletionTcs.Task;
                RecordSuccess();
                await NotifyAsync($"✓ {_config.AgentId} completed ({sw.Elapsed.TotalSeconds:F1}s)");
                return ParseAgentResult(resultJson);
            }
            else
            {
                // ── Agent B needs user input ──
                var inputReq = await listener.UserInputTcs.Task;

                // Disconnect from Agent B's SignalR — safe because Agent B is now
                // suspended at WaitForExternalEventAsync (no events will be published)
                await listener.DisposeAsync();
                listener = null; // prevent double-dispose in finally

                var round = isContinuation
                    ? continuation!.Value<int>("inputRound") + 1
                    : 1;

                _logger.LogInformation(
                    "External agent {AgentId} needs user input (round {Round}): {Prompt}",
                    _config.AgentId, round, inputReq.Prompt);
                await NotifyAsync($"{_config.AgentId} needs your input (round {round})");

                // Build continuation token for the orchestrator to round-trip
                var payload = new JObject
                {
                    ["delegationType"] = "externalAgent",
                    ["agentId"] = _config.AgentId,
                    ["agentBaseUrl"] = agentBaseUrl,
                    ["agentInstanceId"] = inputReq.InstanceId,
                    ["agentSessionId"] = sessionId,
                    ["pendingTaskId"] = inputReq.TaskId,
                    ["pendingSkillName"] = inputReq.SkillName,
                    ["inputRound"] = round
                };

                // Emit payload via side-channel so ExecuteSkillAsync can attach it
                // to the result after the LLM loop finishes.
                EmitPayload(payload);

                // Extract UI fields from the external agent's skill result
                // so the orchestrator can forward them to the UI alongside the question.
                CXOAgentResponse? agentBResult = null;
                if (inputReq.SkillResult.ValueKind == JsonValueKind.Object)
                {
                    try
                    {
                        agentBResult = JsonConvert.DeserializeObject<CXOAgentResponse>(
                            inputReq.SkillResult.GetRawText());
                    }
                    catch { /* best-effort - fields default to false/empty */ }
                }

                var needsInputResponse = new CXOAgentResponse
                {
                    IsSuccess = true,
                    NeedsInputForUser = true,
                    Response = inputReq.Prompt,
                    Payload = payload,
                    IsUIComponent = agentBResult?.IsUIComponent ?? false,
                    UIComponent = agentBResult?.UIComponent ?? string.Empty,
                    IsReport = agentBResult?.IsReport ?? false
                };
                return needsInputResponse;
                //throw new ToolParameterException(JsonConvert.SerializeObject(needsInputResponse));
            }
        }
        catch (OperationCanceledException)
        {
            RecordFailure();
            _logger.LogWarning("Delegation to {AgentId} timed out after {Seconds}s",
                _config.AgentId, sw.Elapsed.TotalSeconds);
            return ErrorResponse($"External agent '{_config.AgentId}' timed out after {_config.TimeoutSeconds}s.");
        }
        catch (Exception ex) when (ex is not ToolParameterException)
        {
            RecordFailure();
            _logger.LogError(ex, "Delegation to {AgentId} failed after {Seconds}s",
                _config.AgentId, sw.Elapsed.TotalSeconds);
            return ErrorResponse($"External agent '{_config.AgentId}' failed: {ex.Message}");
        }
        finally
        {
            if (listener != null) await listener.DisposeAsync();
        }
    }

    // ── SignalR Listener Setup ──────────────────────────────────────────

    /// <summary>
    /// Negotiates with the external agent's SignalR, builds a HubConnection with
    /// dual TCS (completion + user input), registers event handlers, and connects.
    /// </summary>
    private async Task<DualTcsSignalRListener> SetupDualTcsListenerAsync(
        string sessionId, Stopwatch sw)
    {
        // 1. Negotiate
        var negotiateUrl = $"{_config.BaseUrl}{_config.NegotiateEndpoint}?sessionId={Uri.EscapeDataString(sessionId)}";
        var negotiateResponse = await PostWithAuthAsync(negotiateUrl);
        negotiateResponse.EnsureSuccessStatusCode();

        var negotiateBody = await negotiateResponse.Content.ReadAsStringAsync();
        var negotiateJson = JObject.Parse(negotiateBody);
        var hubUrl = negotiateJson["url"]?.ToString()
            ?? throw new InvalidOperationException("External agent negotiate did not return a hub URL.");
        var accessToken = negotiateJson["accessToken"]?.ToString();

        _logger.LogInformation("Negotiated SignalR with {AgentId} in {Ms}ms",
            _config.AgentId, sw.ElapsedMilliseconds);

        // 2. Build connection
        var connection = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                options.AccessTokenProvider = () => Task.FromResult(accessToken);
            })
            .WithAutomaticReconnect()
            .Build();

        // 3. Create dual TCS
        var completionTcs = new TaskCompletionSource<string>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var userInputTcs = new TaskCompletionSource<ExternalUserInputRequest>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        // 4. Register handlers

        // ReceiveCompleted → resolve CompletionTcs
        connection.On<JsonElement>("ReceiveCompleted", payload =>
        {
            var resultJson = payload.TryGetProperty("result", out var resultEl)
                ? resultEl.GetRawText()
                : payload.GetRawText();

            _logger.LogInformation("Received ReceiveCompleted from {AgentId} | Elapsed: {Ms}ms",
                _config.AgentId, sw.ElapsedMilliseconds);

            completionTcs.TrySetResult(resultJson);
        });

        // ReceiveUserInputRequest → resolve UserInputTcs (do NOT forward to UI here!)
        connection.On<string, string, string, string, string, JsonElement>(
            "ReceiveUserInputRequest",
            (skillName, taskId, prompt, agentSessionId, instanceId, skillResult) =>
            {
                _logger.LogInformation(
                    "Received ReceiveUserInputRequest from {AgentId} | Skill: {Skill} | Task: {Task}",
                    _config.AgentId, skillName, taskId);

                userInputTcs.TrySetResult(new ExternalUserInputRequest
                {
                    SkillName = skillName,
                    TaskId = taskId,
                    Prompt = prompt,
                    InstanceId = instanceId,
                    SessionId = agentSessionId,
                    SkillResult = skillResult
                });
            });

        // ReceiveStatus → convert to ToolProgress and relay to UI
        connection.On<JsonElement>("ReceiveStatus", async statusPayload =>
        {
            var message = ExtractStatusMessage(statusPayload);
            await NotifyAsync($"{_config.AgentId}: {message}");
        });

        // ReceiveToolProgress → relay with agent prefix to UI
        connection.On<JsonElement>("ReceiveToolProgress", async progressPayload =>
        {
            var toolName = progressPayload.TryGetProperty("toolName", out var tn)
                ? tn.GetString() ?? "unknown" : "unknown";
            var message = progressPayload.TryGetProperty("message", out var msg)
                ? msg.GetString() ?? "" : "";

            await ForwardEventAsync("ReceiveToolProgress", JToken.Parse(progressPayload.GetRawText()));
        });

        // Connection closed unexpectedly
        connection.Closed += ex =>
        {
            if (!completionTcs.Task.IsCompleted && !userInputTcs.Task.IsCompleted)
            {
                _logger.LogWarning(ex, "SignalR connection closed for {AgentId} before completion", _config.AgentId);
                completionTcs.TrySetException(
                    new InvalidOperationException("SignalR connection closed before agent completed.", ex));
            }
            return Task.CompletedTask;
        };

        // 5. Connect
        await connection.StartAsync();
        _logger.LogInformation("Connected to {AgentId} SignalR for session {SessionId} in {Ms}ms",
            _config.AgentId, sessionId, sw.ElapsedMilliseconds);

        return new DualTcsSignalRListener
        {
            Connection = connection,
            CompletionTcs = completionTcs,
            UserInputTcs = userInputTcs
        };
    }

    // ── HTTP Methods ────────────────────────────────────────────────────

    /// <summary>POST to the external agent's orchestrate endpoint (fire-and-forget).</summary>
    private async Task TriggerOrchestrationAsync(string agentBaseUrl, string prompt, string sessionId)
    {
        var url = $"{agentBaseUrl}{_config.OrchestrateEndpoint}";
        var payload = new { prompt, sessionId, userId = "delegated" };

        var response = await PostWithRetryAsync(url, payload);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException(
                $"External agent returned {response.StatusCode}: {body}");
        }

        _logger.LogInformation("Orchestration triggered on {AgentId} for session {SessionId}",
            _config.AgentId, sessionId);
    }

    /// <summary>Forward the user's input to Agent B's RaiseUserInput endpoint.</summary>
    private async Task ForwardUserInputToAgentAsync(
        string agentBaseUrl, string instanceId,
        string taskId, string skillName, string userInput)
    {
        var url = $"{agentBaseUrl}/api/instances/" +
            $"{Uri.EscapeDataString(instanceId)}/tasks/" +
            $"{Uri.EscapeDataString(taskId)}/skills/" +
            $"{Uri.EscapeDataString(skillName)}/input";

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(userInput, System.Text.Encoding.UTF8, "text/plain")
        };
        await AddAuthHeaderAsync(request);

        var response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("Agent B rejected user input: {Status} {Body}",
                response.StatusCode, body);
            throw new InvalidOperationException(
                $"External agent rejected input ({response.StatusCode}). The agent may have timed out.");
        }

        _logger.LogInformation("Forwarded user input to {AgentId}: {Url}", _config.AgentId, url);
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    /// <summary>Check if _currentPayload is a continuation token for this agent.</summary>
    private JObject? GetContinuationIfMatch()
    {
        if (_currentPayload == null) return null;
        if (_currentPayload.Value<string>("delegationType") != "externalAgent") return null;
        if (_currentPayload.Value<string>("agentId") != _config.AgentId) return null;
        return _currentPayload;
    }

    private static CXOAgentResponse ParseAgentResult(string resultJson)
    {
        try
        {
            return JsonConvert.DeserializeObject<CXOAgentResponse>(resultJson)
                ?? new CXOAgentResponse { IsSuccess = true, Response = resultJson };
        }
        catch
        {
            return new CXOAgentResponse { IsSuccess = true, Response = resultJson };
        }
    }

    private static string ExtractStatusMessage(JsonElement statusPayload)
    {

		// TODO: Fix this if this is incorrect. Added to fix the tool status messages.
		var status = JToken.Parse(statusPayload.GetRawText());
		// Prefer the latest running skill execution message (most specific)
		var skillExecutions = status["SkillExecutions"] ?? status["skillExecutions"];
		if (skillExecutions is JArray skills)
		{
			// StepState.Running == 1
			var running = skills.LastOrDefault(s =>
				(s["State"]?.Value<int>() ?? s["state"]?.Value<int>()) == 1);
			var msg = running?["Message"]?.ToString() ?? running?["message"]?.ToString();
			if (!string.IsNullOrEmpty(msg))
				return msg;
		}

		// Fall back to latest running step
		var steps = status["Steps"] ?? status["steps"];
		if (steps is JArray stepArray)
		{
			var running = stepArray.LastOrDefault(s =>
				(s["State"]?.Value<int>() ?? s["state"]?.Value<int>()) == 1);
			var msg = running?["Message"]?.ToString() ?? running?["message"]?.ToString();
			if (!string.IsNullOrEmpty(msg))
				return msg;
			var stepName = running?["StepName"]?.ToString() ?? running?["stepName"]?.ToString();
			if (!string.IsNullOrEmpty(stepName))
				return $"Running: {stepName}";
		}

		// Last resort: CurrentStep field
		var currentStep = status["CurrentStep"]?.ToString() ?? status["currentStep"]?.ToString();
		return !string.IsNullOrEmpty(currentStep)
			? $"Current step: {currentStep}"
			: "Processing…";

	// Original from Iteration 1
	// if (statusPayload.TryGetProperty("currentStep", out var step))
	//     return step.GetString() ?? "Processing...";
	// if (statusPayload.TryGetProperty("CurrentStep", out var step2))
	//     return step2.GetString() ?? "Processing...";
	// return "Processing...";
    }

    private static CXOAgentResponse ErrorResponse(string message) => new()
    {
        IsSuccess = false,
        Response = message
    };

    // ── Auth ─────────────────────────────────────────────────────────────

    private async Task AddAuthHeaderAsync(HttpRequestMessage request)
    {
        if (_config.ManagedIdentityScope != null)
        {
            var token = await _credential.GetTokenAsync(
                new TokenRequestContext(new[] { _config.ManagedIdentityScope }),
                CancellationToken.None);
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.Token);
        }
        else if (!string.IsNullOrEmpty(_authContext.AccessToken))
        {
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _authContext.AccessToken);
        }
    }

    private async Task<HttpResponseMessage> PostWithAuthAsync(string requestUri)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
        await AddAuthHeaderAsync(request);
        return await _httpClient.SendAsync(request);
    }

    private async Task<HttpResponseMessage> PostWithRetryAsync<T>(string requestUri, T payload)
    {
        var delay = InitialRetryDelay;
        HttpResponseMessage? lastResponse = null;

        for (var attempt = 0; attempt <= _config.MaxRetries; attempt++)
        {
            if (attempt > 0)
            {
                _logger.LogWarning("Retrying POST {Uri} (attempt {Attempt}/{Max})",
                    requestUri, attempt, _config.MaxRetries);
                await Task.Delay(delay);
                delay = TimeSpan.FromMilliseconds(Math.Min(delay.TotalMilliseconds * 2, 15_000));
            }

            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, requestUri)
            {
                Content = new StringContent(
                    JsonConvert.SerializeObject(payload),
                    System.Text.Encoding.UTF8,
                    "application/json")
            };
            await AddAuthHeaderAsync(requestMessage);

            lastResponse = await _httpClient.SendAsync(requestMessage);

            if ((int)lastResponse.StatusCode < 500)
                return lastResponse;

            if (attempt < _config.MaxRetries)
                lastResponse.Dispose();
        }

        return lastResponse!;
    }

    // ── Circuit Breaker ──────────────────────────────────────────────────

    private bool IsCircuitOpen()
    {
        if (_circuitOpenedAt == null) return false;
        if ((DateTime.UtcNow - _circuitOpenedAt.Value).TotalSeconds > _config.CircuitBreakerRecoverySeconds)
        {
            _circuitOpenedAt = null;
            _consecutiveFailures = 0;
            return false;
        }
        return true;
    }

    private void RecordSuccess()
    {
        _consecutiveFailures = 0;
        _circuitOpenedAt = null;
    }

    private void RecordFailure()
    {
        _consecutiveFailures++;
        if (_consecutiveFailures >= _config.CircuitBreakerThreshold)
        {
            _circuitOpenedAt = DateTime.UtcNow;
            _logger.LogWarning("Circuit breaker OPENED for {AgentId} after {Failures} failures",
                _config.AgentId, _consecutiveFailures);
        }
    }
}
