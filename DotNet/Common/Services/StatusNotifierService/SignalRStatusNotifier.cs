using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.SignalR.Management;
using Microsoft.Extensions.Logging;

namespace CXOAI.StatusNotifier;

/// <summary>
/// Pushes orchestrator status updates to connected clients via Azure SignalR Service.
/// Uses the serverless management SDK — no full SignalR server required.
///
/// Client contract (hub name: "orchestrator"):
///   - ReceiveStatus(OrchestratorStatus status)          — full status snapshot
///   - ReceiveUserInputRequest(string skillName, string prompt, string sessionId)
///
/// User input flow:
///   1. Orchestrator calls WaitForUserInputAsync ? sends ReceiveUserInputRequest to client
///   2. Client submits response via HTTP endpoint (e.g., /api/instances/{sessionId}/userInput)
///   3. HTTP trigger resolves the pending TaskCompletionSource via ResolveUserInput
/// </summary>
public class SignalRStatusNotifier : IStatusNotifier, IAsyncDisposable
{
    private readonly ServiceHubContext _hubContext;
    private readonly ILogger<SignalRStatusNotifier> _logger;
    private readonly string _sessionId;

    private static readonly ConcurrentDictionary<string, TaskCompletionSource<string>> _pendingInputs = new();

    public SignalRStatusNotifier(
        ServiceHubContext hubContext,
        string sessionId,
        ILogger<SignalRStatusNotifier> logger)
    {
        _hubContext = hubContext;
        _sessionId = sessionId;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task PublishStatusAsync(OrchestratorStatus status)
    {
        try
        {
            await _hubContext.Clients.Group(_sessionId).SendAsync("ReceiveStatus", status);
            _logger.LogDebug("Published status for session '{SessionId}', step '{Step}'",
                _sessionId, status.CurrentStep);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish status for session '{SessionId}'", _sessionId);
        }
    }

    /// <inheritdoc/>
    public async Task<string> WaitForUserInputAsync(string skillName, string prompt)
    {
        var key = $"{_sessionId}:{skillName}";
        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingInputs[key] = tcs;

        try
        {
            await _hubContext.Clients.Group(_sessionId).SendAsync(
                "ReceiveUserInputRequest", skillName, prompt, _sessionId);

            _logger.LogInformation(
                "Waiting for user input — session '{SessionId}', skill '{SkillName}'",
                _sessionId, skillName);

            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            cts.Token.Register(() => tcs.TrySetCanceled(), useSynchronizationContext: false);

            return await tcs.Task;
        }
        finally
        {
            _pendingInputs.TryRemove(key, out _);
        }
    }

    /// <summary>
    /// Called by the HTTP trigger when the client submits a user response.
    /// Resolves the pending WaitForUserInputAsync call.
    /// </summary>
    public static bool ResolveUserInput(string sessionId, string skillName, string userResponse)
    {
        var key = $"{sessionId}:{skillName}";
        if (_pendingInputs.TryRemove(key, out var tcs))
        {
            tcs.TrySetResult(userResponse);
            return true;
        }
        return false;
    }

    public async ValueTask DisposeAsync()
    {
        await _hubContext.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
