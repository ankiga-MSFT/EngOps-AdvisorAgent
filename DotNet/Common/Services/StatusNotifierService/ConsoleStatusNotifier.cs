using Microsoft.Extensions.Logging;

namespace CXOAI.StatusNotifier;

public class ConsoleStatusNotifier : IStatusNotifier
{
    private readonly ILogger _logger;

    public ConsoleStatusNotifier(ILogger logger)
    {
        _logger = logger;
    }

    public Task PublishStatusAsync(OrchestratorStatus status)
    {
        status.PrintToConsole(_logger);
        return Task.CompletedTask;
    }

    public Task<string> WaitForUserInputAsync(string skillName, string prompt)
    {
        Console.Write($"  [{skillName}] Your response: ");
        var input = Console.ReadLine() ?? string.Empty;
        return Task.FromResult(input);
    }
}
