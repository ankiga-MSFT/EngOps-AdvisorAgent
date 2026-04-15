using CXOAI.AppServices;
using CXOAI.ConfigurationStore;
using CXOAI.SkillFramework;
using CXOAI.StatusNotifier;
using CXOAI.Tools;
using DocumentFormat.OpenXml.Packaging;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace UnitTests;

/// <summary>
/// Tests blob upload (via ReportingTools.GenerateWordAsync) and download
/// (via the same blob store the ArtifactDownloadFunction falls back to).
/// Uses a fake in-memory blob store to run without Azure credentials.
/// </summary>
public class ArtifactBlobStoreTests
{
    private readonly FakeBlobStore _durableArtifactStore = new();
    private readonly ReportingTools _tools;
    private readonly ILogger<ArtifactBlobStoreTests> _logger;

    public ArtifactBlobStoreTests()
    {
        var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
        _logger = loggerFactory.CreateLogger<ArtifactBlobStoreTests>();
        _tools = new ReportingTools(
            loggerFactory.CreateLogger<ReportingTools>(),
            new StubStoreProvider(),
            new StubUserAuthContext(),
            new ConsoleToolStatusNotifier(),
            _durableArtifactStore);
    }

    [Fact]
    public async Task GenerateWord_UploadsToBlob_WithCorrectKeyAndContentType()
    {
        var result = await GenerateWordReport();

        Assert.True(result.IsSuccess);

        // Blob store should have exactly one entry.
        Assert.Single(_durableArtifactStore.Blobs);

        var entry = _durableArtifactStore.Blobs.First();
        var blobName = entry.Key;
        var (data, contentType) = entry.Value;

        // Key format: ReportingSkill/{fileName}
        Assert.StartsWith("ReportingSkill/Report_", blobName);
        Assert.EndsWith(".docx", blobName);
        Assert.Equal("application/vnd.openxmlformats-officedocument.wordprocessingml.document", contentType);
        Assert.True(data.Length > 0, "Blob data must not be empty");

        _logger.LogInformation("Blob uploaded — Key: {BlobName}, Size: {Size} bytes, ContentType: {ContentType}",
            blobName, data.Length, contentType);
    }

    [Fact]
    public async Task GenerateWord_PayloadDoesNotExposeBlobUri()
    {
        var result = await GenerateWordReport();

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Payload);

        var payload = (JObject)result.Payload;

        // BlobUri should NOT be returned in the payload — it is logged server-side only.
        Assert.Null(payload["BlobUri"]);

        var fileName = payload["FileName"]?.ToString();
        var downloadUrl = payload["DownloadUrl"]?.ToString();
        var sizeBytes = payload["SizeBytes"]?.Value<long>();

        Assert.False(string.IsNullOrWhiteSpace(fileName));
        Assert.False(string.IsNullOrWhiteSpace(downloadUrl));
        Assert.True(sizeBytes > 0);

        // Blob store should still have received the upload (verified via fake).
        Assert.Single(_durableArtifactStore.Blobs);

