namespace CXOAI.SkillFramework;

/// <summary>
/// Derives group numbers for a task plan by finding connected components
/// in the undirected dependency graph. Tasks in independent sub-DAGs
/// (no shared dependencies) get different group numbers.
///
/// Example: "give me csat of walmart and total sr ticket for adobe"
///   Task 0 (CSAT) → Task 2 (Summarize Walmart) → Task 4 (Export Walmart)  = Group 1
///   Task 1 (SR)   → Task 3 (Summarize Adobe)   → Task 5 (Export Adobe)   = Group 2
///
/// These two sub-DAGs share no edges → separate groups.
/// </summary>
public static class PlanGrouper
{
    /// <summary>
    /// Assigns <see cref="TaskPlanItem.Group"/> to each task based on
    /// connected components in the undirected dependency graph.
    /// Group numbers start at 1.
    /// </summary>
    public static void AssignGroups(List<TaskPlanItem> plan)
    {
        if (plan.Count == 0) return;

        // Build undirected adjacency using integer indices
        var adjacency = new Dictionary<int, HashSet<int>>();
        for (int i = 0; i < plan.Count; i++)
            adjacency[i] = [];

        for (int i = 0; i < plan.Count; i++)
        {
            foreach (var dep in plan[i].DependsOn)
            {
                if (dep >= 0 && dep < plan.Count)
                {
                    adjacency[i].Add(dep);
                    adjacency[dep].Add(i);
                }
            }
        }

        // BFS to find connected components
        var visited = new HashSet<int>();
        var groupNum = 0;

        for (int i = 0; i < plan.Count; i++)
        {
            if (visited.Contains(i))
                continue;

            groupNum++;
            var queue = new Queue<int>();
            queue.Enqueue(i);
            visited.Add(i);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                plan[current].Group = groupNum;

                foreach (var neighbor in adjacency[current])
                {
                    if (visited.Add(neighbor))
                        queue.Enqueue(neighbor);
                }
            }
        }
    }
}
