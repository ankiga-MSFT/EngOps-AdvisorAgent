using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.SignalR.Management;
using Microsoft.Extensions.Logging;

namespace CXOAI.StatusNotifier;

/// <summary>
/// Sends tool-level progress notifications via Azure SignalR using the existing
/// "ReceiveStatus" event so the frontend handles it without any changes.
/// </summary>
public class SignalRToolStatusNotifier : IToolStatusNotifier
{
    private readonly ServiceHubContext _hubContext;
    private readonly ILogger<SignalRToolStatusNotifier> _logger;

    public SignalRToolStatusNotifier(ServiceHubContext hubContext, ILogger<SignalRToolStatusNotifier> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task NotifyAsync(string sessionId, string toolName, string message)
    {
        try
        {
            await _hubContext.Clients.Group(sessionId).SendAsync("ReceiveToolProgress",
                new { toolName, message, timestamp = DateTimeOffset.UtcNow });
            _logger.LogDebug("Tool notification sent — session '{SessionId}', tool '{ToolName}': {Message}",
                sessionId, toolName, message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send tool notification — session '{SessionId}', tool '{ToolName}'",
                sessionId, toolName);
        }
    }

    public async Task ForwardEventAsync(string sessionId, string eventName, object[] args)
    {
        try
        {
            await _hubContext.Clients.Group(sessionId).SendCoreAsync(eventName, args);
            _logger.LogDebug("Forwarded event '{EventName}' — session '{SessionId}' ({ArgCount} arg(s))",
                eventName, sessionId, args.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to forward event '{EventName}' — session '{SessionId}'",
                eventName, sessionId);
        }
    }
}
