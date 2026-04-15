using System.Text.RegularExpressions;

namespace CXOAI.SkillFramework;

/// <summary>
/// Layer 2 fallback cache: extracts a parameter name from [NEED_INPUT] questions
/// via regex and stores the user's answer keyed by that parameter name.
/// If a later task asks for the same parameter, the cached answer is returned
/// without prompting the user again.
///
/// Layer 1 (context injection of Q&amp;A pairs into the prompt) handles most cases;
/// this cache only fires when the LLM ignores the injected context.
/// Scoped per orchestration run — not persisted across runs.
/// </summary>
public class UserInputCache
{
    private readonly Dictionary<string, string> _paramCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Tries to find a cached answer by extracting the parameter name from the question.
    /// Returns the cached answer or null on miss.
    /// </summary>
    public string? TryGetAnswer(string question)
    {
        var paramKey = ExtractParameterKey(question);
        if (paramKey != null && _paramCache.TryGetValue(paramKey, out var answer))
            return answer;

        return null;
    }

    /// <summary>
    /// Stores a question-answer pair. Extracts the parameter name from the question
    /// and uses it as the cache key. If extraction fails, the pair is silently skipped
    /// (no harm — Layer 1 context injection still carries the Q&amp;A forward).
    /// </summary>
    public void Store(string question, string answer)
    {
        var paramKey = ExtractParameterKey(question);
        if (paramKey != null)
            _paramCache[paramKey] = answer;
    }

    // Patterns that capture the parameter name from typical [NEED_INPUT] questions.
    // The skill system prompt instructs the LLM to use [NEED_INPUT] when a tool returns
    // an error about a missing parameter, producing predictable question structures like:
    //   "Please provide the TPID for Walmart"
    //   "What is the Subscription ID?"
    //   "Could you specify the time range?"
    private static readonly Regex[] ParameterPatterns =
    [
        // "Please provide the Subscription ID for Walmart's IRMET data"
        new(@"(?:provide|specify|enter|give|supply)\s+(?:the\s+)?(.+?)(?:\s+for\s+|\s*\?|\.|\s*$)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled),

        // "What is the Account ID?"
        new(@"what\s+is\s+(?:the\s+)?(.+?)(?:\s+for\s+|\s*\?|\.|\s*$)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled),

        // "Missing the time range for this query"
        new(@"(?:need|require|missing)\s+(?:the\s+)?(.+?)(?:\s+for\s+|\s*\?|\.|\s*$)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled),
    ];

    /// <summary>
    /// Extracts a parameter key from the question text using regex.
    /// Returns a lowercased key (e.g., "tpid", "subscription id") or null if extraction fails.
    /// </summary>
    private static string? ExtractParameterKey(string question)
    {
        // Strip the [NEED_INPUT] marker before matching
        var clean = question
            .Replace(CXOAgentResponse.NeedInputMarker, "", StringComparison.OrdinalIgnoreCase)
            .Trim();

        foreach (var pattern in ParameterPatterns)
        {
            var match = pattern.Match(clean);
            if (match.Success)
            {
                var param = match.Groups[1].Value.Trim().ToLowerInvariant();
                // Ignore overly long extractions (likely captured a full sentence, not a param name)
                if (param.Length > 0 && param.Length <= 40)
                    return param;
            }
        }

        return null;
    }
}
