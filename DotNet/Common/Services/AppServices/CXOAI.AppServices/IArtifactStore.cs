namespace CXOAI.AppServices;

/// <summary>
/// Abstraction for storing and retrieving reporting documents in blob storage.
/// </summary>
public interface IArtifactStore
{
    /// <summary>
    /// Uploads a document to blob storage and returns the blob URI.
    /// </summary>
    Task<string> StoreAsync(string blobName, byte[] data, string contentType, CancellationToken ct = default);

    /// <summary>
    /// Downloads a document from blob storage. Returns null if the blob does not exist.
    /// </summary>
    Task<(byte[] Data, string ContentType)?> RetrieveAsync(string blobName, CancellationToken ct = default);
}
