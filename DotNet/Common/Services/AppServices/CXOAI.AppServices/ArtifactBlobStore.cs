using Azure.Core;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging;

namespace CXOAI.AppServices;

/// <summary>
/// Stores and retrieves artifact documents in Azure Blob Storage using managed identity.
/// </summary>
public class ArtifactBlobStore : IArtifactStore
{
    private readonly BlobContainerClient _container;
    private readonly ILogger<ArtifactBlobStore> _logger;

    public ArtifactBlobStore(Uri endpoint, string containerName, ILogger<ArtifactBlobStore> logger)
    {
        _logger = logger;
        TokenCredential credential = null! ;
#if DEBUG
        credential = new VisualStudioCredential();
#else
        credential= new ManagedIdentityCredential();
#endif

        var serviceClient = new BlobServiceClient(endpoint, credential);
        _container = serviceClient.GetBlobContainerClient(containerName);
    }

    public async Task<string> StoreAsync(string blobName, byte[] data, string contentType, CancellationToken ct = default)
    {
        var blobClient = _container.GetBlobClient(blobName);
        using var stream = new MemoryStream(data);
        await blobClient.UploadAsync(
            stream,
            new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders { ContentType = contentType }
            },
            cancellationToken: ct);

        _logger.LogInformation("Stored blob: {BlobName} ({Size} bytes)", blobName, data.Length);
        return blobClient.Uri.ToString();
    }

    public async Task<(byte[] Data, string ContentType)?> RetrieveAsync(string blobName, CancellationToken ct = default)
    {
        var blobClient = _container.GetBlobClient(blobName);

        try
        {
            var download = await blobClient.DownloadContentAsync(ct);
            var data = download.Value.Content.ToArray();
            var downloadedContentType = download.Value.Details.ContentType;

            _logger.LogInformation("Retrieved blob: {BlobName} ({Size} bytes)", blobName, data.Length);
            return (data, downloadedContentType);
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogWarning("Blob not found: {BlobName}", blobName);
            return null;
        }
    }
}
