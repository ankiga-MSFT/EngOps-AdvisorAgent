using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Logging;

namespace AdvisorAgent.Tools;

/// <summary>
/// Base class for all Advisor Agent tools providing logging, session awareness,
/// and ARM REST API client helpers for calling management.azure.com.
/// </summary>
public abstract class ToolBase
{
    protected readonly ILogger Logger;
    protected readonly HttpClient HttpClient;
    protected string? SessionId { get; private set; }
    protected string? AccessToken { get; private set; }

    private const string ArmBaseUrl = "https://management.azure.com";

    /// <summary>
    /// Maximum characters to return from a single tool invocation.
    /// Prevents individual tool responses from consuming too much of the LLM context window.
    /// 15 000 chars ≈ 3 750 tokens — with 5 tool calls per skill that's ~19K tokens for tool results.
    /// </summary>
    protected const int MaxToolResponseChars = 15_000;

    protected ToolBase(ILogger logger, HttpClient httpClient)
    {
        Logger = logger;
        HttpClient = httpClient;
    }

    public void SetSession(string sessionId) => SessionId = sessionId;

    public void SetAccessToken(string? accessToken) => AccessToken = accessToken;

    /// <summary>
    /// Sends a GET request to an ARM REST API endpoint.
    /// Accepts a full URL or a path relative to https://management.azure.com.
    /// </summary>
    protected async Task<string> ArmGetAsync(string urlOrPath)
    {
        EnsureAccessToken();

        var url = urlOrPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            ? urlOrPath
            : $"{ArmBaseUrl}{urlOrPath}";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);

        Logger.LogInformation("[ARM GET] >>> {Url}", url);
        var response = await HttpClient.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Logger.LogInformation("[ARM GET] <<< {StatusCode} {Url} ({Length} chars)",
            (int)response.StatusCode, url, body.Length);
        Logger.LogDebug("[ARM GET] Response body: {Body}", Truncate(body));

        if (!response.IsSuccessStatusCode)
        {
            Logger.LogWarning("[ARM GET] FAILED {StatusCode} for {Url}: {Body}",
                (int)response.StatusCode, url, Truncate(body));
        }

        response.EnsureSuccessStatusCode();
        return body;
    }

    /// <summary>
    /// Sends a POST request to an ARM REST API endpoint with a JSON body.
    /// Accepts a full URL or a path relative to https://management.azure.com.
    /// </summary>
    protected async Task<string> ArmPostAsync(string urlOrPath, string jsonBody)
    {
        EnsureAccessToken();

        var url = urlOrPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            ? urlOrPath
            : $"{ArmBaseUrl}{urlOrPath}";

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);
        request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

        Logger.LogInformation("[ARM POST] >>> {Url}", url);
        Logger.LogDebug("[ARM POST] Request body: {RequestBody}", Truncate(jsonBody));
        var response = await HttpClient.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Logger.LogInformation("[ARM POST] <<< {StatusCode} {Url} ({Length} chars)",
            (int)response.StatusCode, url, body.Length);
        Logger.LogDebug("[ARM POST] Response body: {Body}", Truncate(body));

        if (!response.IsSuccessStatusCode)
        {
            Logger.LogWarning("[ARM POST] FAILED {StatusCode} for {Url}: {Body}",
                (int)response.StatusCode, url, Truncate(body));
        }

        response.EnsureSuccessStatusCode();
        return body;
    }

    /// <summary>
    /// Executes a KQL query against Azure Resource Graph (api-version 2024-04-01).
    /// Tables: resources, advisorresources, servicehealthresources, etc.
    /// When <paramref name="subscriptionIds"/> is non-empty the query is scoped to those subscriptions;
    /// when null/empty it runs as a tenant-level query.
    /// </summary>
    protected async Task<string> ResourceGraphQueryAsync(string kql, IEnumerable<string>? subscriptionIds = null)
    {
        var subs = subscriptionIds?.Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
        string jsonBody;

        if (subs is { Length: > 0 })
        {
            var subsArray = string.Join(",", subs.Select(s => $"\"{s}\""));
            jsonBody = $$"""{"query":"{{EscapeJsonString(kql)}}","subscriptions":[{{subsArray}}]}""";
        }
        else
        {
            jsonBody = $$"""{"query":"{{EscapeJsonString(kql)}}"}""";
        }

        Logger.LogInformation("[ARG] >>> KQL: {Kql} | Subscriptions: {Subs}", kql, subs?.Length ?? 0);
        var result = await ArmPostAsync("/providers/Microsoft.ResourceGraph/resources?api-version=2024-04-01", jsonBody);
        return TruncateToolResponse(result, "ARG query");
    }

    /// <summary>
    /// Truncates a tool response to <see cref="MaxToolResponseChars"/> to prevent blowing the LLM context window.
    /// Logs a warning when truncation occurs.
    /// </summary>
    protected string TruncateToolResponse(string response, string toolLabel)
    {
        if (response.Length <= MaxToolResponseChars)
            return response;

        Logger.LogWarning("[{Tool}] Response truncated from {Original} to {Max} chars to stay within LLM context budget",
            toolLabel, response.Length, MaxToolResponseChars);
        return string.Concat(response.AsSpan(0, MaxToolResponseChars),
            $"\n... [TRUNCATED — showing first {MaxToolResponseChars} of {response.Length} chars. Narrow your query or reduce subscription scope for complete results.]");
    }

    /// <summary>
    /// Parses a comma/semicolon-separated subscription IDs string into an array.
    /// </summary>
    protected static string[] ParseSubscriptionIds(string? subscriptionIds)
    {
        if (string.IsNullOrWhiteSpace(subscriptionIds))
            return [];

        return subscriptionIds
            .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>
    /// Extracts a subscription ID from a full ARM resource ID.
    /// Returns null if the format is not recognized.
    /// </summary>
    protected static string? ExtractSubscriptionId(string resourceId)
    {
        var parts = resourceId.Split('/');
        var idx = Array.IndexOf(parts, "subscriptions");
        return idx >= 0 && idx + 1 < parts.Length ? parts[idx + 1] : null;
    }

    /// <summary>Escapes a string for safe embedding inside a JSON string literal.</summary>
    private static string EscapeJsonString(string value)
        => value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");

    /// <summary>Truncates a string for logging (max 2000 chars).</summary>
    private static string Truncate(string value, int max = 2000)
        => value.Length <= max ? value : string.Concat(value.AsSpan(0, max), "... [truncated]");

    private void EnsureAccessToken()
    {
        if (string.IsNullOrEmpty(AccessToken))
            throw new InvalidOperationException(
                "ARM access token not set. Ensure the caller provides a valid Bearer token for management.azure.com.");
    }
}
