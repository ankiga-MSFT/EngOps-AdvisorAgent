namespace CXOAI.StatusNotifier;

public interface IStatusNotifier
{
    Task PublishStatusAsync(OrchestratorStatus status);
    Task<string> WaitForUserInputAsync(string skillName, string prompt);
}
