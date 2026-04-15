# CXOAI Orchestrator — Architecture & Data Flow

## Overview

The CXOAI Orchestrator is an AI-powered multi-agent system that processes user prompts through a pipeline of LLM agents. It runs in two modes:

| | Console App (`SkillOrchestrator.RunAsync`) | Azure Functions (`CxoaiOrchestrator` + Durable Tasks) |
|---|---|---|
| **Entry point** | `SkillOrchestrator.RunAsync(userId, prompt, userContext?, sessionId?)` | `POST /api/orchestrate` → `OrchestratorMain` |
| **Configuration** | `TreeJsonConfigurationStoreProvider` (local JSON) | `TreeConfigurationStoreProvider` (Azure AI Search index) |
| **Memory** | `FileMemoryStore` (local JSON file) | `CosmosMemoryStore` (Cosmos DB with vector search) |
| **Knowledge graph** | `KnowledgeGraphTools` — text match + LLM fallback (`gpt-4o`) on `KnowledgeGraph.json` | Same (bundled in output) |
| **Conversation history** | `InMemoryConversationStore` (in-process dictionary) | `InMemoryConversationStore` (swap to Cosmos for prod) |
| **Status notifications** | `ConsoleStatusNotifier` → prints to terminal | `SignalRStatusNotifier` → pushes via Azure SignalR Service |
| **User input** | `Console.ReadLine()` via `IStatusNotifier` | `WaitForExternalEvent` + `POST /api/instances/{id}/skills/{name}/input` |
| **Access token** | N/A | `OrchestratorInput.AccessToken` → `IUserAuthContext` (scoped per activity) |

---

## End-to-End Flow (Mermaid)

```mermaid
flowchart TD
    %% ═══ Entry ═══
    USER(["User"]) -->|"prompt + userId + context"| CONSOLE["Console: RunAsync()"]
    USER -->|"POST JSON body"| HTTP["Functions: POST /api/orchestrate"]
    CONSOLE --> STEP1
    HTTP --> STEP1

    %% ═══ Persistent Stores (right side) ═══
    MEM_STORE[("Memory Store<br/>File / Cosmos")]
    CONV_STORE[("Conversation Store<br/>InMemory / Cosmos")]
    KG_JSON[("KnowledgeGraph.json")]
    KG_TOOLS["KnowledgeGraphTools<br/>Text match + LLM fallback"]
    KG_TOOLS -.->|"reads nodes, tags, descriptions"| KG_JSON
    KG_TOOLS -.->|"MatchNodeNamesByLlmAsync · gpt-4o"| LLM_KG["Azure OpenAI<br/>gpt-4o"]

    %% ═══ Step 1: EnhancePrompt ═══
    STEP1["<b>1. EnhancePrompt</b><br/>Build prompt + user prefs + knowledge + session context<br/><i>(LLM used for knowledge graph matching)</i>"]
    STEP1 -.->|"RecallAsync — user prefs + aspect knowledge"| MEM_STORE
    STEP1 -.->|"GetSessionSummaryAsync — session context"| CONV_STORE
    STEP1 -.->|"GetSystemKnowledgeAsync"| KG_TOOLS
    STEP1 --> STEP2

    %% ═══ Step 2: ClassifyIntent ═══
    STEP2["<b>2. ClassifyIntent</b> · <i>gpt-4o-mini</i><br/>Informational | DataAction | Unknown"]
    STEP2 -->|"Informational"| STEP2A["<b>2a. AnswerFromKnowledge</b> · <i>gpt-4o-mini</i><br/>LLM answers from domain knowledge"]
    STEP2 -->|"Unknown"| SHORT_UNK["Return error message"]
    STEP2 -->|"DataAction"| STEP2B

    STEP2A -.->|"SummarizeAndStoreAsync"| CONV_STORE
    STEP2A -.->|"ExtractAndStoreAsync"| MEM_STORE
    STEP2A -->|"short-circuit"| STEP7
    SHORT_UNK -->|"short-circuit"| STEP7

    %% ═══ Step 2b: CheckHistory ═══
    STEP2B["<b>2b. CheckHistory</b> · <i>gpt-4o-mini</i><br/>Try answer from conversation history"]
    STEP2B -.->|"GetSessionSummaryAsync"| CONV_STORE
    STEP2B -->|"CanAnswer=true"| STEP7
    STEP2B -->|"CanAnswer=false"| STEP3

    %% ═══ Steps 3–5: Plan & Sort ═══
    STEP3["<b>3. DecomposeTasks</b> · <i>gpt-4o-mini</i><br/>LLM planner → TaskPlanItem#91;#93;"]
    STEP3 --> STEP4["<b>4. GetSkillsByName</b><br/>Config lookup — no LLM"]
    STEP4 --> STEP4A["<b>4a. ValidatePlan</b><br/>RemoveUnknownSkills + Reindex DependsOn"]
    STEP4A --> STEP5["<b>5. TopologicalSort</b><br/>PlanValidator.ToDag → Sort"]
    STEP5 --> STEP6

    %% ═══ Step 6: DAG Execution ═══
    subgraph STEP6["<b>6. ExecuteTasks — Task DAG</b>"]
        direction TB

        subgraph Roots[" "]
            T0["Task_0: AspectSkill · <i>gpt-4o</i><br/>Get CSAT · deps: #91;#93;"]
            T1["Task_1: AspectSkill · <i>gpt-4o</i><br/>Get IR Met · deps: #91;#93;"]
            T2["Task_2: AspectSkill · <i>gpt-4o</i><br/>Get FDR · deps: #91;#93;"]
        end

        T0 -->|"memory#91;0#93;"| T3
        T1 -->|"memory#91;1#93;"| T3
        T2 -->|"memory#91;2#93;"| T3

        T3["Task_3: SummarizationSkill · <i>gpt-4o-mini</i><br/>Summarize · deps: #91;0,1,2#93;"]
        T3 -->|"memory#91;3#93;"| T4

        T4["Task_4: ReportingSkill · <i>gpt-4o-mini</i><br/>Export Excel · deps: #91;3#93;"]
        T4 -->|"memory#91;4#93;"| T5

        T5["Task_5: UXGeneratorSkill · <i>gpt-4o-mini</i><br/>Dashboard · deps: #91;4#93;"]
    end

    %% ═══ Result Assembly ═══
    STEP6 --> ASM["GroupResult#91;#93; → CXOAgentResponse<br/>single group: direct | multi: ## Answer N"]
    ASM --> STEP7

    %% ═══ Step 7: Summarize ═══
    STEP7["<b>7. SummarizeAndStoreAsync</b> · <i>gpt-4o-mini</i><br/>LLM summarizes conversation"]
    STEP7 -.->|"UpsertSessionSummary + AppendToHistory"| CONV_STORE
    STEP7 -.->|"ExtractAndStoreAsync — extract facts"| MEM_STORE
    STEP7 --> RESULT(["Return final result"])

    %% ═══ History context from 2b into root tasks ═══
    STEP2B -.->|"historyContext<br/>HasRelevantContext=true"| T0
```

