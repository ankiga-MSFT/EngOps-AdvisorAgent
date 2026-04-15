using CXOAI.ConfigurationStore;
using InfraService.OpenTelemetryProvider;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Text.Json;

namespace CXOAI.Functions.Triggers;

/// <summary>
/// Cosmos DB Change Feed trigger that syncs configuration documents from Cosmos DB
/// into the Azure AI Search index (cxoaiconfigurationstore).
///
/// Flow: Cosmos DB (configurations container) ? Change Feed ? this function
///       ? generates embedding ? upserts into Search index.
///
/// Each document in the change feed is one row from SeedData.json (a TreeConfiguration).
/// The <see cref="ITreeConfigurationStoreProvider"/> handles embedding generation
/// and search index upsert via <see cref="ITreeConfigurationStoreProvider.UploadDocumentAsync"/>.
/// </summary>
public class ConfigurationStoreChangeFeedTrigger
{
    private readonly ITreeConfigurationStoreProvider _configStoreProvider;
    private readonly ILogger<ConfigurationStoreChangeFeedTrigger> _logger;
    private readonly IMetricsProvider? _metricsProvider;

    public ConfigurationStoreChangeFeedTrigger(
        ITreeConfigurationStoreProvider configStoreProvider,
        ILogger<ConfigurationStoreChangeFeedTrigger> logger,
        IMetricsProvider? metricsProvider = null)
    {
        _configStoreProvider = configStoreProvider;
        _logger = logger;
        _metricsProvider = metricsProvider;
    }

    [Function(nameof(SyncConfigurationToSearchIndex))]
    public async Task SyncConfigurationToSearchIndex(
        [CosmosDBTrigger(
            databaseName: "%ConfigurationStoreDatabase%",
            containerName: "%ConfigurationStoreCollection%",
            Connection = "ConfigurationStoreConnection",
            LeaseContainerName = "%ConfigurationStoreLeaseCollection%",
            CreateLeaseContainerIfNotExists = true,StartFromBeginning =true)]
        IReadOnlyList<JsonElement> documents)
    {
        if (documents == null || documents.Count == 0)
        {
            _logger.LogDebug("Change feed invoked with no documents, skipping");
            return;
        }

        _logger.LogInformation("ConfigurationStore change feed triggered with {Count} document(s)", documents.Count);

        using var batchLatency = _metricsProvider?.LatencyMeasureOperation(MetricNames.ChangeFeedSync,
            new KeyValuePair<string, object?>(MetricNames.TagOperationName, "ConfigurationStore"));
        var successCount = 0;
        var failCount = 0;

        foreach (var document in documents)
        {
            string? componentName = null;
            string? configurationName = null;

            try
            {
                var json = document.GetRawText();
                var config = JsonConvert.DeserializeObject<TreeConfiguration>(json);

                if (config == null)
                {
                    _logger.LogWarning("Failed to deserialize change feed document, skipping. Raw: {Json}",
                        json.Length > 500 ? json[..500] + "..." : json);
                    failCount++;
                    continue;
                }

                componentName = config.ComponentName;
                configurationName = config.ConfigurationName;

                if (string.IsNullOrWhiteSpace(componentName) || string.IsNullOrWhiteSpace(configurationName))
                {
                    _logger.LogWarning("Document missing ComponentName or ConfigurationName, skipping. Id: {Id}",
                        config.Id);
                    failCount++;
                    continue;
                }

                _logger.LogInformation(
                    "Processing change feed document: ComponentName={ComponentName}, ConfigurationName={ConfigurationName}",
                    componentName, configurationName);

                await _configStoreProvider.UploadDocumentAsync(config);

                _logger.LogInformation(
                    "Successfully synced to search index: ComponentName={ComponentName}, ConfigurationName={ConfigurationName}",
                    componentName, configurationName);
                successCount++;
                _metricsProvider?.TrackSuccessCounterMetric(MetricNames.ChangeFeedSync, 1,
                    new KeyValuePair<string, object?>(MetricNames.TagOperationName, "ConfigurationStore"));
                _metricsProvider?.TrackAvailabilityMetric(MetricNames.ChangeFeedSync, 1, null,
                    new KeyValuePair<string, object?>(MetricNames.TagOperationName, "ConfigurationStore"));
            }
            catch (Exception ex)
            {
                failCount++;
                _metricsProvider?.TrackFailureCounterMetric(MetricNames.ChangeFeedSync, 1, ex,
                    new KeyValuePair<string, object?>(MetricNames.TagOperationName, "ConfigurationStore"));
                _metricsProvider?.TrackAvailabilityMetric(MetricNames.ChangeFeedSync, 1, ex,
                    new KeyValuePair<string, object?>(MetricNames.TagOperationName, "ConfigurationStore"));
                _logger.LogError(ex,
                    "Failed to sync document to search index: ComponentName={ComponentName}, ConfigurationName={ConfigurationName}",
                    componentName ?? "unknown", configurationName ?? "unknown");
            }
        }

        _logger.LogInformation(
            "ConfigurationStore change feed batch complete. Success={SuccessCount}, Failed={FailCount}, Total={TotalCount}",
            successCount, failCount, documents.Count);

        batchLatency?.SetState(failCount == 0 ? ActivityStatusCode.Ok : ActivityStatusCode.Error);
    }
}
