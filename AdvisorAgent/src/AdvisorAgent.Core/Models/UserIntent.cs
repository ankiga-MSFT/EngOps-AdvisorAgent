using System.Text.Json.Serialization;

namespace AdvisorAgent.Core.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum UserIntentType
{
    Informational,
    ActionRequired,
    Unknown
}

public sealed class UserIntent
{
    [JsonPropertyName("intent")]
    public UserIntentType Intent { get; set; }

    [JsonPropertyName("reasoning")]
    public string Reasoning { get; set; } = string.Empty;
}
