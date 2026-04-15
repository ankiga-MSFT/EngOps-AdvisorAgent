using System.Text.Json.Serialization;

namespace AdvisorAgent.Core.Models;

public sealed class ConversationTurn
{
    [JsonPropertyName("prompt")]
    public string Prompt { get; set; } = string.Empty;

    [JsonPropertyName("response")]
    public string Response { get; set; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    [JsonPropertyName("requestId")]
    public string RequestId { get; set; } = string.Empty;
}