> **Reading guide:** Solid arrows (`→`) = pipeline control flow. Dashed arrows (`-.->`) = read/write to persistent stores or LLM calls. The DAG in Step 6 shows `memory[idx]` flowing from each completed task to its dependents.
>
> **Model key:** Pipeline steps use `gpt-4o-mini` (`_secondaryModelName` from `AzureOpenAIModelNameV2`). Knowledge graph node matching uses `gpt-4o` (via `KnowledgeGraphTools.MatchNodeNamesByLlmAsync`). Skill execution uses per-skill `ModelName` from `Skills.json` — e.g. AspectSkill uses `gpt-4o`.

---

## Skills & Tools Map

Each skill is an LLM agent with a system prompt and a set of tools from the `CXOAIToolsService` project (`Tools\CXOAI\`). The planner in Step 3 selects skills; the agent in Step 6 calls tools via function-calling.

```mermaid
flowchart LR
    %% ═══════════════════════════════════════════
    %% 1. AspectSkill
    %% ═══════════════════════════════════════════
    subgraph S1["<b>AspectSkill</b> · gpt-4o<br/>Metric data retrieval — 3-step workflow"]
        direction TB
        AT1["SearchCustomerByNameAndWorkloadType<br/>name → ch:customer::tpid:{id}"]
        AT2["SearchProductByProductName<br/>name → ch:product::id:{guid}"]
        AT3["SearchMetricConfigFilters<br/>Load filters, groupBy, view/unit"]
        AT4["GetMetricDataByEntityId<br/>Fetch metric data for entity"]
        AT5["SearchCustomerWorkload <i>(optional)</i><br/>free-text → WorkloadTypeEnum"]
        AT6["SearchProgramByProgramName <i>(optional)</i><br/>name → ProgramTypeEnum"]
    end

    AT1 & AT2 & AT4 -->|"POST"| INSIGHTS[("Insights API")]
    AT3 -->|"config lookup"| CONFIG[("Config Store")]
    AT4 -->|"Cosmos query"| COSMOS_DATA[("Cosmos DB")]

    %% ═══════════════════════════════════════════
    %% 2. ReportingSkill
    %% ═══════════════════════════════════════════
    subgraph S2["<b>ReportingSkill</b> · gpt-4o-mini<br/>Generate documents / send email"]
        direction TB
        RT1["GetReportingTemplatesAsync<br/>Config Store → fallback built-in"]
        RT2["GenerateWordAsync<br/>markdown → .docx"]
        RT3["GenerateExcelAsync <i>(placeholder)</i>"]
        RT4["GeneratePDFAsync <i>(placeholder)</i>"]
        RT5["SendEmailAsync <i>(placeholder)</i>"]
    end

    RT1 -->|"GetConfigurationsWithNames"| CONFIG
    RT2 -->|"Store artifact"| BLOB[("Blob Storage")]

    %% ═══════════════════════════════════════════
    %% 3. UXGeneratorSkill
    %% ═══════════════════════════════════════════
    subgraph S3["<b>UXGeneratorSkill</b> · gpt-4o-mini<br/>Fluent UI v8 from Decision Matrix"]
        direction TB
        UX1["GenerateComponentAsync<br/>Validate ComponentType + PropsJson<br/>→ IsUIComponent=true"]
    end

    %% ═══════════════════════════════════════════
    %% 4. SummarizationSkill
    %% ═══════════════════════════════════════════
    subgraph S4["<b>SummarizationSkill</b> · gpt-4o-mini<br/>Executive-ready summaries"]
        direction TB
        SUMM_NOTE["<i>No tools — LLM-only</i>"]
    end

    %% ═══════════════════════════════════════════
    %% 5. NLTKqlSkill (placeholder)
    %% ═══════════════════════════════════════════
    subgraph S5["<b>NLTKqlSkill</b> <i>(placeholder)</i><br/>Natural language → KQL"]
        direction TB
        KQ1["GetKqlQuery<br/>NL → KqlRequest <i>(placeholder)</i>"]
        KQ2["ExecuteQueryAsync<br/>Run KQL on ADX <i>(placeholder)</i>"]
    end

    KQ2 -.->|"KQL query"| ADX[("Azure Data Explorer")]

    %% ═══════════════════════════════════════════
    %% Init-time dependencies (AspectTools)
    %% ═══════════════════════════════════════════
    AT1 -.->|"init: env settings"| CONFIG
    AT1 -.->|"init: client certs"| KV[("Azure Key Vault")]
```

### Skill Summary

| Skill | Model | Tools (from `CXOAIToolsService`) | Description |
|---|---|---|---|
| **AspectSkill** | `gpt-4o` | `AspectTools` (6 methods) | 3-step workflow: resolve entity (customer/product) → load metric config (filters, groupBy, view) → fetch metric data from Insights API or Cosmos DB |
| **ReportingSkill** | `gpt-4o-mini` | `ReportingTools` (5 methods) | Retrieve template → generate document (Word fully implemented; Excel/PDF/Email placeholders) → store artifact to Blob |
| **UXGeneratorSkill** | `gpt-4o-mini` | `UXGeneratorTool` (1 method) | LLM selects Fluent UI v8 component from Decision Matrix, builds props JSON from upstream data, calls `GenerateComponentAsync` once |
| **SummarizationSkill** | `gpt-4o-mini` | *(none — LLM-only)* | Condenses upstream data into executive-ready summaries with trend analysis, cross-entity correlation, and recommendations |
| **NLTKqlSkill** | *(placeholder)* | `NLTKqlTools` (2 methods) | Natural language → KQL query generation + execution on Azure Data Explorer *(not yet implemented)* |

### Tool Details — AspectTools Workflow

```mermaid
flowchart TD
    subgraph Step1["Step 1: Resolve Entity"]
        ENT_CHECK{"Entity in prompt?"}
        ENT_CHECK -->|"name provided"| SEARCH_CUST["SearchCustomerByNameAndWorkloadType<br/>or SearchProductByProductName"]
        ENT_CHECK -->|"CH URI / TPID / GUID"| DIRECT["Use directly:<br/>ch:customer::tpid:{id}<br/>ch:product::id:{guid}"]
        ENT_CHECK -->|"missing"| ASK_USER["Ask user to clarify<br/>EXIT — wait for input"]
        SEARCH_CUST -->|"1 result"| RESOLVED["Entity resolved ✅"]
        SEARCH_CUST -->|"multiple"| ASK_SELECT["Show results, ask to select<br/>EXIT — wait for input"]
        SEARCH_CUST -->|"0 results"| NOT_FOUND["Entity not found ❌"]
        DIRECT --> RESOLVED
    end

    subgraph Step2["Step 2: Resolve Metric Config"]
        RESOLVED --> CONFIG_CALL["SearchMetricConfigFilters<br/>(metricName, entityId)"]
        CONFIG_CALL -->|"success"| CONFIG_OK["Config loaded ✅<br/>Filters, GroupBy, View, Unit"]
        CONFIG_CALL -->|"not found"| CONFIG_FAIL["Metric not found ❌"]
        CONFIG_CALL -->|"entity type mismatch"| CONFIG_MISMATCH["Not supported for<br/>this entity type ❌"]
    end

    subgraph Step3["Step 3: Get Data"]
        CONFIG_OK --> VALIDATE["Validate & map user intent<br/>→ exact filter/groupBy/view/unit names"]
        VALIDATE --> GET_DATA["GetMetricDataByEntityId<br/>(entityId, metric, filters, groupBy,<br/>view, unit, timeRange)"]
        GET_DATA -->|"Insights API"| RESULT_INS["Metric data ✅"]
        GET_DATA -->|"Cosmos DB"| RESULT_COS["Metric data ✅"]
    end
```

---

## External System Interactions

```mermaid
flowchart LR
    subgraph Orchestrator["Orchestrator"]
        ENH["EnhancePrompt"]
        DECOMPOSE["DecomposeTasks"]
        EXEC["ExecuteTasks"]
        SUMM["Summarize & Store"]
        NOTIFY["Status Notifier"]
    end

    subgraph ConfigStore["Skill Configuration<br/>—<br/>Console: SeedData.json<br/>Functions: Azure AI Search Index"]
        SEED["Skill definitions<br/>+ AspectConfigs"]
    end

    subgraph KnowledgeGraph["Knowledge Graph<br/>—<br/>KnowledgeGraphTools"]
        KG["Nodes + Relationships<br/>(aspects, metrics, aliases)"]
        KG_FILE["KnowledgeGraph.json<br/>(static data)"]
        KG_LLM["LLM node matching<br/>(gpt-4o fallback)"]
    end

    subgraph MemoryStore["Memory Store<br/>—<br/>Console: FileMemoryStore (JSON)<br/>Functions: CosmosMemoryStore (Cosmos DB)"]
        MEM_USER["User facts<br/>(preferences, context)"]
        MEM_ASPECT["Aspect knowledge<br/>(system scope)"]
        MEM_VECTOR["Vector search<br/>(embeddings)"]
    end

    subgraph ConvStore["Conversation Store<br/>—<br/>InMemoryConversationStore"]
        CONV["Session summaries<br/>(per userId, timestamped)"]
    end

    subgraph OpenAI["Azure OpenAI"]
        LLM["GPT-4o / GPT-4o-mini"]
        EMBED["text-embedding-3-small"]
    end

    subgraph Notification["Status Notification<br/>—<br/>Console: ConsoleStatusNotifier<br/>Functions: SignalRStatusNotifier"]
        SIG["Azure SignalR Service"]
        CON_OUT["Console output"]
    end

    ENH -->|"RecallAsync(userId, prompt)"| MEM_USER
    ENH -->|"RecallAsync('system', prompt)"| MEM_ASPECT
    ENH -->|"GetSystemKnowledgeAsync(prompt)"| KG
    KG_LLM -->|"MatchNodeNamesByLlmAsync"| LLM
    DECOMPOSE -->|"GetSkillsByNameAsync(skillNames)"| SEED
    EXEC -->|"Tool calls (AspectTools, etc.)"| LLM
    SUMM -->|"SummarizeAndStoreAsync()"| CONV
    SUMM -->|"SummarizeAndStoreAsync()"| MEM_USER
    NOTIFY -->|"PublishStatusAsync()"| SIG
    NOTIFY -->|"PrintToConsole()"| CON_OUT

    MEM_VECTOR -.->|"EmbedAsync()"| EMBED
    SEED -.->|"Vector search (Functions)"| EMBED
```

---

## Step-by-Step Data Flow

```mermaid
sequenceDiagram
    actor User
    participant Entry as Console / HTTP Trigger
    participant Orch as Orchestrator
    participant Memory as Memory Store<br/>(JSON / Cosmos DB)
    participant KG as KnowledgeGraphTools
    participant Config as Configuration Store<br/>(JSON / Search Index)
    participant LLM as Azure OpenAI
    participant Conv as Conversation Store
    participant Notifier as Status Notifier<br/>(Console / SignalR)
    participant UI as Client / UI

    User->>Entry: prompt + userId + userContext + sessionId?
    Entry->>Orch: RunAsync / OrchestratorMain

    Note over Orch: Step 1: EnhancePrompt
    Orch->>Notifier: BeginStep("EnhancePrompt")
    Notifier-->>UI: ReceiveStatus (Running)
    Orch->>Memory: RecallAsync(userId, prompt, scope=User)
    Memory-->>Orch: user preference facts
    Orch->>Memory: RecallAsync("system", prompt, scope=Aspect)
    Memory-->>Orch: aspect knowledge facts
    Orch->>KG: GetSystemKnowledgeAsync(prompt)
    KG->>KG: MatchNodeNamesByText(prompt)
    alt residual or no text matches
        KG->>LLM: MatchNodeNamesByLlmAsync(prompt) · gpt-4o
        LLM-->>KG: matched node names
    end
    KG-->>Orch: matched nodes + relationships
    Note right of Orch: UserContext handled inside EnhancePrompt<br/>(no separate Step 0)
    Orch->>Notifier: CompleteStep("EnhancePrompt")
    Notifier-->>UI: ReceiveStatus (Completed)

    Note over Orch: Step 2: ClassifyIntent (BEFORE history check)
    Orch->>Notifier: BeginStep("ClassifyIntent")
    Notifier-->>UI: ReceiveStatus (Running)
    Orch->>LLM: Classify: Informational | DataAction | Unknown
    LLM-->>Orch: UserIntent
    Orch->>Notifier: CompleteStep("ClassifyIntent")
    Notifier-->>UI: ReceiveStatus (Completed)

    alt Informational
        Orch->>LLM: AnswerFromKnowledgeAsync(prompt, generalKnowledge)
        Orch->>Conv: SummarizeAndStoreAsync()
        Notifier-->>UI: ReceiveCompleted (Functions only)
        Orch-->>User: Return knowledge answer
    else Unknown
        Orch->>Conv: SummarizeAndStoreAsync()
        Notifier-->>UI: ReceiveCompleted (Functions only)
        Orch-->>User: Return error message
    else DataAction
        Note over Orch: Continue to history check...
    end

    Note over Orch: Step 2a: CheckHistory (DataAction only)
    Orch->>Notifier: BeginStep("CheckHistory")
    Notifier-->>UI: ReceiveStatus (Running)
    Orch->>Conv: GetSessionSummaryAsync(userId, sessionId)
    Conv-->>Orch: session summary (if any)
    Orch->>LLM: Can this be answered from history?
    LLM-->>Orch: HistoryAnswerResult

    alt CanAnswer = true
        Orch->>Conv: SummarizeAndStoreAsync()
        Notifier-->>UI: ReceiveCompleted (Functions only)
        Orch-->>User: Return answer (short-circuit)
    else HasRelevantContext = true
        Note over Orch: Carry historyContext forward into pipeline
    end
    Orch->>Notifier: CompleteStep("CheckHistory")
    Notifier-->>UI: ReceiveStatus (Completed)

    Note over Orch: Step 3: DecomposeTasks (single LLM call)
    Orch->>Notifier: BeginStep("DecomposeTasks")
    Notifier-->>UI: ReceiveStatus (Running)
    Orch->>Config: Load skill descriptions for planner system prompt
    Config-->>Orch: skill descriptions
    Orch->>LLM: Decompose prompt into task plan
    LLM-->>Orch: List of TaskPlanItem (group, skill, deps, promptToSend)
    Orch->>Notifier: CompleteStep("DecomposeTasks")
    Notifier-->>UI: ReceiveStatus (Completed)

    Note over Orch: Step 4: GetSkillsByName (config lookup, no LLM)
    Orch->>Config: GetSkillsByNameAsync(uniqueSkillNames)
    Config-->>Orch: List of AgentSkill configs

    Note over Orch: Step 4a: ValidatePlan (Console only, no LLM)
    Note right of Orch: PlanValidator.RemoveUnknownSkillsAndReindex<br/>removes unknown skills and re-indexes DependsOn

    Note over Orch: Step 5: TopologicalSort (no I/O)
    Note over Orch: PlanValidator.ToDag() then TopologicalSort.Sort()

    Note over Orch: Step 6: ExecuteTasks
    Orch->>Notifier: BeginStep("ExecuteTasks")
    Notifier-->>UI: ReceiveStatus (Running)
    loop For each task in topological order
        Orch->>Notifier: BeginSkill("Task_{idx}", "[skill] task label (model: ...)")
        Notifier-->>UI: ReceiveStatus (task Running)

        alt Console App
            Note right of Orch: GenerateSkillPromptAsync (LLM call):<br/>1. LLM scopes user query to this task<br/>2. Code appends structured fields from ExpectedSkillInput<br/>(aspectName, uiContext, domainKnowledge, factualData)
            Orch->>LLM: Scope prompt to task
            LLM-->>Orch: focused prompt
        else Functions App
            Note right of Orch: Direct assembly:<br/>task.PromptToSend<br/>+ ## Input from upstream tasks (dep outputs)<br/>+ domain knowledge (for downstream tasks only)<br/>+ history context (for root tasks only)
        end

        Orch->>LLM: Run agent with tools
        LLM->>LLM: Tool calls (AspectTools, ReportingTools, etc.)

        alt NeedsUserInput = true (max 5 rounds)
            Orch->>Notifier: SkillNeedsInput("Task_{idx}", question, round)
            Notifier-->>UI: ReceiveUserInputRequest

            alt Functions App
                Note over Orch: NotifyUserInputNeededActivity → SignalR<br/>(returns false if SignalR not configured → breaks)
            end

            Note over Orch: Console: WaitForUserInputAsync()<br/>Functions: WaitForExternalEvent("UserInput_{skillName}") + 5-min timeout
            User->>Entry: user response
            Entry->>Orch: resume with input
            Orch->>LLM: Re-execute with base prompt + user response (round N)
        end

        Note right of Orch: memory[taskIndex] = result<br/>(keyed by index, not skill name)
        Orch->>Notifier: CompleteSkill("Task_{idx}")
        Notifier-->>UI: ReceiveStatus (task Completed)
    end

    Note right of Orch: Assemble per-group GroupResult[]<br/>Merge into CXOAgentResponse

    Orch->>Notifier: CompleteStep("ExecuteTasks")
    Notifier-->>UI: ReceiveStatus (Completed)

    Note over Orch: Step 7: Summarize & Store
    Orch->>Conv: SummarizeAndStoreAsync(userId, sessionId, sessionMessages)
    Note right of Conv: Stores conversation summary<br/>+ extracts facts to memory

    Notifier-->>UI: ReceiveCompleted (Functions only)
    Orch-->>User: Return final result
```

---

## Configuration Store Flow

The configuration store holds skill definitions (name, description, tools, model, system prompt) and aspect configurations.

```mermaid
flowchart TB
    subgraph Source["Source Data"]
        SEED_JSON["StoreConfigs/SeedData.json<br/>+ StoreConfigs/AspectConfigs/*.json"]
    end

    subgraph ConsoleFlow["Console App Path"]
        JSON_PROVIDER["TreeJsonConfigurationStoreProvider<br/>Reads SeedData.json at runtime<br/>In-memory keyword matching"]
    end

    subgraph FunctionsFlow["Functions App Path"]
        SEEDER["TreeConfigurationStoreSeeder<br/>One-time: uploads configs to index"]
        SEARCH_PROVIDER["TreeConfigurationStoreProvider<br/>Uses IAzureSearchProvider"]
        AI_SEARCH["Azure AI Search Index<br/>'configurations'<br/>Vector + filter queries"]
        EMBED_CFG["OpenAI Embeddings<br/>text-embedding-3-small"]
    end

    subgraph Consumers["Consumers"]
        GET_SKILLS["DecomposeTasksAsync()<br/>+ GetSkillsByNameAsync()"]
        ASPECT_TOOLS["AspectTools.GetAspectsConfigAsync()"]
    end

    SEED_JSON -->|"reads directly"| JSON_PROVIDER
    SEED_JSON -->|"seeds once"| SEEDER
    SEEDER -->|"embed + upload"| AI_SEARCH
    SEEDER -.->|"GenerateEmbeddingAsync()"| EMBED_CFG
    SEARCH_PROVIDER -->|"SearchAsync + VectorSearch"| AI_SEARCH
    JSON_PROVIDER --> GET_SKILLS
    SEARCH_PROVIDER --> GET_SKILLS
    JSON_PROVIDER --> ASPECT_TOOLS
    SEARCH_PROVIDER --> ASPECT_TOOLS
```

### Environment Settings (Functions)

| Setting | Used By | Purpose |
|---|---|---|
| `SearchServiceEndpoint` | `AzureSearchProvider` | Azure AI Search service URL |
| `SearchIndexName` | `AzureSearchProvider` | Index containing skill configurations |
| `EmbeddingDeployment` | `TreeConfigurationStoreProvider` | OpenAI embedding model for vector queries |
| `AzureOpenAIEndpoint` | All LLM calls + embeddings | Azure OpenAI resource URL |

---

## Memory Store Flow

The memory store provides long-term user preferences and domain knowledge via vector similarity search.

```mermaid
flowchart TB
    subgraph Writes["Write Paths"]
        UI_CTX["Step 1: EnhancePrompt<br/>UserContext handled internally<br/>(entity, filters, params)"]
        EXTRACT["Step 7: SummarizeAndStoreAsync()<br/>Extracts facts from conversation"]
    end

    subgraph Store["Memory Store<br/>—<br/>Console: FileMemoryStore (JSON file)<br/>Functions: CosmosMemoryStore (Cosmos DB)"]
        direction TB
        EMBED_MEM["MemoryEmbedder<br/>text-embedding-3-small"]
        CONFLICT["MemoryConflictResolver<br/>LLM merges or deduplicates"]
        FACTS["Stored Facts<br/>userId | scope | fact | category<br/>embedding[] | tags | timestamps"]
    end

    subgraph Reads["Read Paths"]
        PREF["Step 1: GetUserPreference()<br/>RecallAsync(userId, prompt, scope=User)"]
        ASPECT_MEM["Step 1: GetUserPreference()<br/>RecallAsync('system', prompt, scope=Aspect)"]
    end

    UI_CTX -->|"StoreFactsAsync()"| EMBED_MEM
    EXTRACT -->|"ExtractAndStoreAsync()"| EMBED_MEM
    EMBED_MEM -->|"embed + similarity check"| CONFLICT
    CONFLICT -->|"Add / Update / Noop"| FACTS
    FACTS -->|"VectorDistance() query"| PREF
    FACTS -->|"VectorDistance() query"| ASPECT_MEM

    subgraph CosmosDetail["Cosmos DB (Functions)"]
        CONTAINER["Container: 'memory'<br/>Partition key: userId<br/>Vector index: quantizedFlat"]
    end

    subgraph FileDetail["File Store (Console)"]
        FILE["memory_store.json<br/>Embeddings cached locally"]
    end

    FACTS -.-> CosmosDetail
    FACTS -.-> FileDetail
```

### Environment Settings (Functions — Cosmos)

| Setting | Path in JSON | Purpose |
|---|---|---|
| Account endpoint | `cosmosDbsMaps:MemoryStoreDB:accountEndpoint` | Cosmos DB account URL |
| Database | `cosmosDbsMaps:MemoryStoreDB:databaseId` | Database name (e.g., `cxoai`) |
| Container | `cosmosDbsMaps:MemoryStoreDB:containerId` | Container for memory facts (e.g., `memory`) |
| Lease DB | `cosmosDbsMaps:MemoryStoreDB:leaseDatabaseId` | Change feed lease database |
| Lease container | `cosmosDbsMaps:MemoryStoreDB:leaseContainerId` | Change feed lease container |

---

## Status Notification Flow

```mermaid
sequenceDiagram
    participant Orch as Orchestrator
    participant Notifier as IStatusNotifier
    participant SignalR as Azure SignalR Service
    participant Console as Console Output
    participant UI as Client / Browser

    Note over Orch: Every step publishes status

    alt Console App
        Orch->>Notifier: PublishStatusAsync(status)
        Notifier->>Console: PrintToConsole(logger)
        Note over Console: ? EnhancePrompt Completed (850ms)

        Orch->>Notifier: WaitForUserInputAsync(skill, question)
        Notifier->>Console: Console.Write("[skill] Your response: ")
        Console-->>Notifier: Console.ReadLine()
        Notifier-->>Orch: user input string
    end

    alt Functions App
        Orch->>Notifier: PublishStatusActivity(sessionId, status)
        Notifier->>SignalR: Clients.Group(sessionId).SendAsync("ReceiveStatus", status)
        SignalR-->>UI: ReceiveStatus event (WebSocket)

        Orch->>Notifier: NotifyUserInputNeededActivity(sessionId, skill, prompt, skillResult)
        Note right of Notifier: Returns bool — false if SignalR not configured
        Notifier->>SignalR: SendAsync("ReceiveUserInputRequest", skill, prompt, sessionId, instanceId, skillResult)
        SignalR-->>UI: ReceiveUserInputRequest event

        UI->>Orch: POST /api/instances/{id}/skills/{name}/input (Durable)
        Note over Orch: WaitForExternalEvent("UserInput_{skillName}") resumes

        Note over Orch: On orchestration completion (any exit path):
        Orch->>Notifier: PublishCompletedActivity(sessionId, result)
        Notifier->>SignalR: Clients.Group(sessionId).SendAsync("ReceiveCompleted", {sessionId, result})
        SignalR-->>UI: ReceiveCompleted event
    end
```

### SignalR Endpoints (Functions)

| Endpoint | Method | Purpose |
|---|---|---|
| `/api/negotiate?sessionId={id}` | POST | Returns SignalR connection info, adds client to session group |
| `/api/instances/{id}/skills/{name}/input` | POST | Durable Functions external event — raises `UserInput_{skillName}` for the orchestrator |

### SignalR Hub Events (pushed to client)

| Event | Payload | When |
|---|---|---|
| `ReceiveStatus` | `OrchestratorStatus` (full snapshot) | Every `BeginStep` / `CompleteStep` / `BeginSkill` / `CompleteSkill` / `SkillNeedsInput` / `FailStep` |
| `ReceiveUserInputRequest` | `skillName`, `prompt`, `sessionId`, `instanceId`, `skillResult` | When a skill needs user input |
| `ReceiveCompleted` | `{ sessionId, result: CXOAgentResponse }` | When the orchestration finishes (success, error, or short-circuit) |

---

## Skill Execution Detail

```mermaid
flowchart TB
    subgraph SkillLoop["Step 6: Task Execution Loop"]
        direction TB
        TOPO["Topological order: [Task_0, Task_1, ...]"]

        subgraph Task0["Task_0: AspectSkill (root — no dependencies)"]
            RESOLVE1["ResolveTools()<br/>AspectTools.GetAspectsConfigAsync<br/>AspectTools.AspectRequestCreatorAsync<br/>AspectTools.GetAspectResultAsync"]
            BUILD1_CON["Console: GenerateSkillPromptAsync<br/>1. LLM scopes user query to task<br/>2. Append structured fields from ExpectedSkillInput<br/>(aspectName, uiContext, domainKnowledge)"]
            BUILD1_FN["Functions: Direct assembly<br/>= task.PromptToSend<br/>+ history context (root tasks only)"]
            EXEC1["ExecuteSkillActivity<br/>LLM agent with tool calling"]
            RESULT1["memory#91;0#93; = result<br/>(keyed by task index)"]
        end

        subgraph Task1["Task_1: ReportingSkill (depends on Task_0)"]
            RESOLVE2["ResolveTools()<br/>ReportingTools.GetTemplatesAsync<br/>ReportingTools.GenerateExcelAsync"]
            BUILD2_CON["Console: GenerateSkillPromptAsync<br/>1. LLM scopes user query to task<br/>2. Append structured fields<br/>(factualData = upstream outputs)"]
            BUILD2_FN["Functions: Direct assembly<br/>= task.PromptToSend<br/>+ ## Input from upstream tasks (Task_0 output)<br/>+ domain knowledge (downstream only)"]
            EXEC2["ExecuteSkillActivity<br/>LLM agent with tool calling"]
            RESULT2["memory#91;1#93; = result<br/>(keyed by task index)"]
        end

        subgraph UserInput["User Input Loop (max 5 rounds)"]
            NEED["NeedsInput = true"]
            NOTIFY_UI["Notify UI<br/>(SignalR / Console)"]
            CHECK_SIG["Functions: check NotifyUserInputAsync result<br/>(false → break with failure)"]
            WAIT["Wait for user response<br/>(ExternalEvent + 5-min timeout / WaitForUserInputAsync)"]
            REEXEC["Re-execute with<br/>base prompt + user response (round N)"]
        end
    end

    subgraph GroupAssembly["Result Assembly"]
        GROUPS["Per-group GroupResult#91;#93;<br/>ordered by Group number"]
        MERGE["Merge into CXOAgentResponse<br/>single group → direct response<br/>multi group → ## Answer N sections"]
    end

    subgraph ConfigRead["Config Store Read"]
        CFG_STORE["JSON / Search Index"]
    end

    TOPO --> RESOLVE1
    RESOLVE1 --> BUILD1_CON
    RESOLVE1 --> BUILD1_FN
    BUILD1_CON --> EXEC1
    BUILD1_FN --> EXEC1
    EXEC1 -->|"NeedsInput=false"| RESULT1
    EXEC1 -->|"NeedsInput=true"| NEED
    NEED --> NOTIFY_UI
    NOTIFY_UI --> CHECK_SIG
    CHECK_SIG --> WAIT
    WAIT --> REEXEC
    REEXEC --> EXEC1

    RESULT1 -->|"dependency output"| BUILD2_CON
    RESULT1 -->|"dependency output"| BUILD2_FN
    RESOLVE2 --> BUILD2_CON
    RESOLVE2 --> BUILD2_FN
    BUILD2_CON --> EXEC2
    BUILD2_FN --> EXEC2
    EXEC2 --> RESULT2

    RESULT1 --> GROUPS
    RESULT2 --> GROUPS
    GROUPS --> MERGE

    EXEC1 -.->|"GetAspectsConfigAsync()"| CFG_STORE
```

---

## Conversation History Flow

```mermaid
flowchart TB
    subgraph Run1["RunAsync #1: 'show me csat of walmart'"]
        R1_ENHANCE["EnhancePrompt"]
        R1_INTENT["ClassifyIntent → DataAction"]
        R1_HISTORY["CheckHistory → no history yet"]
        R1_DECOMPOSE["DecomposeTasks → Task_0: AspectSkill"]
        R1_EXEC["ExecuteTasks → CSAT: 72.45"]
        R1_SUMM["Summarize:<br/>**User Asked:** show me csat of walmart<br/>| Metric | Value |<br/>| CSAT | 72.45 |"]
    end

    subgraph ConvStore["Conversation Store"]
        CONV_DB["summaries[]<br/>filtered by: sessionId"]
    end

    subgraph MemStore["Memory Store"]
        MEM_DB["Extracted facts:<br/>- User asked about CSAT for Walmart<br/>- CSAT value was 72.45"]
    end

    subgraph Run2["RunAsync #2: 'what was the csat value?'"]
        R2_ENHANCE["EnhancePrompt<br/>(memory recalls CSAT context)"]
        R2_INTENT["ClassifyIntent → DataAction"]
        R2_HISTORY["CheckHistory → CanAnswer=true<br/>Answer: 'CSAT for Walmart: 72.45'"]
        R2_RETURN["Return answer → SHORT CIRCUIT<br/>No tasks executed"]
    end

    subgraph Run3["RunAsync #3: 'export to word'"]
        R3_ENHANCE["EnhancePrompt"]
        R3_INTENT["ClassifyIntent → DataAction"]
        R3_HISTORY["CheckHistory → HasRelevantContext=true<br/>RelevantContext: 'CSAT: 72.45 for Walmart'"]
        R3_DECOMPOSE["DecomposeTasks → Task_0: ReportingSkill"]
        R3_EXEC["ExecuteTasks → ReportingSkill<br/>uses history context as input data"]
    end

    R1_SUMM -->|"SummarizeAndStoreAsync()"| CONV_DB
    R1_SUMM -->|"SummarizeAndStoreAsync()"| MEM_DB
    CONV_DB -->|"GetSessionSummaryAsync()"| R2_HISTORY
    MEM_DB -->|"RecallAsync(userId)"| R2_ENHANCE
    CONV_DB -->|"GetSessionSummaryAsync()"| R3_HISTORY
```

---

## Environment Configuration Structure

```mermaid
flowchart LR
    subgraph EnvVar["Environment Variables"]
        CXOAI_ENV["CXOAI_ENVIRONMENT<br/>(test/ppe/prvw/prod)"]
        GENEVA["GENEVA_ACCOUNT<br/>GENEVA_NAMESPACE"]
        SIGNALR_CONN["AzureSignalRConnectionString"]
    end

    subgraph SettingsFile["EnvironmentSettings/{env}.environment.settings.json"]
        OPENAI["AzureOpenAIEndpoint"]
        MODEL["AzureOpenAIModel"]
        EMBEDDING["EmbeddingDeployment"]
        SEARCH_EP["SearchServiceEndpoint"]
        SEARCH_IDX["SearchIndexName"]
        COSMOS["cosmosDbsMaps.MemoryStoreDB<br/>.accountEndpoint / .databaseId / .containerId"]
        KV["KeyVaultUrl"]
        STORAGE["AppStorageAccountName"]
    end

    subgraph DI["DI Registrations in Program.cs"]
        P_SEARCH["IAzureSearchProvider<br/>(SearchEndpoint + IndexName)"]
        P_CONFIG["ITreeConfigurationStoreProvider<br/>(SearchProvider + OpenAI + Embedding)"]
        P_COSMOS["ICosmosDbProvider<br/>(cosmosDbsMaps config)"]
        P_MEMORY["IMemoryStore → CosmosMemoryStore<br/>(CosmosProvider + OpenAI + Embedding)"]
        P_SIGNALR["ServiceHubContext<br/>(SignalR connection string)"]
    end

    CXOAI_ENV -->|"selects file"| SettingsFile
    OPENAI --> P_CONFIG
    OPENAI --> P_MEMORY
    EMBEDDING --> P_CONFIG
    EMBEDDING --> P_MEMORY
    SEARCH_EP --> P_SEARCH
    SEARCH_IDX --> P_SEARCH
    COSMOS --> P_COSMOS
    P_SEARCH --> P_CONFIG
    P_COSMOS --> P_MEMORY
    SIGNALR_CONN --> P_SIGNALR
```

### Sample: `test.environment.settings.json`

```json
{
  "AzureOpenAIEndpoint": "https://your-openai-test.openai.azure.com/",
  "AzureOpenAIModel": "gpt-4o-mini",
  "EmbeddingDeployment": "text-embedding-3-small",
  "SearchServiceEndpoint": "https://your-search-test.search.windows.net",
  "SearchIndexName": "configurations",
  "KeyVaultUrl": "https://your-kv-test.vault.azure.net/",
  "ApplicationInsightsConnectionString": "",
  "cosmosDbsMaps": {
    "MemoryStoreDB": {
      "databaseId": "cxoai",
      "containerId": "memory",
      "leaseDatabaseId": "cxoai",
      "leaseContainerId": "memory-leases",
      "accountEndpoint": "https://your-cosmos-test.documents.azure.com:443/"
    }
  }
}
```

---

## Console → Functions Implementation Mapping

| Concern | Console App | Functions App |
|---|---|---|
| **Orchestrator** | `SkillOrchestrator.RunAsync()` (single method) | `CxoaiOrchestrator.OrchestratorMain` + `SkillExecutionSubOrchestrator` |
| **Step execution** | Direct `await` calls in `RunAsync` via `IOrchestratorStepService` | `context.CallActivityAsync()` per step |
| **Task decomposition** | `_stepService.DecomposeTasksAsync()` → `List<TaskPlanItem>` | `DecomposeTasksActivity` → `List<TaskPlanItem>` |
| **Skill config lookup** | `_stepService.GetSkillsByNameAsync(names)` | `GetSkillsByNameActivity(names)` |
| **Plan validation** | `PlanValidator.RemoveUnknownSkillsAndReindex()` (removes unknown skills, re-indexes DependsOn) | `PlanValidator.ToDag()` only (no unknown-skill removal in orchestrator) |
| **Prompt generation** | `GenerateSkillPromptAsync()` per task — LLM scopes query + appends structured fields from `ExpectedSkillInput` | Direct `task.PromptToSend` + manual assembly of upstream outputs, domain knowledge, and history context |
| **Task loop** | `foreach` with `await ExecuteSkillAsync()` per task | `CallSubOrchestratorAsync` → `foreach` + `CallActivityAsync` per task |
| **Execution memory** | `Dictionary<string, CXOAgentResponse>` (keyed by task index) | `Dictionary<string, SkillExecutionResult>` (keyed by task index) |
| **Result assembly** | Per-group `GroupResult[]` merged into `CXOAgentResponse` | Per-group `GroupResult[]` merged into `CXOAgentResponse` |
| **State** | `OrchestratorState` (in-memory cache with `TryGet`/`Set`) | Durable orchestrator checkpoint (automatic) |
| **Config store** | `TreeJsonConfigurationStoreProvider("StoreConfigs/SeedData.json")` | `ITreeConfigurationStoreProvider` via DI (Search index) |
| **Memory store** | `FileMemoryStore` (local JSON + embeddings) | `CosmosMemoryStore` via DI (Cosmos DB vector search) |
| **Conversation store** | `InMemoryConversationStore` | `InMemoryConversationStore` (or `CosmosConversationStore`) |
| **Knowledge graph** | `KnowledgeGraphTools` → `Configuration/KnowledgeGraph.json` | Same (file bundled in output) |
| **Status publish** | `ConsoleStatusNotifier.PrintToConsole()` | `PublishStatusActivity` → SignalR `SendAsync("ReceiveStatus")` |
| **Completed notification** | N/A (return value) | `PublishCompletedActivity` → SignalR `SendAsync("ReceiveCompleted", { sessionId, result })` |
| **User input wait** | `_notifier.WaitForUserInputAsync()` (max 5 rounds) | `WaitForExternalEvent("UserInput_{skillName}")` with 5-min durable timeout (max 5 rounds) |
| **User input notify** | `_notifier.PublishStatusAsync()` with `SkillNeedsInput` | `NotifyUserInputNeededActivity` → SignalR `SendAsync("ReceiveUserInputRequest", skill, prompt, sessionId, instanceId, skillResult)` — returns `bool` (false if SignalR unavailable) |
| **User input submit** | Keyboard input via `IStatusNotifier` | `POST /api/instances/{id}/skills/{name}/input` |
| **Access token** | N/A | `OrchestratorInput.AccessToken` → `ExecuteSkillInput.AccessToken` → `IUserAuthContext.AccessToken` (scoped per activity) |
| **Tool session** | `_stepService.SetToolSession(sessionId)` (once before loop) | `_stepService.SetToolSession(sessionId)` (per `ExecuteSkillActivity`) |
| **Error handling** | Exception propagates to caller | `try/catch` in `OrchestratorMain` + `SkillExecutionSubOrchestrator` → returns `CXOAgentResponse { IsSuccess = false }` + best-effort `PublishCompletedActivity` |
| **Result delivery** | Return `CXOAgentResponse` to caller | `PublishCompletedActivity` via SignalR + Durable Functions status query: `GET /runtime/webhooks/durableTask/instances/{id}` |

---

## Key Design Decisions

| Decision | Reason |
|---|---|
| **Intent classification before history check** | Definitional queries ("what is csat?") go straight to AnswerFromKnowledge without hitting the history store; unknown queries exit immediately |
| **DecomposeTasks replaces 3 LLM calls** | Old pipeline had GetRelevantSkills + GetSkillDAG + GetSkillPrompts (3 LLM calls). New `DecomposeTasksActivity` produces `List<TaskPlanItem>` (group, skill, deps, promptToSend) in a single LLM call |
| **Task-index keyed memory** | Memory is keyed by task index (not skill name) so multiple tasks using the same skill don't overwrite each other |
| **Two config providers (JSON / Search Index)** | Console uses local JSON for fast iteration; Functions uses Search Index for vector-based retrieval at scale |
| **Two memory providers (File / Cosmos)** | Console caches embeddings locally; Functions uses Cosmos DB vector search for production persistence |
| **`IStatusNotifier` abstraction** | Console prints to terminal; Functions pushes via SignalR — same orchestrator code, different notification channel |
| **`IOrchestratorStepService` abstraction** | Console and Functions share the same step logic; only flow control differs (direct `await` vs. `CallActivityAsync`) |
| **Knowledge graph with LLM fallback** | Static domain topology (aspects → metrics → aliases) loaded from `KnowledgeGraph.json`; `KnowledgeGraphTools` first tries text/tag matching, then falls back to `gpt-4o` LLM for semantic node resolution |
| **Memory conflict resolution** | `MemoryConflictResolver` uses LLM to merge/deduplicate facts — prevents contradictory preferences |
| **Conversation history as session summaries** | Session-scoped summaries (per userId + sessionId); summaries are compact (not raw conversation) |
| **TopologicalSort in orchestrator** | Deterministic, no I/O — safe for Durable Functions replay semantics |
| **Sub-orchestrator for task loop** | Isolates `foreach` + `WaitForExternalEvent` pattern — enables independent retry and monitoring |
| **`PublishStatusActivity` (not direct I/O)** | Durable orchestrators can't do I/O — status push must happen in an activity |
| **`PublishCompletedActivity` on every exit path** | Ensures UI always receives a final `ReceiveCompleted` event, even for short-circuit and error paths |
| **SignalR session groups** | Each orchestration instance gets its own group — status updates target only the requesting client |
| **Max 5 user input rounds** | Prevents infinite loops when skills repeatedly request user input |
| **`PlanValidator.RemoveUnknownSkillsAndReindex`** | Safely removes tasks referencing unknown skills and re-indexes all `DependsOn` references to prevent index-shift corruption |
| **Console `GenerateSkillPromptAsync` vs Functions direct assembly** | Console uses an additional LLM call per task to scope the user query + appends structured fields from `ExpectedSkillInput` keywords. Functions uses `task.PromptToSend` directly with manual context assembly for simpler serialization across durable activities |
| **Domain knowledge injection for downstream tasks only (Functions)** | Root tasks already have their prompts from the planner; downstream tasks need domain context (metric definitions, relationships) to interpret upstream data correctly |
| **Access token propagation via `IUserAuthContext`** | Scoped service populated in each `ExecuteSkillActivity` — tools accessing authenticated APIs get the user's bearer token without threading it through every method |
| **Graceful error handling in orchestrators** | Both `OrchestratorMain` and `SkillExecutionSubOrchestrator` wrap their logic in `try/catch`, returning `CXOAgentResponse { IsSuccess = false }` with a user-friendly message via `OrchestratorMessages.GracefulError` |
| **`NotifyUserInputAsync` returns `bool`** | If SignalR is not configured (e.g., local dev), the activity returns `false` and the skill execution breaks out gracefully instead of hanging on `WaitForExternalEvent` forever |
