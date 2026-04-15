using Microsoft.Extensions.Logging;

namespace CXOAI.StatusNotifier;

public enum StepState
{
    Pending,
    Running,
    Completed,
    WaitingForInput,
    Failed
}

public class StepStatus
{
    public string StepName { get; set; } = string.Empty;
    public StepState State { get; set; } = StepState.Pending;
    public string? Message { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public double? DurationMs => CompletedAt.HasValue && StartedAt.HasValue
        ? (CompletedAt.Value - StartedAt.Value).TotalMilliseconds
        : null;
}

public class SkillExecutionStatus
{
    public string SkillName { get; set; } = string.Empty;
    public StepState State { get; set; } = StepState.Pending;
    public string? Message { get; set; }
    public int UserInputRound { get; set; }
    public string? UserPrompt { get; set; }
}

public class OrchestratorStatus
{
    public string SessionId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string OriginalPrompt { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public string CurrentStep { get; set; } = string.Empty;
    public List<StepStatus> Steps { get; set; } = [];
    public List<SkillExecutionStatus> SkillExecutions { get; set; } = [];

    public void BeginStep(string stepName, string? message = null)
    {
        CurrentStep = stepName;
        var step = Steps.FirstOrDefault(s => s.StepName == stepName);
        if (step == null)
        {
            step = new StepStatus { StepName = stepName };
            Steps.Add(step);
        }
        step.State = StepState.Running;
        step.Message = message;
        step.StartedAt = DateTime.UtcNow;
    }

    public void CompleteStep(string stepName, string? message = null)
    {
        var step = Steps.FirstOrDefault(s => s.StepName == stepName);
        if (step != null)
        {
            step.State = StepState.Completed;
            step.Message = message;
            step.CompletedAt = DateTime.UtcNow;
        }
    }

    public void FailStep(string stepName, string? message = null)
    {
        var step = Steps.FirstOrDefault(s => s.StepName == stepName);
        if (step != null)
        {
            step.State = StepState.Failed;
            step.Message = message;
            step.CompletedAt = DateTime.UtcNow;
        }
    }

    public void BeginSkill(string skillName, string? message = null)
    {
        var skill = SkillExecutions.FirstOrDefault(s => s.SkillName == skillName);
        if (skill == null)
        {
            skill = new SkillExecutionStatus { SkillName = skillName };
            SkillExecutions.Add(skill);
        }
        skill.State = StepState.Running;
        skill.Message = message;
    }

    public void CompleteSkill(string skillName, string? message = null)
    {
        var skill = SkillExecutions.FirstOrDefault(s => s.SkillName == skillName);
        if (skill != null)
        {
            skill.State = StepState.Completed;
            skill.Message = message;
        }
    }

    public void SkillNeedsInput(string skillName, string userPrompt, int round)
    {
        var skill = SkillExecutions.FirstOrDefault(s => s.SkillName == skillName);
        if (skill != null)
        {
            skill.State = StepState.WaitingForInput;
            skill.UserPrompt = userPrompt;
            skill.UserInputRound = round;
        }
    }

    public void FailedSkill(string skillName, string userPrompt, int round)
    {
        var skill = SkillExecutions.FirstOrDefault(s => s.SkillName == skillName);
        if (skill != null)
        {
            skill.State = StepState.Failed;
            skill.UserPrompt = userPrompt;
            skill.UserInputRound = round;
        }
    }

    public void PrintToConsole(ILogger logger)
    {
        logger.LogInformation("");
        logger.LogInformation("+----------------------------------------------------------+");
        logger.LogInformation("|  Session: {SessionId}|", SessionId.PadRight(47));
        logger.LogInformation("|  Prompt:  {PromptPreview}|", OriginalPrompt[..Math.Min(47, OriginalPrompt.Length)].PadRight(47));
        logger.LogInformation("+----------------------------------------------------------+");

        foreach (var step in Steps)
        {
            var icon = step.State switch
            {
                StepState.Pending => "[ ]",
                StepState.Running => "[~]",
                StepState.Completed => "[x]",
                StepState.WaitingForInput => "[?]",
                StepState.Failed => "[!]",
                _ => "   "
            };
            var duration = step.DurationMs.HasValue ? $" ({step.DurationMs:F0}ms)" : "";
            var msg = step.Message != null ? $" - {step.Message}" : "";
            logger.LogInformation("|  {Icon} {StepName} {State}{Duration}{Message}", icon, step.StepName.PadRight(20), step.State.ToString().PadRight(15), duration, msg);
        }

        if (SkillExecutions.Count > 0)
        {
            logger.LogInformation("+----------------------------------------------------------+");
            logger.LogInformation("|  Skill Execution:                                        |");
            foreach (var skill in SkillExecutions)
            {
                var icon = skill.State switch
                {
                    StepState.Pending => "[ ]",
                    StepState.Running => "[~]",
                    StepState.Completed => "[x]",
                    StepState.WaitingForInput => "[?]",
                    StepState.Failed => "[!]",
                    _ => "   "
                };
                var extra = skill.State == StepState.WaitingForInput
                    ? $" (round {skill.UserInputRound}: {skill.UserPrompt})"
                    : skill.Message != null ? $" - {skill.Message}" : "";
                logger.LogInformation("|    {Icon} {SkillName} {State}{Extra}", icon, skill.SkillName.PadRight(18), skill.State.ToString().PadRight(15), extra);
            }
        }

        logger.LogInformation("+----------------------------------------------------------+");
    }
}
