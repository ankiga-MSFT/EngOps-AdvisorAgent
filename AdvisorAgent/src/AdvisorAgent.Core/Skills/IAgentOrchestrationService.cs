using AdvisorAgent.Core.Models;
using Microsoft.Extensions.AI;

namespace AdvisorAgent.Core.Skills;

/// <summary>
/// Core orchestration service that drives the Advisor Agent pipeline steps.
/// </summary>
public interface IAgentOrchestrationService
{
    /// <summary>
    /// Resolves Azure context (subscription, resource group, resource) from the user prompt.
    /// </summary>
    Task<AzureContext> ResolveAzureContextAsync(string prompt, AzureContext? existingContext, List<ConversationTurn>? conversationHistory = null);

    /// <summary>
    /// Classifies the user's intent as Informational, ActionRequired, or Unknown.
    /// </summary>
    Task<UserIntent> ClassifyIntentAsync(string prompt, string azureContextSummary, List<ConversationTurn>? conversationHistory = null);

    /// <summary>
    /// Provides a direct answer for Informational intent queries.
    /// </summary>
    Task<string> AnswerDirectlyAsync(string prompt, string azureContextSummary, List<ConversationTurn>? conversationHistory = null);

    /// <summary>
    /// Decomposes an ActionRequired prompt into a task plan with skill assignments.
    /// </summary>
    Task<List<TaskPlanItem>> DecomposeTasksAsync(string prompt, string azureContextSummary, List<ConversationTurn>? conversationHistory = null);

    /// <summary>
    /// Returns skill definitions for the given skill names from the configuration store.
    /// </summary>
    List<AgentSkillDefinition> GetSkillDefinitions(List<string> skillNames);

    /// <summary>
    /// Generates a skill-specific prompt from the task context and upstream outputs.
    /// </summary>
    Task<string> GenerateSkillPromptAsync(
        string taskLabel,
        string skillDescription,
        string expectedInput,
        string azureContextSummary,
        string upstreamOutputs,
        string originalPrompt,
        List<ConversationTurn>? conversationHistory = null);

    /// <summary>
    /// Executes a skill using the Agent Framework with resolved tools.
    /// </summary>
    Task<AdvisorAgentResponse> ExecuteSkillAsync(AgentSkillDefinition skill, string prompt, string? accessToken = null);

    /// <summary>
    /// Resolves tool instances for a skill via reflection-based discovery.
    /// </summary>
    List<AITool> ResolveTools(AgentSkillDefinition skill);
}
