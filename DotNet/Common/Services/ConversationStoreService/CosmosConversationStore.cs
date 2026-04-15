using InfraService.OpenTelemetryProvider;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Provider.Interfaces;
using System.Diagnostics;

namespace CXOAI.ConversationStore;

/// <summary>
/// Cosmos DB-backed conversation store using ICosmosDbProvider.
/// Stores two documents per session (both partitioned by userId):
///   1. Rolling summary (id=sessionId) — overwritten each turn
///   2. Raw history (id={sessionId}_history) — append-only array of turns
/// </summary>
public class CosmosConversationStore : IConversationStore
{
    private readonly ICosmosDbProvider _cosmosProvider;
    private readonly ILogger<CosmosConversationStore> _logger;
    private readonly IMetricsProvider? _metricsProvider;

    public CosmosConversationStore(ICosmosDbProvider cosmosProvider, ILogger<CosmosConversationStore> logger, IMetricsProvider? metricsProvider = null)
    {
        _cosmosProvider = cosmosProvider;
        _logger = logger;
        _metricsProvider = metricsProvider;
    }

    public async Task<string?> GetSessionSummaryAsync(string userId, string sessionId)
    {
        using var latency = _metricsProvider?.LatencyMeasureOperation(MetricNames.DataStoreOperation,
            new KeyValuePair<string, object?>(MetricNames.TagOperationName, "GetSessionSummary"));
        try
        {
            var doc = await _cosmosProvider.GetDocumentsAsync(sessionId, userId);
            var summary = doc?["summary"]?.ToString();
            _logger.LogDebug("Retrieved session summary for user '{UserId}', session '{SessionId}' ({Length} chars)",
                userId, sessionId, summary?.Length ?? 0);
            latency?.SetState(ActivityStatusCode.Ok);
            _metricsProvider?.TrackAvailabilityMetric(MetricNames.DataStoreOperation, 1, null,
                new KeyValuePair<string, object?>(MetricNames.TagOperationName, "GetSessionSummary"));
            return summary;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogDebug("No session summary found for user '{UserId}', session '{SessionId}' (first turn)",
                userId, sessionId);
            latency?.SetState(ActivityStatusCode.Ok);
            return null;
        }
    }

    public async Task UpsertSessionSummaryAsync(string userId, string sessionId, string summary)
    {
        using var latency = _metricsProvider?.LatencyMeasureOperation(MetricNames.DataStoreOperation,
            new KeyValuePair<string, object?>(MetricNames.TagOperationName, "UpsertSessionSummary"));
        var doc = new JObject
        {
            ["id"] = sessionId,
            ["UserId"] = userId,
            ["summary"] = summary,
            ["updatedAt"] = DateTimeOffset.UtcNow.ToString("o")
        };

        await _cosmosProvider.UpdateDocumentAsync(sessionId, userId, doc);
        latency?.SetState(ActivityStatusCode.Ok);
        _metricsProvider?.TrackAvailabilityMetric(MetricNames.DataStoreOperation, 1, null,
            new KeyValuePair<string, object?>(MetricNames.TagOperationName, "UpsertSessionSummary"));
        _logger.LogInformation("Upserted session summary for user '{UserId}', session '{SessionId}'",
            userId, sessionId);
    }

    public async Task AppendToHistoryAsync(string userId, string sessionId, string prompt, string response, string? requestId = null)
    {
        using var latency = _metricsProvider?.LatencyMeasureOperation(MetricNames.DataStoreOperation,
            new KeyValuePair<string, object?>(MetricNames.TagOperationName, "AppendToHistory"));
        var historyId = $"{sessionId}_history";

        // Read existing history doc or create new
        JObject historyDoc;
        try
        {
            historyDoc = await _cosmosProvider.GetDocumentsAsync(historyId, userId);
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            historyDoc = new JObject
            {
                ["id"] = historyId,
                ["UserId"] = userId,
                ["history"] = new JArray()
            };
        }

        // Append new turn
        var historyArray = (JArray)(historyDoc["history"] ?? new JArray());
        historyArray.Add(new JObject
        {
            ["prompt"] = prompt,
            ["response"] = response,
            ["timestamp"] = DateTimeOffset.UtcNow.ToString("o"),
            ["requestId"] = requestId ?? string.Empty
        });
        historyDoc["history"] = historyArray;
        historyDoc["updatedAt"] = DateTimeOffset.UtcNow.ToString("o");

        await _cosmosProvider.UpdateDocumentAsync(historyId, userId, historyDoc);
        latency?.SetState(ActivityStatusCode.Ok);
        _metricsProvider?.TrackAvailabilityMetric(MetricNames.DataStoreOperation, 1, null,
            new KeyValuePair<string, object?>(MetricNames.TagOperationName, "AppendToHistory"));
        _logger.LogInformation("Appended turn to history for user '{UserId}', session '{SessionId}', request '{RequestId}' ({TurnCount} turns)",
            userId, sessionId, requestId ?? "N/A", historyArray.Count);
    }

    public async Task<List<ConversationTurnEntry>?> GetSessionHistoryAsync(string userId, string sessionId, int? lastN = null)
    {
        using var latency = _metricsProvider?.LatencyMeasureOperation(MetricNames.DataStoreOperation,
            new KeyValuePair<string, object?>(MetricNames.TagOperationName, "GetSessionHistory"));
        try
        {
            var historyId = $"{sessionId}_history";
            var doc = await _cosmosProvider.GetDocumentsAsync(historyId, userId);
            var historyArray = doc?["history"] as JArray;
            if (historyArray is null || historyArray.Count == 0)
                return null;

            IEnumerable<JToken> items = historyArray;
            if (lastN.HasValue && lastN.Value > 0 && lastN.Value < historyArray.Count)
                items = historyArray.Skip(historyArray.Count - lastN.Value);

            latency?.SetState(ActivityStatusCode.Ok);
            _metricsProvider?.TrackAvailabilityMetric(MetricNames.DataStoreOperation, 1, null,
                new KeyValuePair<string, object?>(MetricNames.TagOperationName, "GetSessionHistory"));
            return items.Select(t => new ConversationTurnEntry
            {
                Prompt = t["prompt"]?.ToString() ?? string.Empty,
                Response = t["response"]?.ToString() ?? string.Empty,
                Timestamp = t["timestamp"]?.ToString() ?? string.Empty,
                RequestId = t["requestId"]?.ToString() ?? string.Empty
            }).ToList();
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            latency?.SetState(ActivityStatusCode.Ok);
            return null;
        }
    }
}
