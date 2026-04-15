namespace CXOAI.Tools.Configuration;

/// <summary>
/// Configuration for an external CXOAI agent that can receive delegated tasks.
/// Loaded from the <c>ExternalAgents:Agents</c> section of environment settings JSON.
/// </summary>
public class ExternalAgentConfig
{
    /// <summary>Unique identifier for this external agent (e.g., "compliance-agent").</summary>
    public required string AgentId { get; set; }

    /// <summary>Base URL of the external function app (e.g., "https://func-cxoai-compliance.azurewebsites.net").</summary>
    public required string BaseUrl { get; set; }

    /// <summary>Endpoint for starting orchestration. Default: "/api/orchestrate".</summary>
    public string OrchestrateEndpoint { get; set; } = "/api/orchestrate";

    /// <summary>Endpoint for SignalR negotiation. Default: "/api/negotiate".</summary>
    public string NegotiateEndpoint { get; set; } = "/api/negotiate";

    /// <summary>Maximum wait time for the external agent to complete, in seconds. Default: 300 (5 min).</summary>
    public int TimeoutSeconds { get; set; } = 300;

    /// <summary>Maximum retry attempts for HTTP calls. Default: 3.</summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>Circuit breaker failure threshold. Default: 3 consecutive failures.</summary>
    public int CircuitBreakerThreshold { get; set; } = 3;

    /// <summary>Circuit breaker recovery window in seconds. Default: 60.</summary>
    public int CircuitBreakerRecoverySeconds { get; set; } = 60;

    /// <summary>Optional managed identity scope for S2S auth with the external agent.</summary>
    public string? ManagedIdentityScope { get; set; }

    /// <summary>Description of what this external agent does.</summary>
    public string? Description { get; set; }
}

/// <summary>
/// Collection of external agent configurations loaded from environment settings.
/// </summary>
public class ExternalAgentsConfig
{
    public Dictionary<string, ExternalAgentConfig> Agents { get; set; } = new();
}
