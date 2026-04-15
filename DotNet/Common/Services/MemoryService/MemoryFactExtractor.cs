using Azure.AI.OpenAI;
using Azure.Core;
using InfraService.OpenTelemetryProvider;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using System.Diagnostics;
using System.Text.Json;

namespace CXOAI.Memory;

/// <summary>
/// Uses an LLM to extract discrete facts from conversation content.
/// Supports scoped extraction: user preferences, system facts.
/// </summary>
public class MemoryFactExtractor
{
    private readonly string _endpoint;
    private readonly string _modelName;
    private readonly TokenCredential _credential;
    private readonly ILogger<MemoryFactExtractor> _logger;
    private readonly IMetricsProvider? _metricsProvider;

    public MemoryFactExtractor(string endpoint, TokenCredential credential, ILogger<MemoryFactExtractor> logger, string modelName = "gpt-4o-mini", IMetricsProvider? metricsProvider = null)
    {
        _endpoint = endpoint;
        _credential = credential;
        _modelName = modelName;
        _logger = logger;
        _metricsProvider = metricsProvider;
    }

    public async Task<List<ExtractedFact>> ExtractFactsAsync(string conversationContent, MemoryScope scope = MemoryScope.User)
    {
        var systemPrompt = scope switch
        {
            MemoryScope.Org => OrgExtractionPrompt,
            MemoryScope.System => OrgExtractionPrompt, // System redirects to Org logic for now
            _ => UserExtractionPrompt
        };

        var client = new AzureOpenAIClient(new Uri(_endpoint), _credential);
        var chatClient = client.GetChatClient(_modelName);

        _logger.LogInformation("Calling MemoryFactExtractor.ExtractFacts ({Scope}) with contentLength={ContentLen}",
            scope, conversationContent.Length);

        var options = new ChatCompletionOptions
        {
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                "extracted_facts",
                BinaryData.FromString(ExtractedFactsSchema))
        };

        using var latency = _metricsProvider?.LatencyMeasureOperation(MetricNames.LlmCall,
            new KeyValuePair<string, object?>(MetricNames.TagStepName, "MemoryFactExtractor.ExtractFacts"));
        var completion = await chatClient.CompleteChatAsync(
        [
            new SystemChatMessage(systemPrompt),
            new UserChatMessage($"## Content\n{conversationContent}")
        ], options);

