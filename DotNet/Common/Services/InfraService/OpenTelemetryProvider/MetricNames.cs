namespace InfraService.OpenTelemetryProvider;

/// <summary>
/// Centralized metric operation names and tag keys used across the CXOAI pipeline.
/// All Geneva/OpenTelemetry metric instrumentation should reference these constants
/// to prevent typo drift and make renaming a single-line change.
/// </summary>
public static class MetricNames
{
    // ── Metric operation names (used in LatencyMeasureOperation / TrackAvailabilityMetric / counters) ──

    /// <summary>LLM call latency + availability (RunWithTimeoutAsync, MemoryFactExtractor, MemoryConflictResolver).</summary>
    public const string LlmCall = "LLM_Call";

    /// <summary>Skill execution latency + availability (ExecuteSkillAsync).</summary>
    public const string SkillExecution = "Skill_Execution";

    /// <summary>Cosmos DB / data store latency + availability (memory store, conversation store).</summary>
    public const string DataStoreOperation = "DataStore_Operation";

    /// <summary>Outbound HTTP call latency (HttpClientProvider).</summary>
    public const string HttpClientCall = "HttpClient_Call";

    /// <summary>Outbound HTTP call availability (HttpClientProvider).</summary>
    public const string HttpClientAvailability = "HttpClient_Availability";

    /// <summary>HTTP trigger counter / latency (CxoaiHttpTrigger, ArtifactDownloadFunction).</summary>
    public const string HttpTrigger = "HTTP_Trigger";

    /// <summary>Cosmos DB change feed sync latency + counters (ConfigurationStoreChangeFeedTrigger).</summary>
    public const string ChangeFeedSync = "ChangeFeed_Sync";

    // ── Tag keys (low-cardinality dimensions attached to metrics) ──

    public const string TagStepName = "StepName";
    public const string TagSkillName = "SkillName";
    public const string TagResourceName = "ResourceName";
    public const string TagOperationName = "OperationName";
}
