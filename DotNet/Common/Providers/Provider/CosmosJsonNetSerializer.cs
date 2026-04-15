using Microsoft.Azure.Cosmos;
using Newtonsoft.Json;

namespace Provider;

/// <summary>
/// Custom Cosmos serializer that uses Newtonsoft.Json with configurable settings.
/// Enables StringEnumConverter so enums are stored as readable strings ("User")
/// instead of integers (0) in Cosmos DB documents.
/// </summary>
public class CosmosJsonNetSerializer : CosmosSerializer
{
    private readonly JsonSerializerSettings _settings;

    public CosmosJsonNetSerializer(JsonSerializerSettings settings)
    {
        _settings = settings;
    }

    public override T FromStream<T>(Stream stream)
    {
        using var reader = new StreamReader(stream);
        using var jsonReader = new JsonTextReader(reader);
        var serializer = JsonSerializer.Create(_settings);
        return serializer.Deserialize<T>(jsonReader)!;
    }

    public override Stream ToStream<T>(T input)
    {
        var stream = new MemoryStream();
        using var writer = new StreamWriter(stream, leaveOpen: true);
        using var jsonWriter = new JsonTextWriter(writer);
        var serializer = JsonSerializer.Create(_settings);
        serializer.Serialize(jsonWriter, input);
        jsonWriter.Flush();
        writer.Flush();
        stream.Position = 0;
        return stream;
    }
}