using CXOAI.AppServices;
using InfraService.OpenTelemetryProvider;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Net;

namespace CXOAI.Functions.Triggers;

public class ArtifactDownloadFunction
{
    private readonly IArtifactStore? _durableArtifactStore;
    private readonly ILogger<ArtifactDownloadFunction> _logger;
    private readonly IMetricsProvider? _metricsProvider;

    public ArtifactDownloadFunction(ILogger<ArtifactDownloadFunction> logger, IArtifactStore? durableArtifactStore = null, IMetricsProvider? metricsProvider = null)
    {
        _durableArtifactStore = durableArtifactStore;
        _logger = logger;
        _metricsProvider = metricsProvider;
    }

    /// <summary>
    /// GET /api/artifacts/{fileName}
    /// Retrieves artifacts from blob storage (durable, cross-instance source of truth).
    /// Auth is handled by the existing MISE AuthenticationMiddleware in the Functions Worker pipeline.
    /// </summary>
    [Function("ArtifactDownload")]
    public async Task<HttpResponseData> DownloadArtifact(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "artifacts/{fileName}")] HttpRequestData req,
        string fileName,
        CancellationToken cancellationToken)
    {
        using var latency = _metricsProvider?.LatencyMeasureOperation(MetricNames.HttpTrigger,
            new KeyValuePair<string, object?>(MetricNames.TagOperationName, "ArtifactDownload"));
        _logger.LogInformation("Artifact download requested: {FileName}", fileName);

        if (_durableArtifactStore is not null)
        {
            try
            {
                var blob = await _durableArtifactStore.RetrieveAsync(fileName, cancellationToken);
                if (blob is not null)
                {
                    var response = req.CreateResponse(HttpStatusCode.OK);
                    response.Headers.Add("Content-Type", blob.Value.ContentType);
                    response.Headers.Add("Content-Length", blob.Value.Data.Length.ToString());
                    response.Headers.Add("Content-Disposition", $"attachment; filename=\"{fileName}\"");
                    await response.Body.WriteAsync(blob.Value.Data, cancellationToken);
                    _logger.LogInformation("Artifact served from blob: {FileName} ({Size} bytes)", fileName, blob.Value.Data.Length);
                    latency?.SetState(ActivityStatusCode.Ok);
                    _metricsProvider?.TrackCounterMetric(MetricNames.HttpTrigger, 1,
                        new KeyValuePair<string, object?>(MetricNames.TagOperationName, "ArtifactDownload"));
                    _metricsProvider?.TrackAvailabilityMetric(MetricNames.HttpTrigger, 1, null,
                        new KeyValuePair<string, object?>(MetricNames.TagOperationName, "ArtifactDownload"));
                    return response;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Blob retrieval failed for {FileName}.", fileName);
                latency?.SetState(ActivityStatusCode.Error);
                _metricsProvider?.TrackCounterMetric(MetricNames.HttpTrigger, 1,
                    new KeyValuePair<string, object?>(MetricNames.TagOperationName, "ArtifactDownload"));
                _metricsProvider?.TrackAvailabilityMetric(MetricNames.HttpTrigger, 1, ex,
                    new KeyValuePair<string, object?>(MetricNames.TagOperationName, "ArtifactDownload"));
            }
        }

        _logger.LogWarning("Artifact not found: {FileName}", fileName);
        var notFound = req.CreateResponse(HttpStatusCode.NotFound);
        await notFound.WriteStringAsync($"Artifact '{fileName}' not found.");
        return notFound;
    }
}