        _logger.LogInformation(
            "Payload details — FileName: {FileName}, DownloadUrl: {DownloadUrl}, SizeBytes: {Size}",
            fileName, downloadUrl, sizeBytes);
    }

    [Fact]
    public async Task RetrieveFromBlob_ReturnsUploadedDocument()
    {
        var result = await GenerateWordReport();
        Assert.True(result.IsSuccess);

        // Extract the blob key from what was stored.
        var blobName = _durableArtifactStore.Blobs.First().Key;

        // Retrieve via the same interface the download endpoint uses.
        var retrieved = await _durableArtifactStore.RetrieveAsync(blobName);

        Assert.NotNull(retrieved);
        var (data, contentType) = retrieved.Value;

        Assert.Equal("application/vnd.openxmlformats-officedocument.wordprocessingml.document", contentType);
        Assert.True(data.Length > 0);

        // Validate it's a real .docx.
        using var ms = new MemoryStream(data);
        using var doc = WordprocessingDocument.Open(ms, false);
        var body = doc.MainDocumentPart?.Document?.Body;
        Assert.NotNull(body);
        Assert.Contains("Walmart", body.InnerText);

        _logger.LogInformation("Retrieved from blob — Key: {BlobName}, Size: {Size} bytes, Valid .docx: true", blobName, data.Length);
    }

    [Fact]
    public async Task RetrieveFromBlob_NonExistentKey_ReturnsNull()
    {
        var result = await _durableArtifactStore.RetrieveAsync("ReportingSkill/NonExistent.docx");
        Assert.Null(result);

        _logger.LogInformation("Retrieve non-existent blob correctly returned null");
    }

    [Fact]
    public async Task DownloadFallback_ServesFromBlob()
    {
        // Generate Word doc — stored in blob.
        var result = await GenerateWordReport();
        Assert.True(result.IsSuccess);

        var payload = (JObject)result.Payload!;
        var downloadUrl = payload["DownloadUrl"]!.ToString();

        // Parse the route params the same way ArtifactDownloadFunction does.
        var segments = downloadUrl.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var skillName = segments[^2];
        var fileName = segments[^1];
        var artifactKey = $"{skillName}/{fileName}";

        // Blob has it — this is the primary path.
        var blob = await _durableArtifactStore.RetrieveAsync(artifactKey);
        Assert.NotNull(blob);
        Assert.True(blob.Value.Data.Length > 0);
        Assert.Equal("application/vnd.openxmlformats-officedocument.wordprocessingml.document", blob.Value.ContentType);

        _logger.LogInformation(
            "Download test — Blob served: Key={Key}, Size={Size} bytes, ContentType={CT}",
            artifactKey, blob.Value.Data.Length, blob.Value.ContentType);
    }

    // ═══════════════════════════════════════════════════════════════
    // Helpers
    // ═══════════════════════════════════════════════════════════════

    private async Task<CXOAgentResponse> GenerateWordReport()
    {
        return await _tools.GenerateWordAsync("ExecutiveSummary", """
                # Walmart — Support CSAT Executive Summary
                **Period**: Last 30 days

                ## Executive Summary
                Walmart's Support CSAT score has **declined to 72.45**.

                | Metric | Value |
                |---|---|
                | Support CSAT | 72.45 |
                """);
    }

    // ═══════════════════════════════════════════════════════════════
    // Fakes / Stubs
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// In-memory fake of IArtifactStore. Stores blobs in a dictionary,
    /// returns URIs matching real Azure Blob Storage URL format.
    /// </summary>
    private class FakeBlobStore : IArtifactStore
    {
        public Dictionary<string, (byte[] Data, string ContentType)> Blobs { get; } = new();

        public Task<string> StoreAsync(string blobName, byte[] data, string contentType, CancellationToken ct = default)
        {
            Blobs[blobName] = (data, contentType);
            var uri = $"https://fakestorage.blob.core.windows.net/fakecontainer/{blobName}";
            return Task.FromResult(uri);
        }

        public Task<(byte[] Data, string ContentType)?> RetrieveAsync(string blobName, CancellationToken ct = default)
        {
            if (Blobs.TryGetValue(blobName, out var entry))
                return Task.FromResult<(byte[] Data, string ContentType)?>(entry);
            return Task.FromResult<(byte[] Data, string ContentType)?>(null);
        }
    }

    private class StubStoreProvider : ITreeConfigurationStoreProvider
    {
        public Task<List<TreeConfiguration>> GetConfigurations(string componentName, bool needNestedConfigs)
            => Task.FromResult(new List<TreeConfiguration>());
        public Task<List<TreeConfiguration>> GetConfigurationsWithDescription(string componentName, string searchText, bool needNestedConfigs)
            => Task.FromResult(new List<TreeConfiguration>());
        public Task<List<TreeConfiguration>> GetConfigurationsWithNames(string componentName, List<string> configurationNames, bool needNestedConfigs)
            => Task.FromResult(new List<TreeConfiguration>());
        public Task<Azure.Response<Azure.Search.Documents.Models.IndexDocumentsResult>> UploadDocumentAsync(TreeConfiguration configStore)
            => throw new NotImplementedException();
    }

    private class StubUserAuthContext : IUserAuthContext
    {
        public string? AccessToken { get; set; }
        public string? UserObjectId { get; set; }
        public string? UserName { get; set; }
        public string? UserPrincipalName { get; set; }
    }
}
