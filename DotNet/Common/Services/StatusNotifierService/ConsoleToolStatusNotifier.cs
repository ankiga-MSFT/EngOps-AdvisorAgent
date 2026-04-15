namespace CXOAI.StatusNotifier;

/// <summary>
/// Console fallback for tool-level progress notifications.
/// Used when Azure SignalR is not configured (local development).
/// </summary>
public class ConsoleToolStatusNotifier : IToolStatusNotifier
{
    public Task NotifyAsync(string sessionId, string toolName, string message)
    {
        Console.WriteLine($"  [{toolName}] {message}");
        return Task.CompletedTask;
    }

    public Task ForwardEventAsync(string sessionId, string eventName, object[] args)
    {
        Console.WriteLine($"  [Forward:{eventName}] → session {sessionId} ({args.Length} arg(s))");
        return Task.CompletedTask;
    }
}
