# Azure Advisor Agent

An AI-powered agent that helps you optimize, protect, and modernize your Azure workloads. It provides actionable recommendations across cost optimization, reliability, retirement planning, and outage remediation — all through a conversational interface backed by Azure Durable Functions and OpenAI.

## Architecture Overview

```
┌──────────────────┐        ┌─────────────────────────────────────────────┐
│  advisor-agent-ui │◄─────►│  AdvisorAgent.Functions  (Azure Functions)  │
│  (Static SPA)     │SignalR │  ├─ HTTP Triggers & SignalR negotiate      │
└──────────────────┘        │  ├─ Durable Orchestrator pipeline           │
                            │  └─ Activity functions (skill execution)    │
                            ├─────────────────────────────────────────────┤
                            │  AdvisorAgent.Core                          │
                            │  ├─ Intent classification & task planning   │
                            │  ├─ Conversation store (Cosmos DB / Memory) │
                            │  └─ Azure context resolution                │
                            ├─────────────────────────────────────────────┤
                            │  AdvisorAgent.Tools                         │
                            │  ├─ Advisor Recommendations                 │
                            │  ├─ Cost Optimization                       │
                            │  ├─ Resiliency Assessment                   │
                            │  ├─ Retirement Analysis                     │
                            │  ├─ Outage Remediation                      │
                            │  ├─ Resource Graph Queries                  │
                            │  └─ Subscription Management                 │
                            └─────────────────────────────────────────────┘
```

### Projects

| Project | Description |
|---|---|
| **AdvisorAgent.Functions** | Azure Functions host — HTTP triggers, SignalR hub, Durable orchestrator & activities |
| **AdvisorAgent.Core** | Shared domain logic — orchestration service, conversation store, intent/context models |
| **AdvisorAgent.Tools** | Tool classes invoked by skills (Advisor, Cost, Resiliency, Retirement, Outage, Resource Graph, Subscriptions) |
| **AdvisorAgent.Tests** | Unit tests (xUnit + Moq) |
| **advisor-agent-ui** | Static HTML/JS/CSS frontend with SignalR real-time updates |

---

## Prerequisites

| Tool | Version | Install |
|---|---|---|
| **.NET SDK** | 9.0+ | https://dotnet.microsoft.com/download/dotnet/9.0 |
| **Azure Functions Core Tools** | v4 | `npm i -g azure-functions-core-tools@4` |
| **Node.js** | 18+ | https://nodejs.org |
| **Azurite** (local storage emulator) | latest | `npm i -g azurite` |
| **Azure CLI** | latest | https://learn.microsoft.com/cli/azure/install-azure-cli |

You also need access to:

- An **Azure OpenAI** deployment (default model: `gpt-4.1`)
- *(Optional)* **Azure Cosmos DB** account for persistent conversation storage (falls back to in-memory)
- *(Optional)* **Durable Task Scheduler** for managed durable orchestration (default: local emulator at `http://localhost:8080`)

---

## Getting Started

### 1. Clone the repository

```bash
git clone <repo-url>
cd EngOps-AdvisorAgent
```

### 2. Configure the backend

The backend reads configuration from environment variables. A default `local.settings.json` is provided at:

```
AdvisorAgent/src/AdvisorAgent.Functions/local.settings.json
```

Key settings to review/update:

| Variable | Purpose | Default |
|---|---|---|
| `AZURE_OPENAI_ENDPOINT` | Azure OpenAI resource endpoint | *(required)* |
| `AZURE_OPENAI_MODEL` | Deployed model name | `gpt-4.1` |
| `AzureWebJobsStorage` | Storage connection (Azurite for local) | `UseDevelopmentStorage=true` |
| `DURABLE_TASK_SCHEDULER_CONNECTION_STRING` | Durable Task scheduler endpoint | `Endpoint=http://localhost:8080;Authentication=None` |
| `CONVERSATION_STORE_TYPE` | `Cosmos` or `InMemory` | `Cosmos` |
| `COSMOS_CONVERSATION_ENDPOINT` | Cosmos DB endpoint (when store type = Cosmos) | — |
| `COSMOS_CONVERSATION_DATABASE` | Cosmos DB database name | `AdvisorScore` |
| `COSMOS_CONVERSATION_CONTAINER` | Cosmos DB container name | `Conversations` |

