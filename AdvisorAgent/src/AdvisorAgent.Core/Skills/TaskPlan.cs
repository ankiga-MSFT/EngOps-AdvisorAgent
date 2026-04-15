using System.Text.Json.Serialization;

namespace AdvisorAgent.Core.Skills;

public sealed class TaskPlanItem
{
    [JsonPropertyName("task")]
    public string Task { get; set; } = string.Empty;

    [JsonPropertyName("skillName")]
    public string SkillName { get; set; } = string.Empty;

    [JsonPropertyName("dependsOn")]
    public List<int> DependsOn { get; set; } = [];
}

/// <summary>
/// Validates and topologically sorts a task plan DAG.
/// </summary>
public static class PlanValidator
{
    /// <summary>
    /// Removes tasks that reference unknown skills and re-indexes DependsOn references.
    /// </summary>
    public static List<TaskPlanItem> RemoveUnknownSkills(List<TaskPlanItem> plan, HashSet<string> knownSkills)
    {
        var indexMap = new Dictionary<int, int>();
        var filtered = new List<TaskPlanItem>();

        for (int i = 0; i < plan.Count; i++)
        {
            if (knownSkills.Contains(plan[i].SkillName))
            {
                indexMap[i] = filtered.Count;
                filtered.Add(plan[i]);
            }
        }

        foreach (var task in filtered)
        {
            task.DependsOn = task.DependsOn
                .Where(d => indexMap.ContainsKey(d))
                .Select(d => indexMap[d])
                .ToList();
        }

        return filtered;
    }

    /// <summary>
    /// Performs topological sort on the task plan. Returns execution order indices.
    /// Throws if a cycle is detected.
    /// </summary>
    public static List<int> TopologicalSort(List<TaskPlanItem> plan)
    {
        int n = plan.Count;
        var inDegree = new int[n];
        var adjacency = new List<List<int>>(n);

        for (int i = 0; i < n; i++)
            adjacency.Add([]);

        for (int i = 0; i < n; i++)
        {
            foreach (int dep in plan[i].DependsOn)
            {
                if (dep >= 0 && dep < n)
                {
                    adjacency[dep].Add(i);
                    inDegree[i]++;
                }
            }
        }

        var queue = new Queue<int>();
        for (int i = 0; i < n; i++)
        {
            if (inDegree[i] == 0) queue.Enqueue(i);
        }

        var order = new List<int>();
        while (queue.Count > 0)
        {
            int current = queue.Dequeue();
            order.Add(current);

            foreach (int neighbor in adjacency[current])
            {
                inDegree[neighbor]--;
                if (inDegree[neighbor] == 0) queue.Enqueue(neighbor);
            }
        }

        if (order.Count != n)
            throw new InvalidOperationException("Task plan contains a cycle and cannot be executed.");

        return order;
    }
}
