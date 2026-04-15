# Task Planning & Execution

The task planning system enables the agent to break complex user requests into multiple, potentially dependent sub-tasks, each executed by a specialized skill.

## Task Plan Structure

A task plan is an array of `TaskPlanItem` objects forming a **Directed Acyclic Graph (DAG)**:

```json
[
  {
    "task": "Identify retiring Azure resources across subscriptions",
    "skillName": "RetirementSkill",
    "dependsOn": []
  },
  {
    "task": "Assess resiliency posture of affected resources",
    "skillName": "ResiliencySkill",
    "dependsOn": [0]
  },
  {
    "task": "Estimate cost impact of required migrations",
    "skillName": "CostOptimizationSkill",
    "dependsOn": [0]
  }
]
```

### Fields

| Field | Type | Description |
|-------|------|-------------|
| `task` | string | Human-readable description of what this task does |
| `skillName` | string | Which skill from the catalog executes this task |
| `dependsOn` | int[] | 0-based indices of tasks that must complete first |

## Plan Validation

The `PlanValidator` static class ensures task plans are safe to execute.

### Step 1: Remove Unknown Skills

```csharp
PlanValidator.RemoveUnknownSkills(plan, knownSkills)
```

- Filters out tasks referencing skills not in the catalog
- **Re-indexes** `dependsOn` references to account for removed tasks
- Prevents LLM hallucination of non-existent skills from causing failures

### Step 2: Topological Sort

```csharp
PlanValidator.TopologicalSort(plan)
```

Implements **Kahn's algorithm** to produce a valid execution order:

1. Compute in-degree for each task node
2. Enqueue all nodes with in-degree 0 (no dependencies)
3. Process queue: for each node, decrement in-degree of dependents; enqueue when in-degree reaches 0
4. If processed count ≠ total nodes → **cycle detected** → throw `InvalidOperationException`

**Output:** Array of task indices in execution order.

### Example

Given the plan above:
```
Task 0 (RetirementSkill):     dependsOn: []      → in-degree: 0
Task 1 (ResiliencySkill):     dependsOn: [0]     → in-degree: 1
Task 2 (CostOptimizationSkill): dependsOn: [0]   → in-degree: 1
```

Execution order: `[0, 1, 2]` (or `[0, 2, 1]` — both are valid topological orderings).

```
    ┌────────────────┐
    │ RetirementSkill │  (Task 0)
    └───────┬────────┘
            │
     ┌──────┴──────┐
     ▼             ▼
┌──────────┐ ┌──────────────────┐
│Resiliency│ │CostOptimization  │
│Skill     │ │Skill             │
│(Task 1)  │ │(Task 2)          │
└──────────┘ └──────────────────┘
```

## Skill Execution Sub-Orchestrator

The `SkillExecutionSubOrchestrator` processes the validated task plan:

```
For each taskIndex in executionOrder:
    │
    ├─ 1. Collect upstream outputs
    │     • For each dependency in task.dependsOn
    │       → Retrieve stored result from outputs dictionary
    │
    ├─ 2. Generate skill prompt
    │     • Combine: task description + skill system prompt
    │       + Azure context + upstream outputs + conversation history
    │
    ├─ 3. Execute skill
    │     • LLM conversation with tool access
    │     • Tools make live ARM/ARG queries
    │     • Returns AdvisorAgentResponse
    │
    └─ 4. Store result
          • outputs[taskIndex] = result
          • Available as upstream data for dependent tasks
```

### Context Chaining

When a task depends on previous tasks, their outputs are included in the skill prompt. This enables sophisticated multi-step analyses:

1. **Task 0** (RetirementSkill): "Found 3 retiring resources: VM-A, VM-B, Storage-C"
2. **Task 1** (ResiliencySkill): Receives Task 0's output → "Assess resiliency of VM-A, VM-B, Storage-C"
3. **Task 2** (CostOptimizationSkill): Receives Task 0's output → "Estimate migration costs for VM-A, VM-B, Storage-C"

### Response Aggregation

After all tasks complete, the sub-orchestrator aggregates outputs:

```
Task 0 result
---
Task 1 result
---
Task 2 result
```

This aggregated markdown response becomes the final orchestration output.

## Edge Cases

| Scenario | Handling |
|----------|----------|
| Single task plan | Normal execution — sub-orchestrator simply runs the one task |
| All tasks independent | All have `dependsOn: []` — executed sequentially in sorted order |
| Diamond dependencies | Task depends on two tasks that depend on the same root — handled correctly by topological sort |
| Cycle detected | `TopologicalSort()` throws; orchestrator catches and falls back to sequential execution |
| LLM suggests unknown skill | `RemoveUnknownSkills()` filters it pre-execution; dependencies re-indexed |
| Empty plan after filtering | Returns a "no applicable skills" message |