        var json = completion.Value.Content[0].Text;
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new CaseInsensitiveEnumConverterFactory() }
        };
        var result = JsonSerializer.Deserialize<ExtractedFacts>(json, jsonOptions);
        var facts = result?.Facts ?? [];

        latency?.SetState(ActivityStatusCode.Ok);
        _metricsProvider?.TrackAvailabilityMetric(MetricNames.LlmCall, 1, null,
            new KeyValuePair<string, object?>(MetricNames.TagStepName, "MemoryFactExtractor.ExtractFacts"));
        _logger.LogInformation("Called MemoryFactExtractor.ExtractFacts ({Scope}), extracted {Count} fact(s): [{Facts}]",
            scope, facts.Count, string.Join(" | ", facts.Select(f => f.Fact[..Math.Min(80, f.Fact.Length)])));
        return facts;
    }

    private const string UserExtractionPrompt = """
        # Fact Extractor – User Scope (Preferences & Entity Directory)

        ## Role
        Extract user preferences AND entity-TPID mappings from the conversation.
        Do NOT extract any data values, metric results, or trend data — those belong in Org scope.

        ## Entity Types
        The system has four entity types. Always identify the correct type:
        - **customer**: Companies/organizations. ONE customer can have MULTIPLE subsidiaries,
          each with its own name and TPID (e.g., "Walmart Inc." TPID:784852, "Walmart Canada Bank" TPID:902415).
          Always treat different TPIDs as SEPARATE entities even if the names share a parent brand.
        - **product**: Azure services (e.g., "Azure Data Box", "Azure Data Explorer"). Each has ONE unique TPID.
          Similar names do NOT mean same entity — "Azure Data Box" ≠ "Azure Data Explorer".
        - **workload**: Workload groupings (e.g., "M365 Bedrock List", "ME Critical", "QCC"). Each has ONE unique TPID.
        - **program**: Programs (e.g., "AEM", "AMP", "ARR", "Azure Priority Zero"). Each has ONE unique TPID.

        ## What to extract

        ### Entity-TPID mappings (one fact per entity, REQUIRED format)
        Every entity mentioned in the conversation MUST be extracted as its own fact.
        Fact text format: "{EntityName} [{entityType}] (TPID:{id})"
        Examples:
        - "Walmart Inc. [customer] (TPID:784852)"
        - "Walmart Canada Bank [customer] (TPID:902415)"
        - "Adobe Inc. [customer] (TPID:8975)"
        - "Azure Data Box [product] (TPID:55555)"
        Rules for entity facts:
        - ONE fact per entity — never combine multiple entities into one fact
        - Always include the entity name, type in brackets, and TPID in parentheses
        - When multiple entities share a parent name (e.g., Walmart Inc., Walmart Canada Bank),
          extract EACH as a SEPARATE fact with its own TPID. Never combine them.
        - Set EntityType and EntityId on the extracted fact object (not just in the text)

        ### User preferences and clarifications
        - "User prefers Word format for exports"
        - "User works on Walmart Inc. account"
        - "User confirmed time range is last 6 months"

        ## What to NEVER extract (belongs in Org scope, not here)
        - Metric values (e.g., "CSAT is 72.45") — DO NOT extract these
        - Trend data (e.g., "CSAT trend: Oct=72, Nov=68") — DO NOT extract these
        - Any numeric data results from skill outputs — DO NOT extract these
        - Orchestration details, skill names, or internal pipeline info
        - Tool call parameters or API responses verbatim

        ## Rules
        - Each fact must be a single, complete statement that stands alone.
        - Category: always "Permanent" (User scope stores only preferences).
        - Scope: always "User".
        - Source: "user_input" for user responses, "prompt" for the query.
        - Tags: include relevant keywords (entity names, TPID, preference type).
        - EntityType: set to "customer", "product", "workload", or "program" when about a specific entity. Null for non-entity facts.
        - EntityId: set to the TPID or CH URI when available. Null if not mentioned.
        - CacheKey: for entity mapping facts (those with EntityType and EntityId), set to "entity:{entityType}:{entityId}" (e.g., "entity:customer:784852"). This prevents duplicate entity entries across sessions. For non-entity facts (preferences, clarifications), set to empty string "".
        """;

    private const string OrgExtractionPrompt = """
        # Fact Extractor – Org Scope (Shared Data Cache)

        ## Role
        Extract ONLY data values, metric results, and trend data from the conversation.
        These facts are shared across ALL users and sessions as an app-wide cache.
        Do NOT extract user preferences or entity associations — those belong in User scope.

        ## Entity Types
        The system has four entity types. Always identify the correct type:
        - **customer**: Companies/organizations. ONE customer can have MULTIPLE subsidiaries,
          each with its own name and TPID (e.g., "Walmart Inc." TPID:784852, "Walmart Canada Bank" TPID:902415).
          Always treat different TPIDs as SEPARATE entities even if the names share a parent brand.
        - **product**: Azure services (e.g., "Azure Data Box", "Azure Data Explorer"). Each has ONE unique TPID.
          Similar names do NOT mean same entity — "Azure Data Box" ≠ "Azure Data Explorer".
        - **workload**: Workload groupings (e.g., "M365 Bedrock List", "ME Critical", "QCC"). Each has ONE unique TPID.
        - **program**: Programs (e.g., "AEM", "AMP", "ARR", "Azure Priority Zero"). Each has ONE unique TPID.

        ## What to extract
        Extract metric values and trend data WITH full query context.
        Every fact MUST include ALL of the following in the fact text:
        - Entity name, type, and ID (TPID/CH URI) if available
        - Metric name
        - Time range / date range used in the query
        - Any active filters (Region, Subscription, etc.)
        - The actual data values

        ### Single-value metric examples:
        - "CSAT for Walmart Inc. [customer] (TPID:784852), default timerange, no filters: 72.45"
        - "IRMET for Azure Data Box [product] (TPID:55555), last 6 months, Region=West US: 85.2%"
        - "Open case count for Contoso [customer] (TPID:67890), last 30 days: 142"

        ### Trend data examples (≤12 data points — keep ALL values):
        - "CSAT trend for Walmart Inc. [customer] (TPID:784852), last 6 months, no filters: Oct=72, Nov=68, Dec=71, Jan=75, Feb=73, Mar=69"

        ### Rules for data facts:
        - Extract as ONE fact per metric per entity (do NOT split data points into separate facts)
        - Include ALL data points in compact format: "Label=Value, Label=Value, ..."
        - If no specific time range was mentioned, write "default timerange"
        - If no filters were applied, write "no filters"
        - If entity ID/TPID is in the conversation, include it in parentheses

        ### CacheKey (REQUIRED for Org facts)
        Generate a normalized lookup key for each fact. This enables deterministic cache matching
        and prevents time-range or threshold conflation in vector search.
        Format: {metric}:{entityTarget}:{timeRange}[:{filters}]
        - metric: lowercase metric or data type name (csat, irmet, casecount, csat-trend, incident-impact, etc.)
        - entityTarget: TPID/entity ID if about a specific entity, "all-{type}" if aggregate (all-customers, all-products).
          For incident queries, use the incident ID as entityTarget.
        - timeRange: normalized duration (6m, 1m, 30d, 1y, ytd, default). Use "default" if no time range specified.
        - filters: optional, appended with colon if present (lt2.0, gt3.5, region=westus)
        - Use lowercase throughout. No spaces.
        - The key must capture ALL parameters that would change the query result.

        CacheKey examples:
        - "csat:784852:6m"                    (CSAT for TPID 784852, last 6 months)
        - "csat:784852:1m"                    (CSAT for TPID 784852, last 1 month — different from above)
        - "csat:all-customers:6m:lt2.0"       (all customers with CSAT < 2.0, 6 months)
        - "csat-trend:784852:6m"              (CSAT trend for TPID 784852, 6 months)
        - "incident-impact:12345:11111"       (incident 12345 impact on TPID 11111)
        - "irmet:55555:6m:region=westus"      (IRMET for TPID 55555, 6m, Region filter)
        - "casecount:67890:30d"               (case count for TPID 67890, 30 days)

        ## What to NEVER extract (belongs in User scope, not here)
        - User preferences (e.g., "User prefers Word format") — DO NOT extract these
        - Entity associations (e.g., "User works on Walmart") — DO NOT extract these
        - Orchestration details, skill names, or internal pipeline info
        - Tool call parameters or API responses verbatim

        ## Rules
        - Each fact must be a single, complete statement that stands alone.
        - Category: always "Temporal" (all Org facts expire after 1 hour).
        - Scope: always "Org".
        - Source: "skill_output" for skill results, "prompt" for the query.
        - Tags: include relevant keywords (entity names, metric names, TPID, filters, time range).
        - EntityType: set to "customer", "product", "workload", or "program". Required for Org facts.
        - EntityId: set to the TPID or CH URI when available. Null if not mentioned.
        - CacheKey: REQUIRED for every Org fact. Must be a non-empty normalized key in the format {metric}:{entityTarget}:{timeRange}[:{filters}]. See CacheKey examples above. Never return empty string for Org facts.
        """;

    private const string ExtractedFactsSchema = """
        {
          "type": "object",
          "properties": {
            "Facts": {
              "type": "array",
              "items": {
                "type": "object",
                "properties": {
                  "Fact": { "type": "string" },
                  "Category": { "type": "string", "enum": ["Permanent", "Temporal"] },
                  "Scope": { "type": "string", "enum": ["User", "Org", "System"] },
                  "Source": { "type": "string" },
                  "Tags": { "type": "array", "items": { "type": "string" } },
                  "EntityType": { "type": ["string", "null"], "enum": ["customer", "product", "workload", "program", null] },
                  "EntityId": { "type": ["string", "null"] },
                  "CacheKey": { "type": "string" }
                },
                "required": ["Fact", "Category", "Scope", "Source", "Tags", "EntityType", "EntityId", "CacheKey"],
                "additionalProperties": false
              }
            }
          },
          "required": ["Facts"],
          "additionalProperties": false
        }
        """;

    /// <summary>
    /// Lightweight extraction: derive a normalized cache key from a query string.
    /// Used at recall time to enable deterministic lookup before falling back to vector search.
    /// Returns null if the query can't be reduced to a deterministic key.
    /// </summary>
    public async Task<string?> ExtractCacheKeyAsync(string query)
    {
        var client = new AzureOpenAIClient(new Uri(_endpoint), _credential);
        var chatClient = client.GetChatClient(_modelName);

        _logger.LogInformation("Calling MemoryFactExtractor.ExtractCacheKey for query ({QueryLen} chars)", query.Length);

        var options = new ChatCompletionOptions
        {
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                "cache_key",
                BinaryData.FromString(CacheKeySchema))
        };

        try
        {
            using var latency = _metricsProvider?.LatencyMeasureOperation(MetricNames.LlmCall,
                new KeyValuePair<string, object?>(MetricNames.TagStepName, "MemoryFactExtractor.ExtractCacheKey"));
            var completion = await chatClient.CompleteChatAsync(
            [
                new SystemChatMessage(CacheKeyExtractionPrompt),
                new UserChatMessage(query)
            ], options);

            var json = completion.Value.Content[0].Text;
            using var doc = JsonDocument.Parse(json);
            var cacheKey = doc.RootElement.GetProperty("CacheKey").GetString()?.Trim();

            latency?.SetState(ActivityStatusCode.Ok);
            _metricsProvider?.TrackAvailabilityMetric(MetricNames.LlmCall, 1, null,
                new KeyValuePair<string, object?>(MetricNames.TagStepName, "MemoryFactExtractor.ExtractCacheKey"));
            _logger.LogInformation("Extracted CacheKey: '{CacheKey}' from query", cacheKey ?? "(none)");
            return string.IsNullOrWhiteSpace(cacheKey) ? null : cacheKey;
        }
        catch (Exception ex)
        {
            _metricsProvider?.TrackAvailabilityMetric(MetricNames.LlmCall, 1, ex,
                new KeyValuePair<string, object?>(MetricNames.TagStepName, "MemoryFactExtractor.ExtractCacheKey"));
            _logger.LogWarning(ex, "ExtractCacheKeyAsync failed, returning null");
            return null;
        }
    }

    private const string CacheKeyExtractionPrompt = """
        # Cache Key Extractor
        Extract a normalized cache key from the user's query for Org-scoped data cache lookup.

        ## Format
        {metric}:{entityTarget}:{timeRange}[:{filters}]

        ## Rules
        - metric: lowercase metric or data type name (csat, irmet, casecount, csat-trend, incident-impact)
        - entityTarget: TPID/entity ID if specific entity, "all-{type}" for aggregate queries (all-customers, all-products).
          For incident queries, use the incident ID as the entity target.
        - timeRange: normalized duration (6m, 1m, 30d, 1y, ytd, default). Use "default" if not specified.
        - filters: optional, appended if present (lt2.0, gt3.5, region=westus)
        - Use lowercase throughout. No spaces.
        - The key should capture ALL parameters that would change the query result.

        ## Examples
        Query: "get me csat score for Walmart (tpid:12345) for last 6 months" → CacheKey: "csat:12345:6m"
        Query: "give me all customers where csat < 2.0 for last 6 months" → CacheKey: "csat:all-customers:6m:lt2.0"
        Query: "for incident 12345 show all impacted customers" → CacheKey: "incident-impact:12345"
        Query: "for incident 12345 show me Contoso (tpid:11111) tickets" → CacheKey: "incident-impact:12345:11111"
        Query: "CSAT trend for Azure Data Box (TPID:55555) last 3 months" → CacheKey: "csat-trend:55555:3m"
        Query: "compare csat between Walmart and Contoso" → CacheKey: ""

        ## Output
        Return the cache key string. If the query is too vague, compound, or cannot be
        reduced to a deterministic key, return empty string "".
        """;

    private const string CacheKeySchema = """
        {
          "type": "object",
          "properties": {
            "CacheKey": { "type": "string" }
          },
          "required": ["CacheKey"],
          "additionalProperties": false
        }
        """;
}
