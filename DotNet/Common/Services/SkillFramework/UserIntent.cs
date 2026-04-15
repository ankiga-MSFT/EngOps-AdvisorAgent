namespace CXOAI.SkillFramework;

public enum UserIntentType
{
    Informational,
    DataAction,
    Unknown
}

public class UserIntent
{
    public UserIntentType Intent { get; set; }
    public string Reasoning { get; set; } = string.Empty;
}

public class HistoryAnswerResult
{
    public bool CanAnswer { get; set; }
    public bool HasRelevantContext { get; set; }
    public string Answer { get; set; } = string.Empty;
    public string RelevantContext { get; set; } = string.Empty;
}
