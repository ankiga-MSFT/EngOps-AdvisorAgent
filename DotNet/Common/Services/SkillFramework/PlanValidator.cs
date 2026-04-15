using Microsoft.Extensions.Logging;

namespace CXOAI.SkillFramework;

/// <summary>
/// Validates a task plan produced by the Planner LLM and provides safe removal
/// with DependsOn re-indexing to prevent the index-shift bug.
/// All checks are structural — no skill-name-specific logic.
/// </summary>
public class PlanValidator
{
    private readonly ILogger _logger;

    public PlanValidator(ILogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Validates the plan and returns a list of error messages.
    /// An empty list means the plan is valid.
    /// </summary>
    public List<string> Validate(List<PlannerTaskItem> plan, HashSet<string> knownSkills)
    {
        var errors = new List<string>();

        for (int i = 0; i < plan.Count; i++)
        {
            var task = plan[i];

            // Rule 1: SkillName must be a registered skill
            if (!knownSkills.Contains(task.SkillName))
                errors.Add($"Task {i} ('{task.Task}') references unknown skill '{task.SkillName}'");

            // Rule 2: DependsOn indices must be valid
            foreach (var dep in task.DependsOn)
            {
                if (dep < 0 || dep >= plan.Count)
                    errors.Add($"Task {i} ('{task.Task}') has out-of-range DependsOn index {dep}");
                else if (dep == i)
                    errors.Add($"Task {i} ('{task.Task}') depends on itself");
            }
        }

        // Rule 3: No circular dependencies
        if (errors.Count == 0 && HasCycle(plan))
            errors.Add("Circular dependency detected in plan");

        if (errors.Count > 0)
            _logger.LogWarning("PlanValidator found {ErrorCount} issue(s): [{Errors}]",
                errors.Count, string.Join("; ", errors));

        return errors;
    }

    /// <summary>
    /// Safely removes tasks with unknown skills and re-indexes all DependsOn references
    /// so no index-shift corruption occurs.
    /// </summary>
    public static void RemoveUnknownSkillsAndReindex(List<TaskPlanItem> plan, HashSet<string> knownSkills, ILogger logger)
    {
        for (int i = 0; i < plan.Count; i++)
        {
            if (knownSkills.Contains(plan[i].SkillName))
                continue;

            var removedIdx = i;
            logger.LogWarning("Task {Idx} ('{Task}') references unknown skill '{Skill}', removing and re-indexing",
                removedIdx, plan[i].Task, plan[i].SkillName);
            plan.RemoveAt(i);
            i--;

            // Re-index DependsOn in all remaining tasks
            foreach (var t in plan)
            {
                // Remove references to the removed task
                t.DependsOn.Remove(removedIdx);
                // Shift down any indices that were above the removed index
                t.DependsOn = t.DependsOn
                    .Select(d => d > removedIdx ? d - 1 : d)
                    .ToList();
            }
        }

        // Final cleanup: remove out-of-range and self-references
        for (int i = 0; i < plan.Count; i++)
        {
            plan[i].DependsOn.RemoveAll(d => d < 0 || d >= plan.Count || d == i);
        }
    }

    /// <summary>
    /// Converts a validated plan into a DAG dictionary suitable for TopologicalSort.
    /// Keys are task indices (as strings), values are DependsOn indices (as strings).
    /// </summary>
    public static Dictionary<string, List<string>> ToDag(List<TaskPlanItem> plan)
    {
        var dag = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < plan.Count; i++)
        {
            dag[i.ToString()] = plan[i].DependsOn.Select(d => d.ToString()).ToList();
        }
        return dag;
    }

