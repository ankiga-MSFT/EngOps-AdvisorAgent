using CXOAI.StatusNotifier;
using Newtonsoft.Json.Linq;

namespace CXOAI.Tools;

/// <summary>
/// Abstract base class for all tool classes. Provides real-time notification
/// capability via <see cref="IToolStatusNotifier"/>. Tool methods call
/// <see cref="NotifyAsync"/> to send progress updates to the UI.
/// </summary>
public abstract class ToolBase : ISessionAware, IContinuationAware, IPayloadEmitter
{
    private readonly IToolStatusNotifier _notifier;
    private string? _sessionId;

    /// <summary>
    /// Continuation payload from a previous execution round.
    /// Set by the orchestrator before re-invoking the tool.
    /// Tools check this to detect continuation mode (e.g., reconnect to an external agent).
    /// </summary>
    protected JObject? _currentPayload;

    /// <summary>
    /// Payload emitted by the tool during execution (e.g., continuation state).
    /// Read and cleared by the orchestrator after the agent run completes.
    /// </summary>
    private JObject? _emittedPayload;

    protected ToolBase(IToolStatusNotifier notifier)
    {
        _notifier = notifier;
    }

    /// <summary>
    /// Called by the orchestrator before each skill execution to set the session context.
    /// </summary>
    public void SetSession(string sessionId)
    {
        _sessionId = sessionId;
    }

    /// <summary>
    /// Sets the continuation payload from a previous execution round.
    /// Called by the Activity when PayloadJson is present in the input.
    /// </summary>
    public void SetContinuationPayload(JObject? payload)
    {
        _currentPayload = payload;
    }

    /// <summary>
    /// Store a payload to be picked up by the orchestrator after the agent run.
    /// Used when the tool needs to propagate state (e.g., continuation tokens)
    /// without going through the LLM's response JSON.
    /// </summary>
    protected void EmitPayload(JObject payload)
    {
        _emittedPayload = payload;
    }

    /// <summary>
    /// Reads and clears any payload emitted during the last tool execution.
    /// Called by <see cref="OrchestratorStepService.ExecuteSkillAsync"/> after the agent run.
    /// </summary>
    public JObject? ConsumeEmittedPayload()
    {
        var p = _emittedPayload;
        _emittedPayload = null;
        return p;
    }

    /// <summary>
    /// Send a real-time progress notification to the UI.
    /// Safe to call even if sessionId is not set — silently no-ops.
    /// </summary>
    protected async Task NotifyAsync(string message)
    {
        if (_sessionId is not null)
            await _notifier.NotifyAsync(_sessionId, GetType().Name, message);
    }

    /// <summary>
    /// Forward a raw SignalR event from an external agent to the UI.
    /// Safe to call even if sessionId is not set — silently no-ops.
    /// </summary>
    protected async Task ForwardEventAsync(string eventName, params object[] args)
    {
        if (_sessionId is not null)
            await _notifier.ForwardEventAsync(_sessionId, eventName, args);
    }
}
