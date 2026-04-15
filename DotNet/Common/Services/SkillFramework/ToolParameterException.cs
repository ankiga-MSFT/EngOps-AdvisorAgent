namespace CXOAI.SkillFramework;

public class ToolParameterException : Exception
{
    public string Message { get; }
    public ToolParameterException(string error)
        : base(error) { }
    

    public string ToPromptMessage() =>
        $"Tool error: '{Message}'";
}