### 3. Authenticate with Azure

The backend uses `DefaultAzureCredential`. For local development, sign in via the Azure CLI:

```bash
az login
```

---

## Running the Backend

```bash
# Start Azurite (in a separate terminal)
azurite --silent

# Navigate to the Functions project
cd AdvisorAgent/src/AdvisorAgent.Functions

# Restore and start
dotnet restore
func start
```

The Functions host will start on **http://localhost:7071**. Available endpoints:

| Method | Route | Description |
|---|---|---|
| POST | `/api/advisor/orchestrate` | Start an advisor orchestration |
| GET | `/api/advisor/status/{instanceId}` | Poll orchestration status |
| POST | `/api/negotiate` | SignalR connection negotiation |
| GET | `/api/advisor/health` | Health check |

---

## Running the Frontend

```bash
# Navigate to the UI directory
cd advisor-agent-ui

# Install dependencies
npm install

# Start the dev server
npm start
```

The UI will be served at **http://localhost:3000**. It connects to the backend at `http://localhost:7071/api` by default (configurable in `js/config.js`).

The frontend communicates with the backend via:
- **SignalR** for real-time orchestration status updates
- **HTTP polling** as a fallback when SignalR is unavailable

---

## Running Tests

```bash
cd AdvisorAgent

# Run all tests
dotnet test

# Run with verbose output
dotnet test --logger "console;verbosity=detailed"
```

---

## Project Structure

```
EngOps-AdvisorAgent/
├── README.md
├── advisor-agent-ui/                # Frontend SPA
│   ├── index.html
│   ├── css/styles.css
│   └── js/
│       ├── api-client.js            # HTTP client for backend APIs
│       ├── app.js                   # Application bootstrap
│       ├── config.js                # API URLs & polling config
│       ├── signalr-client.js        # SignalR connection manager
│       └── ui.js                    # DOM rendering helpers
├── AdvisorAgent/
│   ├── AdvisorAgent.slnx            # Solution file
│   ├── src/
│   │   ├── AdvisorAgent.Core/       # Domain models & services
│   │   │   ├── AzureContext/        # Subscription/context resolution
│   │   │   ├── Conversation/        # Cosmos & in-memory conversation stores
│   │   │   ├── Models/              # Request/response DTOs
│   │   │   └── Skills/              # Orchestration service & skill definitions
│   │   ├── AdvisorAgent.Functions/  # Azure Functions host
│   │   │   ├── Configuration/       # skills.json – skill catalog
│   │   │   ├── Orchestration/       # Durable orchestrator & activities
│   │   │   ├── Triggers/            # HTTP triggers & SignalR negotiate
│   │   │   └── Program.cs           # DI & startup configuration
│   │   └── AdvisorAgent.Tools/      # Tool implementations
│   └── tests/
│       └── AdvisorAgent.Tests/      # Unit tests (xUnit + Moq)
└── docs/                            # VitePress documentation site
```

---

## Skills

Skills are defined in [`AdvisorAgent/src/AdvisorAgent.Functions/Configuration/skills.json`](AdvisorAgent/src/AdvisorAgent.Functions/Configuration/skills.json) and represent domain-specific AI capabilities:

| Skill | Description |
|---|---|
| **RetirementSkill** | Identifies retiring Azure resources and generates migration action plans |
| **OutageRemediationSkill** | Analyzes incidents and produces post-outage remediation plans |
| **ResiliencySkill** | Assesses workload resiliency posture and recommends improvements |
| **CostOptimizationSkill** | Identifies cost-saving opportunities across subscriptions |
| **AdvisorRecommendationSkill** | Fetches and synthesizes Azure Advisor recommendations |

---

## Documentation

A VitePress documentation site is available under `docs/`. To run it locally:

```bash
cd docs
npm install
npx vitepress dev
```
