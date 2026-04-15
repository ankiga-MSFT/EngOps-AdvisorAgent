namespace CXOAI.SkillFramework;

/// <summary>
/// Thrown by <see cref="OrchestratorStepService"/> when an LLM call fails
/// due to timeout, transient HTTP error, or unexpected failure.
/// Both orchestrators can catch this single type to report step-level errors
/// via their respective notification channels (SignalR / console).
/// </summary>
public class LlmOperationException : Exception
{
    /// <summary>Pipeline step that failed (e.g., "ClassifyIntent", "ExecuteSkill(AspectSkill)").</summary>
    public string StepName { get; }

    /// <summary>True when the failure is likely transient and a retry may succeed.</summary>
    public bool IsTransient { get; }

    /// <summary>True when the failure was caused by a timeout.</summary>
    public bool IsTimeout { get; }

    public LlmOperationException(string stepName, string message, Exception? innerException = null,
        bool isTransient = false, bool isTimeout = false)
        : base(message, innerException)
    {
        StepName = stepName;
        IsTransient = isTransient;
        IsTimeout = isTimeout;
    }

    /// <summary>User-friendly error message suitable for UI display.</summary>
    public string UserMessage => IsTimeout
        ? $"The '{StepName}' step timed out. The AI service may be experiencing high latency. Please try again."
        : IsTransient
            ? $"The '{StepName}' step failed due to a temporary service error. Please try again."
            : $"The '{StepName}' step encountered an unexpected error. Please try again. If the issue persists, contact support.";
}
