using Azure.AI.OpenAI;
using Azure.Core;
using Microsoft.Extensions.Logging;
using OpenAI.Embeddings;

namespace CXOAI.Memory;

/// <summary>
/// Generates vector embeddings for facts and queries using Azure OpenAI.
/// </summary>
public class MemoryEmbedder
{
    private readonly EmbeddingClient _embeddingClient;
    private readonly int _dimensions;
    private readonly ILogger<MemoryEmbedder> _logger;

    public MemoryEmbedder(string endpoint, TokenCredential credential, ILogger<MemoryEmbedder> logger, string deploymentName = "text-embedding-3-small", int dimensions = 512)
    {
        _logger = logger;
        _dimensions = dimensions;
        var client = new AzureOpenAIClient(new Uri(endpoint), credential);
        _embeddingClient = client.GetEmbeddingClient(deploymentName);
    }

    public async Task<float[]> EmbedAsync(string text)
    {
        var options = new EmbeddingGenerationOptions { Dimensions = _dimensions };
        var result = await _embeddingClient.GenerateEmbeddingAsync(text, options);
        return result.Value.ToFloats().ToArray();
    }

    public async Task<List<float[]>> EmbedBatchAsync(List<string> texts)
    {
        _logger.LogDebug("Embedding batch of {Count} texts", texts.Count);
        var options = new EmbeddingGenerationOptions { Dimensions = _dimensions };
        var result = await _embeddingClient.GenerateEmbeddingsAsync(texts, options);
        return result.Value.Select(e => e.ToFloats().ToArray()).ToList();
    }

    public static float CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length)
            return 0f;

        float dot = 0f, magA = 0f, magB = 0f;
        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            magA += a[i] * a[i];
            magB += b[i] * b[i];
        }

        var denom = MathF.Sqrt(magA) * MathF.Sqrt(magB);
        return denom == 0f ? 0f : dot / denom;
    }
}
