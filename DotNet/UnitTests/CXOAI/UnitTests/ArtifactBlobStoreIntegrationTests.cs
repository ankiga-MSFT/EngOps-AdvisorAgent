using CXOAI.AppServices;
using CXOAI.ConfigurationStore;
using CXOAI.SkillFramework;
using CXOAI.StatusNotifier;
using CXOAI.Tools;
using DocumentFormat.OpenXml.Packaging;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;

namespace UnitTests;

/// <summary>
/// Unit tests that generate a Word document via <see cref="ReportingTools"/> and
/// verify it is stored/retrieved correctly using an in-memory <see cref="IArtifactStore"/>
/// stub — no network or Azure credentials required.
/// </summary>
public class ArtifactBlobStoreIntegrationTests
{
    private readonly InMemoryArtifactStore _durableStore = new();
    private readonly ReportingTools _tools;
    private readonly ILogger<ArtifactBlobStoreIntegrationTests> _logger;

    public ArtifactBlobStoreIntegrationTests()
    {
        var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
        _logger = loggerFactory.CreateLogger<ArtifactBlobStoreIntegrationTests>();

        _tools = new ReportingTools(
            loggerFactory.CreateLogger<ReportingTools>(),
            new StubStoreProvider(),
            new StubUserAuthContext(),
            new ConsoleToolStatusNotifier(),
            _durableStore);
    }

    [Fact]
    public async Task WordDoc_StoreToDurableStore_Succeeds()
    {
        var result = await GenerateWordReport();

        Assert.True(result.IsSuccess, "GenerateWordAsync should succeed.");

        var payload = (JObject)result.Payload!;
        var fileName = payload["FileName"]!.ToString();
        var sizeBytes = payload["SizeBytes"]!.Value<long>();

        Assert.EndsWith(".docx", fileName);
        Assert.True(sizeBytes > 0, "Document size must be > 0 bytes.");

        // Verify the document was stored in the durable store.
        var artifactKey = $"ReportingSkill/{fileName}";
        var retrieved = await _durableStore.RetrieveAsync(artifactKey);
        Assert.NotNull(retrieved);
        Assert.Equal(sizeBytes, retrieved.Value.Data.Length);

        _logger.LogInformation(
            "Store OK — FileName: {FileName}, Size: {Size} bytes, Durable store verified: true",
            fileName, sizeBytes);
    }

    [Fact]
    public async Task WordDoc_RetrieveFromDurableStore_ReturnsValidDocx()
    {
        var result = await GenerateWordReport();
        Assert.True(result.IsSuccess);

        var payload = (JObject)result.Payload!;
        var downloadUrl = payload["DownloadUrl"]!.ToString();
        var segments = downloadUrl.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var artifactKey = $"{segments[^2]}/{segments[^1]}";

        var retrieved = await _durableStore.RetrieveAsync(artifactKey);

        Assert.NotNull(retrieved);
        var (data, contentType) = retrieved.Value;

        Assert.Equal("application/vnd.openxmlformats-officedocument.wordprocessingml.document", contentType);
        Assert.True(data.Length > 0);

        // Validate it's a real, openable .docx with expected content.
        using var ms = new MemoryStream(data);
        using var wordDoc = WordprocessingDocument.Open(ms, false);
        var body = wordDoc.MainDocumentPart?.Document?.Body;
        Assert.NotNull(body);
        Assert.Contains("Walmart", body.InnerText);

        _logger.LogInformation(
            "Retrieve OK — Key: {Key}, Size: {Size} bytes, ContentType: {CT}, Valid .docx: true",
            artifactKey, data.Length, contentType);
    }

    [Fact]
    public async Task WordDoc_PayloadContainsExpectedFields()
    {
        var result = await GenerateWordReport();
        Assert.True(result.IsSuccess);
        Assert.True(result.IsUIComponent);

        var payload = (JObject)result.Payload!;
        Assert.NotNull(payload["FileName"]);
        Assert.NotNull(payload["ContentType"]);
        Assert.NotNull(payload["SizeBytes"]);
        Assert.NotNull(payload["DownloadUrl"]);

        Assert.Equal("application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            payload["ContentType"]!.ToString());
        Assert.StartsWith("/api/artifacts/ReportingSkill/", payload["DownloadUrl"]!.ToString());
    }

    [Fact]
    public async Task WordDoc_RetrieveAndSaveToDisk_ProducesOpenableFile()
    {
        var result = await GenerateWordReport();
        Assert.True(result.IsSuccess);

        var payload = (JObject)result.Payload!;
        var fileName = payload["FileName"]!.ToString();
        var artifactKey = $"ReportingSkill/{fileName}";

        // Retrieve the document bytes from the durable store.
        var retrieved = await _durableStore.RetrieveAsync(artifactKey);
        Assert.NotNull(retrieved);

        var (data, contentType) = retrieved.Value;
        Assert.Equal("application/vnd.openxmlformats-officedocument.wordprocessingml.document", contentType);

        // Write the file to a TestArtifacts folder next to the test assembly for easy access.
        var outputDir = Path.Combine(AppContext.BaseDirectory, "TestArtifacts");
        Directory.CreateDirectory(outputDir);
        var outputPath = Path.Combine(outputDir, fileName);
        await File.WriteAllBytesAsync(outputPath, data);

        Assert.True(File.Exists(outputPath), $"File should exist on disk at: {outputPath}");

        var fileInfo = new FileInfo(outputPath);
        Assert.True(fileInfo.Length > 0, "Downloaded file must not be empty.");

        // Open and validate the saved file is a well-formed .docx.
        using var fs = File.OpenRead(outputPath);
        using var wordDoc = WordprocessingDocument.Open(fs, false);
        var body = wordDoc.MainDocumentPart?.Document?.Body;
        Assert.NotNull(body);
        Assert.Contains("Walmart", body.InnerText);

        _logger.LogInformation(
            "Download OK — Saved to: {Path}, Size: {Size} bytes",
            outputPath, fileInfo.Length);
    }

    [Fact]
    public async Task WordDoc_EmptyMarkdown_ReturnsFailure()
    {
        var result = await _tools.GenerateWordAsync("ExecutiveSummary", "");

        Assert.False(result.IsSuccess);
        Assert.Contains("empty", result.Response, StringComparison.OrdinalIgnoreCase);
    }

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

    /// <summary>
    /// In-memory implementation of <see cref="IArtifactStore"/> for unit testing.
    /// </summary>
    private class InMemoryArtifactStore : IArtifactStore
    {
        private readonly ConcurrentDictionary<string, (byte[] Data, string ContentType)> _blobs = new();

        public Task<string> StoreAsync(string blobName, byte[] data, string contentType, CancellationToken ct = default)
        {
            _blobs[blobName] = (data, contentType);
            return Task.FromResult($"memory://{blobName}");
        }

        public Task<(byte[] Data, string ContentType)?> RetrieveAsync(string blobName, CancellationToken ct = default)
        {
            if (_blobs.TryGetValue(blobName, out var value))
                return Task.FromResult<(byte[] Data, string ContentType)?>(value);
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
