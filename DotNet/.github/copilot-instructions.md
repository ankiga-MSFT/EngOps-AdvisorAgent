# Copilot Instructions

## General Guidelines
- First general instruction
- Second general instruction

## Code Style
- Use specific formatting rules
- Follow naming conventions
- Prefer using mock frameworks (e.g., Moq) for test dependencies rather than NullLogger/Abstractions approaches.

## Function App and Console SkillOrchestrator
- Avoid code duplication between Function App orchestrator and Console SkillOrchestrator. 
- Exception handling and timeout logic should reside in `OrchestratorStepService`, the shared step layer, rather than in individual orchestrators.
- Each LLM call should have its own timeout variable (not universal), and for skill execution, use `skillInfo.Timeout`. 
- Timeout variables should be configurable for future releases.

## Database Instructions
- When using the Cosmos DB VectorDistance function, note that it returns cosine SIMILARITY (higher = more similar, 1.0 = identical), NOT cosine distance. Filter with `item.Distance < minScore` to keep matches, not `item.Distance > maxDistance`.

## Knowledge Graph Instructions
- When adding relationships to `KnowledgeGraph.json`, add relationships to the source node (e.g., "get csat score"), not to the target metric nodes. The source node is the one that has the relationships pointing outward.
- Be aware that `KnowledgeGraphTools` has a static `Dictionary<Node, List<Relationship>>` graph field. Since `Node` is a class without `Equals`/`GetHashCode` overrides, the dictionary uses reference equality. If `OrchestratorStepService` is transient, each new `KnowledgeGraphTools` instance deserializes new `Node` objects that become duplicate keys in the static graph, causing `CompactNodeIndex` to grow and potentially exceed LLM token limits. To prevent this, either call `graph.Clear()` before populating or register `KnowledgeGraphTools` as a singleton.