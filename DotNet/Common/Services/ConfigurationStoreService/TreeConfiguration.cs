using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CXOAI.ConfigurationStore;

public class TreeConfiguration
{
    [SimpleField(IsKey = true, IsFilterable = true, IsSortable = true)] 
    public string Id
    {
        get => $"{ComponentName}-{ConfigurationName}";        
    }

    [VectorSearchField(VectorSearchDimensions = 1536, VectorSearchProfileName = "default-vector-profile")]
    public IReadOnlyList<float>? Embedding { get; set; }=new List<float>();

    [SearchableField(IsFilterable = true, IsSortable = true, IsFacetable = true)]
    public string? ComponentName { get; set; }

    [SearchableField(IsFilterable = true, IsSortable = true, IsFacetable = true)]
    public string? ConfigurationName { get; set; }

    [SearchableField(IsFilterable = true, IsSortable = true, IsFacetable = true)]
    public string? Description { get; set; }

    [SearchableField(IsFilterable = true, IsSortable = true, IsFacetable = true)]
    public string? Configuration { get; set; }

    [Newtonsoft.Json.JsonConverter(typeof(StringOrArrayConverter<DependsOnEntry>))]
    public IList<DependsOnEntry>? DependsOn { get; set; }=new List<DependsOnEntry>();

    [SimpleField(IsFilterable = true, IsSortable = true, IsFacetable = true)]
    public DateTimeOffset? ModifiedOn { get; set; }=DateTimeOffset.UtcNow;
}

public class DependsOnEntry
{
    [SearchableField(IsFilterable = true, IsFacetable = true)]
    public string? ComponentName { get; set; }

    [SearchableField(IsFilterable = true, IsFacetable = true)]
    public string? ConfigurationName { get; set; }
}

/// <summary>
/// Handles deserialization when a JSON property is stored as either
/// a real JSON array or a stringified JSON array.
/// </summary>
public class StringOrArrayConverter<T> : Newtonsoft.Json.JsonConverter<IList<T>?>
{
    public override IList<T>? ReadJson(JsonReader reader, Type objectType, IList<T>? existingValue, bool hasExistingValue, Newtonsoft.Json.JsonSerializer serializer)
    {
        var token = JToken.Load(reader);

        return token.Type switch
        {
            JTokenType.Array => token.ToObject<List<T>>(serializer),
            JTokenType.String => Newtonsoft.Json.JsonConvert.DeserializeObject<List<T>>(token.Value<string>()!),
            JTokenType.Null => null,
            _ => null
        };
    }

    public override void WriteJson(JsonWriter writer, IList<T>? value, Newtonsoft.Json.JsonSerializer serializer)
    {
        serializer.Serialize(writer, value);
    }
}