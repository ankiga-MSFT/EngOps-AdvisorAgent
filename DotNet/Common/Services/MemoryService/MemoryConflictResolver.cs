using Azure.AI.OpenAI;
using Azure.Core;
using InfraService.OpenTelemetryProvider;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using System.Diagnostics;
using System.Text.Json;

namespace CXOAI.Memory;

/// <summary>
/// Uses an LLM to resolve conflicts between a new fact and an existing similar fact.
/// </summary>
public class MemoryConflictResolver
{
    private readonly string _endpoint;
    private readonly string _modelName;
    private readonly TokenCredential _credential;
    private readonly ILogger<MemoryConflictResolver> _logger;
    private readonly IMetricsProvider? _metricsProvider;

    public MemoryConflictResolver(string endpoint, TokenCredential credential, ILogger<MemoryConflictResolver> logger, string modelName = "gpt-4o-mini", IMetricsProvider? metricsProvider = null)
    {
        _endpoint = endpoint;
        _credential = credential;
        _modelName = modelName;
        _logger = logger;
        _metricsProvider = metricsProvider;
    }

    public async Task<MemoryOperationResult> ResolveAsync(string newFact, string existingFact)
    {
        var systemPrompt = """
            # Memory Conflict Resolver

            ## Role
            Given an existing fact and a new fact, decide the operation.

            ## Operations
            - **Add**: The new fact contains DIFFERENT information. Both should exist.
            - **Update**: Same topic, newer/corrected information. Replace with merged version in MergedFact.
            - **Noop**: Identical or redundant. No action needed.

            ## Rules
            - Same entity + same attribute + different values → Update (use newer value).
            - Same entity + different attributes → Add.
            - Semantically identical → Noop.
            - **Different entity IDs (TPID, CH URI) always means different entities → Add.**
              Even if names are similar (e.g., "Walmart Inc." vs "Walmart Canada Bank",
              "Azure Data Box" vs "Azure Data Explorer"), different IDs make them distinct.
              NEVER merge or update across entity IDs.
            - When updating, produce a clean MergedFact.
            - Category is REQUIRED for every operation, including Noop.
            - Category must be exactly "Permanent" or "Temporal". Never leave it empty.
            - Use Permanent for preferences, entity mappings, associations.
            - Use Temporal for data values, metrics, time-bound information.
            - For Noop, set Category to match the existing fact's category.
            - For Noop, copy the existing fact text into MergedFact.
            """;

        var client = new AzureOpenAIClient(new Uri(_endpoint), _credential);
        var chatClient = client.GetChatClient(_modelName);

        var userPrompt = $"Existing fact: \"{existingFact}\"\nNew fact: \"{newFact}\"";

        _logger.LogInformation("Calling MemoryConflictResolver.Resolve with existing='{Existing}', new='{New}'",
            existingFact[..Math.Min(100, existingFact.Length)], newFact[..Math.Min(100, newFact.Length)]);

        var options = new ChatCompletionOptions
        {
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                "memory_operation",
                BinaryData.FromString(OperationSchema))
        };

        using var latency = _metricsProvider?.LatencyMeasureOperation(MetricNames.LlmCall,
            new KeyValuePair<string, object?>(MetricNames.TagStepName, "MemoryConflictResolver"));
        var completion = await chatClient.CompleteChatAsync(
        [
            new SystemChatMessage(systemPrompt),
            new UserChatMessage(userPrompt)
        ], options);

        var json = completion.Value.Content[0].Text;
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new CaseInsensitiveEnumConverterFactory() }
        };

        MemoryOperationResult result;
        try
        {
            result = JsonSerializer.Deserialize<MemoryOperationResult>(json, jsonOptions)
                     ?? new MemoryOperationResult { Operation = MemoryOperationType.Noop };
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse conflict resolution response. Raw JSON: {Json}", json);
            latency?.SetState(ActivityStatusCode.Error);
            _metricsProvider?.TrackAvailabilityMetric(MetricNames.LlmCall, 1, ex,
                new KeyValuePair<string, object?>(MetricNames.TagStepName, "MemoryConflictResolver"));
            return new MemoryOperationResult
            {
                Operation = MemoryOperationType.Noop,
                Reasoning = "Deserialization failed - skipping to avoid memory corruption"
            };
        }

        latency?.SetState(ActivityStatusCode.Ok);
        _metricsProvider?.TrackAvailabilityMetric(MetricNames.LlmCall, 1, null,
            new KeyValuePair<string, object?>(MetricNames.TagStepName, "MemoryConflictResolver"));
        _logger.LogInformation("Called MemoryConflictResolver.Resolve, here is response: Operation={Operation}, Category={Category}, Reasoning={Reasoning}",
            result.Operation, result.Category, result.Reasoning);
        return result;
    }

    private const string OperationSchema = """
        {
          "type": "object",
          "properties": {
            "Operation": { "type": "string", "enum": ["Add", "Update", "Noop"] },
            "MergedFact": { "type": "string" },
            "Category": { "type": "string", "enum": ["Permanent", "Temporal"] },
            "Reasoning": { "type": "string" }
          },
          "required": ["Operation", "MergedFact", "Category", "Reasoning"],
          "additionalProperties": false
        }
        """;
}
