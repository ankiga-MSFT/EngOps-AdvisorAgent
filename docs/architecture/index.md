# Architecture Overview

Advisor Agent is an **AI-powered Azure advisory system** built on a multi-tier serverless architecture. It combines Azure Functions with Durable Orchestration, Azure OpenAI, and Azure Resource Graph to deliver actionable recommendations across reliability, cost, retirement, outage remediation, and architecture domains.

## High-Level Architecture

```
┌──────────────┐       ┌──────────────────────────┐       ┌──────────────────┐
│   Client     │──────▶│  Azure Functions Host     │──────▶│  Azure OpenAI    │
│  (HTTP/SigR) │◀──────│  (Durable Orchestration)  │◀──────│  (GPT-4o / 4.1) │
└──────────────┘       └────────────┬─────────────┘       └──────────────────┘
                                    │
                       ┌────────────┼────────────┐
                       ▼            ▼            ▼
                ┌────────────┐ ┌─────────┐ ┌──────────────┐
                │ Azure      │ │ Cosmos  │ │ ARM / Azure  │
                │ Resource   │ │ DB      │ │ Service      │
                │ Graph      │ │         │ │ Health       │
                └────────────┘ └─────────┘ └──────────────┘
```

## Core Design Principles

| Principle | How It's Applied |
|-----------|-----------------|
| **Skill-Based Orchestration** | Capabilities are modular skills defined in a JSON catalog, each with its own tools, system prompt, and model configuration |
| **LLM-Driven Routing** | Azure OpenAI classifies intent, decomposes tasks, and generates skill-specific prompts — no hard-coded routing rules |
| **Durable Execution** | Azure Durable Functions provide resilient, resumable, long-running orchestrations with built-in retry and progress tracking |
| **Context Chaining** | Upstream task outputs are fed into downstream skill prompts, enabling multi-step problem solving with data flow |
| **Reflection-Based Tool Binding** | Tools are discovered dynamically via `ClassName-MethodName` patterns — no hardcoded tool references in the orchestrator |
| **Response Budgeting** | Tool responses are truncated to 15K characters to prevent LLM context window overflow |

## Three-Layer Architecture

### 1. Functions Layer (`AdvisorAgent.Functions`)
The serverless entry point and durable orchestration host:
- **HTTP Triggers** — accept user requests and expose status polling
- **Durable Orchestrator** — manages the multi-step pipeline (context → intent → plan → execute)
- **Activity Functions** — atomic units of work called by the orchestrator
- **DI Container** — wires up credentials, skill catalog, tool instances, and stores

### 2. Core Layer (`AdvisorAgent.Core`)
Domain logic, orchestration service, and abstractions:
- **AgentOrchestrationService** — the brain: LLM calls for context extraction, intent classification, task decomposition, and skill execution
- **Skill Definitions** — metadata-driven skill catalog loaded from JSON
- **Task Planning** — DAG-based task plans with topological sort and cycle detection
- **Conversation Store** — multi-turn history persistence (Cosmos DB or in-memory)
- **Domain Models** — `AzureContext`, `UserIntent`, `ConversationTurn`, `AdvisorAgentResponse`

### 3. Tools Layer (`AdvisorAgent.Tools`)
Concrete Azure data retrieval capabilities:
- **ToolBase** — shared ARM REST and Resource Graph helpers
- **7 Tool Classes** — each exposes methods that the LLM can invoke during skill execution
- All tools use the caller's Bearer token for Azure API access

## Key Technologies

| Technology | Purpose |
|------------|---------|
| Azure Functions (isolated) | Serverless compute host |
| Azure Durable Functions | Orchestration, sub-orchestration, activity coordination |
| Azure OpenAI (GPT-4o) | Intent classification, task decomposition, skill execution |
| Azure Resource Graph | KQL-based resource and advisor recommendation queries |
| Azure Cosmos DB | Conversation history persistence |
| Azure SignalR | Real-time progress updates to clients |
| Microsoft.Extensions.AI | AI function invocation middleware |
| DefaultAzureCredential | Managed identity for Azure SDK calls |
