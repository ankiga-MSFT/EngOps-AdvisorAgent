namespace CXOAI.StatusNotifier;

/// <summary>
/// Lightweight notification interface for tool-level progress updates.
/// Injected into tool classes via DI so tool methods can send real-time
/// progress notifications to the UI during execution.
/// </summary>
public interface IToolStatusNotifier
{
    /// <summary>
    /// Send a progress notification from a tool to the connected client.
    /// </summary>
    /// <param name="sessionId">The session to target.</param>
    /// <param name="toolName">The tool class name (e.g., "AspectTools").</param>
    /// <param name="message">A human-readable progress message.</param>
    Task NotifyAsync(string sessionId, string toolName, string message);

    /// <summary>
    /// Forward a raw SignalR event from an external agent to the connected client.
    /// Used by agent-relay tools (e.g., ManagedReviewAgentTools) to blindly forward
    /// events such as ReceiveStatus and ReceiveToolProgress.
    /// </summary>
    /// <param name="sessionId">The CXOAI session to target.</param>
    /// <param name="eventName">The SignalR event name (e.g., "ReceiveStatus").</param>
    /// <param name="args">The event arguments to forward as-is.</param>
    Task ForwardEventAsync(string sessionId, string eventName, object[] args);
}

/// <summary>
/// Marker interface for objects that accept a session context.
/// Implemented by <c>ToolBase</c> so the orchestrator can set sessionId
/// on tool instances without depending on the Tools project.
/// </summary>
public interface ISessionAware
{
    void SetSession(string sessionId);
}

/// <summary>
/// Interface for tools that support continuation payloads for multi-round
/// external agent delegation. When an external agent requests user input,
/// the tool stores continuation state in a <see cref="Newtonsoft.Json.Linq.JObject"/>
/// payload. On re-invocation, the orchestrator passes this payload back
/// so the tool can reconnect and resume.
/// </summary>
public interface IContinuationAware
{
    void SetContinuationPayload(Newtonsoft.Json.Linq.JObject? payload);
}

/// <summary>
/// Interface for tools that emit opaque payloads (e.g., continuation tokens)
/// as a side-channel. The orchestrator reads and clears the payload after the
/// agent run completes, attaching it to the <c>CXOAgentResponse</c>.
/// </summary>
public interface IPayloadEmitter
{
    Newtonsoft.Json.Linq.JObject? ConsumeEmittedPayload();
}
