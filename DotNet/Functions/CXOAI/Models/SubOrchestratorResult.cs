using CXOAI.SkillFramework;
using CXOAI.StatusNotifier;

namespace CXOAI.Functions.Models;

public class SubOrchestratorResult
{
    public OrchestratorStatus Status { get; set; } = new();
    public CXOAgentResponse Response { get; set; } = new();
    /// <summary>Only fresh skill outputs (tagged [TaskOutput:...]) for Org memory extraction.
    /// Excludes injected context to prevent the cyclic freshness problem.</summary>
    public string? FreshSkillOutputs { get; set; }
}