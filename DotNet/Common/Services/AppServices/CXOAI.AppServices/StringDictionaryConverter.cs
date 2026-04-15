using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CXOAI.AppServices;

/// <summary>
/// Handles nested JSON objects by serializing them as raw JSON strings in the dictionary.
/// A config like { "key": { "nested": true } } becomes { "key": "{\"nested\":true}" }.
/// String values are kept as-is.
/// </summary>
public class StringDictionaryConverter : JsonConverter
{
    public override bool CanConvert(Type objectType)
    {
        return objectType == typeof(Dictionary<string, string>);
    }

    public override object ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        var obj = JObject.Load(reader);
        var dict = new Dictionary<string, string>();

        foreach (var property in obj.Properties())
        {
            var value = property.Value.Type == JTokenType.String
                ? property.Value.ToString()
                : property.Value.ToString(Formatting.None);

            dict.Add(property.Name, value);
        }

        return dict;
    }

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        if (value is IDictionary<string, string> dict)
        {
            writer.WriteStartObject();
            foreach (var kvp in dict)
            {
                writer.WritePropertyName(kvp.Key);
                writer.WriteRawValue(kvp.Value);
            }
            writer.WriteEndObject();
        }
    }
}