    /// <summary>
    /// Detects cycles using iterative DFS with a 3-color marking scheme.
    /// </summary>
    private static bool HasCycle(List<PlannerTaskItem> plan)
    {
        var nodeCount = plan.Count;
        var white = new HashSet<int>(Enumerable.Range(0, nodeCount));
        var gray = new HashSet<int>();
        var black = new HashSet<int>();

        // Build adjacency: index → list of indices that depend on it
        var adjacency = new Dictionary<int, List<int>>();
        for (int i = 0; i < nodeCount; i++)
            adjacency[i] = [];
        for (int i = 0; i < nodeCount; i++)
        {
            foreach (var dep in plan[i].DependsOn)
            {
                if (dep >= 0 && dep < nodeCount)
                    adjacency[dep].Add(i);
            }
        }

        for (int startNode = 0; startNode < nodeCount; startNode++)
        {
            if (!white.Contains(startNode))
                continue;

            var stack = new Stack<(int node, bool expanding)>();
            stack.Push((startNode, true));

            while (stack.Count > 0)
            {
                var (node, expanding) = stack.Pop();

                if (!expanding)
                {
                    gray.Remove(node);
                    black.Add(node);
                    continue;
                }

                if (black.Contains(node)) continue;
                if (gray.Contains(node)) return true;

                white.Remove(node);
                gray.Add(node);
                stack.Push((node, false));

                foreach (var neighbor in adjacency[node])
                {
                    if (gray.Contains(neighbor)) return true;
                    if (!black.Contains(neighbor))
                        stack.Push((neighbor, true));
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Skills that are intermediate data-fetching steps — their output feeds
    /// downstream tasks but is not directly shown to the user.
    /// </summary>
    private static readonly HashSet<string> DataSourceSkills = new(StringComparer.OrdinalIgnoreCase)
    {
        "AspectSkill", "NLTKqlSkill"
    };

    /// <summary>
    /// Returns the set of task indices whose results should appear in the
    /// final Groups list. Excludes intermediate data-fetching tasks when
    /// output tasks (summarization, visualization, reporting) exist.
    /// If ALL tasks are data-fetching, includes everything.
    /// </summary>
    public static HashSet<int> GetOutputTaskIndices(List<TaskPlanItem> plan)
    {
        var outputIndices = new HashSet<int>();
        var allIndices = new HashSet<int>();

        for (int i = 0; i < plan.Count; i++)
        {
            allIndices.Add(i);
            if (!DataSourceSkills.Contains(plan[i].SkillName))
                outputIndices.Add(i);
        }

        // If no output tasks exist (e.g., simple "get csat"), show all tasks
        return outputIndices.Count > 0 ? outputIndices : allIndices;
    }

    /// <summary>
    /// Fixes dependency violations involving consumer/action skills.
    ///
    /// Phase 1 — Consumer skills are terminal (leaf) nodes.
    ///   No task should ever depend on a consumer skill. If any task
    ///   (including SummarizationSkill) depends on a consumer, rewire
    ///   to the consumer's upstream data sources.
    ///
    /// Phase 2 — When a SummarizationSkill already exists in a consumer's
    ///   dependency chain, rewire the consumer to depend on it instead
    ///   of raw data sources. Does NOT auto-insert summaries — that is
    ///   the planner's job (triggered by analysis/why queries).
    ///
    /// Example fix (both phases):
    ///   Before: Task0(Aspect,[]) → Task1(UXGenerator,[0]) → Task2(Summary,[0,1]) → Task3(Reporting,[2])
    ///   Phase1: Task2(Summary,[0])  — removed consumer dep [1]
    ///   Phase2: Task1(UXGenerator,[2]) — rewired from data [0] to summary [2]
    ///   After:  Task0(Aspect,[]) → Task2(Summary,[0]) → Task1(UXGenerator,[2])
    ///                                                  → Task3(Reporting,[2])
    /// </summary>
    private static readonly HashSet<string> ConsumerSkills = new(StringComparer.OrdinalIgnoreCase)
    {
        "UXGeneratorSkill", "ReportingSkill"
    };

    public static void FixSiblingDependencies(List<TaskPlanItem> plan, ILogger? logger = null)
    {
        // ── Phase 1: No task should depend on a consumer skill ─────────
        // Consumer skills produce final output (charts, documents) and
        // should never be upstream inputs to analysis or other tasks.
        bool changed;
        do
        {
            changed = false;
            for (int i = 0; i < plan.Count; i++)
            {
                var newDeps = new List<int>();
                foreach (var depIdx in plan[i].DependsOn)
                {
                    if (depIdx < 0 || depIdx >= plan.Count)
                        continue;

                    if (ConsumerSkills.Contains(plan[depIdx].SkillName))
                    {
                        // VIOLATION: task depends on a consumer skill.
                        // Rewire to the consumer's upstream data sources.
                        foreach (var transitiveDep in plan[depIdx].DependsOn)
                        {
                            if (!newDeps.Contains(transitiveDep))
                                newDeps.Add(transitiveDep);
                        }

                        logger?.LogWarning(
                            "FixSiblingDependencies Phase1: Task {Idx} ({Skill}) depended on consumer Task {DepIdx} ({DepSkill}). Rewired to [{NewDeps}]",
                            i, plan[i].SkillName, depIdx, plan[depIdx].SkillName,
                            string.Join(",", plan[depIdx].DependsOn));
                        changed = true;
                    }
                    else
                    {
                        if (!newDeps.Contains(depIdx))
                            newDeps.Add(depIdx);
                    }
                }

                plan[i].DependsOn = newDeps;
            }
        } while (changed);

        // ── Phase 2: Rewire consumers to their chain's summary ─────────
        // When a SummarizationSkill exists and shares data-source deps
        // with a consumer, the consumer should receive the analyzed
        // output instead of raw data. Only rewires — never inserts.
        var summaryTasks = plan
            .Select((t, i) => (Task: t, Index: i))
            .Where(x => x.Task.SkillName.Equals("SummarizationSkill", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (summaryTasks.Count == 0)
            return;

        for (int i = 0; i < plan.Count; i++)
        {
            if (!ConsumerSkills.Contains(plan[i].SkillName))
                continue;

            // Find the summary whose data sources overlap most with
            // this consumer's deps (handles multi-group plans).
            (int SummaryIdx, List<int> SharedDeps)? bestMatch = null;

            foreach (var (summaryTask, summaryIdx) in summaryTasks)
            {
                var shared = plan[i].DependsOn
                    .Intersect(summaryTask.DependsOn)
                    .ToList();

                if (shared.Count > 0 &&
                    (bestMatch is null || shared.Count > bestMatch.Value.SharedDeps.Count))
                {
                    bestMatch = (summaryIdx, shared);
                }
            }

            if (bestMatch is null)
                continue;

            var oldDeps = plan[i].DependsOn.ToList();

            // Replace shared data-source deps with the matching summary.
            // Keep non-shared deps (belong to a different data path).
            var newDeps = plan[i].DependsOn
                .Except(bestMatch.Value.SharedDeps)
                .ToList();

            if (!newDeps.Contains(bestMatch.Value.SummaryIdx))
                newDeps.Add(bestMatch.Value.SummaryIdx);

            if (!oldDeps.SequenceEqual(newDeps))
            {
                plan[i].DependsOn = newDeps;
                logger?.LogInformation(
                    "FixSiblingDependencies Phase2: Task {Idx} ({Skill}) rewired from [{OldDeps}] to [{NewDeps}]",
                    i, plan[i].SkillName, string.Join(",", oldDeps), string.Join(",", newDeps));
            }
        }
    }
}
