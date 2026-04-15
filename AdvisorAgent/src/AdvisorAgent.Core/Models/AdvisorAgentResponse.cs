using System.Text.Json.Serialization;

namespace AdvisorAgent.Core.Models;

/// <summary>
/// Unified response returned by skill execution and the orchestrator.
/// </summary>
public sealed class AdvisorAgentResponse
{
    [JsonPropertyName("isSuccess")]
    public bool IsSuccess { get; set; }

    [JsonPropertyName("response")]
    public string Response { get; set; } = string.Empty;

    [JsonPropertyName("needsUserInput")]
    public bool NeedsUserInput { get; set; }

    /// <summary>
    /// UI action type hint. When set, the frontend renders a specialized interactive card
    /// instead of plain markdown. Supported values: "subscriptionPicker".
    /// </summary>
    [JsonPropertyName("uiAction")]
    public string? UiAction { get; set; }

    /// <summary>
    /// Structured payload for the UI action (e.g., subscription list for the picker card).
    /// Serialized to JSON so the frontend can parse and render interactive controls.
    /// </summary>
    [JsonPropertyName("uiData")]
    public object? UiData { get; set; }

    [JsonPropertyName("category")]
    public string? Category { get; set; }

    public static AdvisorAgentResponse Success(string response, string? category = null) =>
        new() { IsSuccess = true, Response = response, Category = category };

    public static AdvisorAgentResponse Failure(string reason) =>
        new() { IsSuccess = false, Response = reason };
}
