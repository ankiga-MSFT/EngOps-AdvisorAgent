using AdvisorAgent.Core.Models;

namespace AdvisorAgent.Core.ContextResolution;

/// <summary>
/// Resolves Azure context from user prompt by extracting subscription, resource group  
/// and resource identifiers. Delegates to the orchestration service for LLM-based extraction.
/// </summary>
public interface IAzureContextResolver
{
    /// <summary>
    /// Extracts Azure resource identifiers from the prompt.
    /// Returns an empty context if nothing can be resolved.
    /// </summary>
    Task<Models.AzureContext> ResolveAsync(string prompt, Models.AzureContext? existingContext, List<ConversationTurn>? conversationHistory = null);
}
