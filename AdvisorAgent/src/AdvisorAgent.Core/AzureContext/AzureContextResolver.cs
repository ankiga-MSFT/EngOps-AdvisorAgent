using AdvisorAgent.Core.Models;
using AdvisorAgent.Core.Skills;

namespace AdvisorAgent.Core.ContextResolution;

/// <summary>
/// Resolves Azure context by delegating to the orchestration service's LLM-based extraction.
/// </summary>
public sealed class AzureContextResolver : IAzureContextResolver
{
    private readonly IAgentOrchestrationService _orchestrationService;

    public AzureContextResolver(IAgentOrchestrationService orchestrationService)
    {
        _orchestrationService = orchestrationService;
    }

    public Task<Models.AzureContext> ResolveAsync(string prompt, Models.AzureContext? existingContext, List<ConversationTurn>? conversationHistory = null)
    {
        return _orchestrationService.ResolveAzureContextAsync(prompt, existingContext, conversationHistory);
    }
}
