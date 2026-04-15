namespace CXOAI.SkillFramework;

/// <summary>
/// User-facing messages returned by the orchestrator pipeline.
/// Centralized here so both the Durable Functions and Console paths
/// use identical wording.
/// </summary>
public static class OrchestratorMessages
{
    public const string GracefulError =
        "I'm sorry, something went wrong while processing your request. Please try again. " +
        "If the issue persists, please contact support.";

    public const string UnknownIntent =
        "I'm sorry, I couldn't understand your request. " +
        "Please try rephrasing your query with a specific data metric, customer name, or action " +
        "(e.g., \"show me csat of Walmart\" or \"what does csat mean?\").";

    public const string NoTasksGenerated =
        "I don't have the capability to handle this request. " +
        "Please try a query related to data metrics (e.g., csat, consumption units, revenue) " +
        "or reporting (Excel, Word, PDF, email).";
}
