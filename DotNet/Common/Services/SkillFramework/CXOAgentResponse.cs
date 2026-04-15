using System.Text.Json;
using System.Text.Json.Serialization;
using Newtonsoft.Json.Linq;

namespace CXOAI.SkillFramework;

public class CXOAgentResponse
{
    public bool IsSuccess { get; set; }
    public string Response { get; set; } = string.Empty;

    // User-input / external agent callback
    public bool NeedsInputForUser { get; set; }
    public const string NeedInputMarker = "[NEED_INPUT]";

    /// <summary>Opaque continuation token from an external agent.
    /// Round-tripped unchanged on resume. Set by tool code in C#, not by LLM JSON.
    /// Uses a custom converter so System.Text.Json can serialize/deserialize
    /// the Newtonsoft JObject without crashing.</summary>
    [JsonConverter(typeof(JObjectJsonConverter))]
    public JObject? Payload { get; set; }

    // UI hints (buttons/options for user-input scenarios)
    public bool IsUIComponent { get; set; }
    public string UIComponent { get; set; } = string.Empty;
    public bool IsReport { get; set; }

    /// <summary>Populated by the orchestrator when the prompt has multiple independent question groups.
    /// Null for single-question prompts � UI reads Response directly.
    /// Response contains merged markdown for backward compatibility.</summary>
    public List<GroupResult>? Groups { get; set; }
}

/// <summary>One independent question group's result.</summary>
public class GroupResult
{
    public int Group { get; set; }
    public string Label { get; set; } = string.Empty;
    public bool IsSuccess { get; set; }
    public string Response { get; set; } = string.Empty;

    public bool NeedsInputForUser { get; set; }

    [JsonConverter(typeof(JObjectJsonConverter))]
    public JObject? Payload { get; set; }

    public bool IsUIComponent { get; set; }
    public string UIComponent { get; set; } = string.Empty;

    public bool IsReport { get; set; }
}

/// <summary>
/// Bridges Newtonsoft <see cref="JObject"/> for System.Text.Json serialization.
/// Converts between the raw JSON text and JObject on read/write so the rest of
/// the codebase can keep using JObject indexers unchanged.
/// </summary>
public sealed class JObjectJsonConverter : JsonConverter<JObject?>
{
    public override JObject? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        using var doc = JsonDocument.ParseValue(ref reader);
        return JObject.Parse(doc.RootElement.GetRawText());
    }

    public override void Write(Utf8JsonWriter writer, JObject? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        using var doc = JsonDocument.Parse(value.ToString(Newtonsoft.Json.Formatting.None));
        doc.RootElement.WriteTo(writer);
    }
}
