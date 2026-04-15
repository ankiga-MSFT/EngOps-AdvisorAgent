# Project Structure

The AdvisorAgent solution is organized into three projects plus a test project, following a clean separation of concerns.

```
AdvisorAgent/
├── AdvisorAgent.slnx                    # Solution file
├── src/
│   ├── AdvisorAgent.Functions/          # Serverless host & orchestration
│   │   ├── Program.cs                   # DI container & startup
│   │   ├── host.json                    # Functions runtime config
│   │   ├── local.settings.json          # Local dev settings
│   │   ├── Configuration/
│   │   │   └── skills.json              # Skill catalog definitions
│   │   ├── Triggers/
│   │   │   └── AdvisorHttpTrigger.cs    # HTTP endpoints
│   │   ├── Orchestration/
│   │   │   ├── AdvisorOrchestrator.cs   # Durable orchestrator & sub-orchestrator
│   │   │   └── AdvisorActivities.cs     # Activity function wrappers
│   │   └── Models/
│   │       └── OrchestratorModels.cs    # DTOs for orchestration I/O
│   │
│   ├── AdvisorAgent.Core/              # Domain logic & services
│   │   ├── Models/
│   │   │   ├── AzureContext.cs          # Azure resource scope model
│   │   │   ├── UserIntent.cs            # Intent classification model
│   │   │   ├── ConversationTurn.cs      # Chat turn model
│   │   │   └── AdvisorAgentResponse.cs  # Unified response model
│   │   ├── Skills/
│   │   │   ├── IAgentOrchestrationService.cs   # Core orchestration interface
│   │   │   ├── AgentOrchestrationService.cs    # LLM orchestration implementation
│   │   │   ├── AgentSkillDefinition.cs         # Skill metadata model
│   │   │   └── TaskPlan.cs                     # Task plan + DAG validator
│   │   ├── Conversation/
│   │   │   ├── IConversationStore.cs            # Store interface
│   │   │   ├── InMemoryConversationStore.cs     # Dev/test store
│   │   │   └── CosmosConversationStore.cs       # Production store
│   │   └── AzureContext/
│   │       ├── IAzureContextResolver.cs         # Context resolution interface
│   │       └── AzureContextResolver.cs          # LLM-based context resolver
│   │
│   └── AdvisorAgent.Tools/             # Azure data retrieval tools
│       ├── ToolBase.cs                  # Base class with ARM/ARG helpers
│       ├── AdvisorRecommendationTools.cs
│       ├── CostOptimizationTools.cs
│       ├── OutageRemediationTools.cs
│       ├── ResiliencyTools.cs
│       ├── ResourceGraphTools.cs
│       ├── RetirementTools.cs
│       └── SubscriptionTools.cs
│
└── tests/
    └── AdvisorAgent.Tests/             # xUnit test project
        ├── AzureContextModelTests.cs
        ├── ConversationStoreTests.cs
        └── TaskPlanValidatorTests.cs
```

## Project Dependencies

```
AdvisorAgent.Functions
    ├── AdvisorAgent.Core
    └── AdvisorAgent.Tools

AdvisorAgent.Core
    └── (no internal dependencies)

AdvisorAgent.Tools
    └── (no internal dependencies)

AdvisorAgent.Tests
    └── AdvisorAgent.Core
```

## Key Files by Responsibility

### Entry Points
| File | Role |
|------|------|
| `Triggers/AdvisorHttpTrigger.cs` | HTTP endpoints: orchestrate, status, negotiate, health |
| `Program.cs` | Dependency injection, skill catalog loading, tool registration |

### Orchestration
| File | Role |
|------|------|
| `Orchestration/AdvisorOrchestrator.cs` | Main orchestrator + skill execution sub-orchestrator |
| `Orchestration/AdvisorActivities.cs` | 12 activity functions wrapping core service calls |
| `Skills/AgentOrchestrationService.cs` | LLM-powered orchestration logic (context, intent, plan, execute) |

### Domain
| File | Role |
|------|------|
| `Skills/TaskPlan.cs` | DAG task plan model with topological sort and cycle detection |
| `Skills/AgentSkillDefinition.cs` | Skill metadata: name, tools, prompt, model, temperature |
| `Configuration/skills.json` | Declarative skill catalog (5 skills) |

### Data Access
| File | Role |
|------|------|
| `Conversation/CosmosConversationStore.cs` | Cosmos DB conversation persistence |
| `Tools/ToolBase.cs` | ARM REST + Resource Graph query helpers |
| `Tools/*.cs` | 7 tool classes for Azure data retrieval |
