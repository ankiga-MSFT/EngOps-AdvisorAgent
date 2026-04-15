using AdvisorAgent.Core.Models;
using AdvisorAgent.Core.Skills;
using Xunit;

namespace AdvisorAgent.Tests;

public class TaskPlanValidatorTests
{
    [Fact]
    public void RemoveUnknownSkills_FiltersUnknownAndReindexes()
    {
        var plan = new List<TaskPlanItem>
        {
            new() { Task = "Task A", SkillName = "RetirementSkill", DependsOn = [] },
            new() { Task = "Task B", SkillName = "UnknownSkill", DependsOn = [0] },
            new() { Task = "Task C", SkillName = "ResiliencySkill", DependsOn = [0, 1] },
        };

        var known = new HashSet<string> { "RetirementSkill", "ResiliencySkill" };
        var result = PlanValidator.RemoveUnknownSkills(plan, known);

        Assert.Equal(2, result.Count);
        Assert.Equal("RetirementSkill", result[0].SkillName);
        Assert.Equal("ResiliencySkill", result[1].SkillName);
        // Task C depended on index 0 (kept) and 1 (removed), so should only depend on 0
        Assert.Single(result[1].DependsOn);
        Assert.Equal(0, result[1].DependsOn[0]);
    }

    [Fact]
    public void TopologicalSort_ReturnsCorrectOrder()
    {
        var plan = new List<TaskPlanItem>
        {
            new() { Task = "Task A", SkillName = "A", DependsOn = [] },
            new() { Task = "Task B", SkillName = "B", DependsOn = [0] },
            new() { Task = "Task C", SkillName = "C", DependsOn = [0] },
            new() { Task = "Task D", SkillName = "D", DependsOn = [1, 2] },
        };

        var order = PlanValidator.TopologicalSort(plan);

        Assert.Equal(4, order.Count);
        Assert.Equal(0, order[0]); // A first (no deps)
        Assert.Equal(3, order[^1]); // D last (depends on B and C)
        // B and C before D
        Assert.True(order.IndexOf(1) < order.IndexOf(3));
        Assert.True(order.IndexOf(2) < order.IndexOf(3));
    }

    [Fact]
    public void TopologicalSort_ThrowsOnCycle()
    {
        var plan = new List<TaskPlanItem>
        {
            new() { Task = "Task A", SkillName = "A", DependsOn = [1] },
            new() { Task = "Task B", SkillName = "B", DependsOn = [0] },
        };

        Assert.Throws<InvalidOperationException>(() => PlanValidator.TopologicalSort(plan));
    }
}
