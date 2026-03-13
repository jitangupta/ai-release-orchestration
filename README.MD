# AI-Orchestrated Release Management

An intelligent recommendation engine that uses **RAG (Retrieval-Augmented Generation)** and **probabilistic reasoning** to determine which tenants in a multi-tenant SaaS platform should upgrade to a new release — and why.

Instead of manually reviewing each tenant-release combination, this system analyzes release content (features, bugs, tech debt) against tenant profiles (active features, usage patterns, risk tolerance) and generates per-tenant recommendations: **MUST**, **SHOULD**, or **SKIP** — each with detailed, auditable reasoning.

## Architecture

```
                    ┌──────────────────────────────────────┐
                    │         Ai.Orchestrator               │
                    │       (ASP.NET Core Web API)          │
                    │                                      │
                    │  ┌────────────────────────────────┐  │
                    │  │    RecommendationEngine         │  │
                    │  │    (Pipeline Orchestrator)      │  │
                    │  └──────┬──────────────┬──────────┘  │
                    │         │              │              │
                    │    ┌────▼────┐   ┌─────▼──────┐      │
                    │    │ Qdrant  │   │   LLM      │      │
                    │    │Retriever│   │ Reasoning  │      │
                    │    │Service  │   │ Service    │      │
                    │    └────┬────┘   └─────┬──────┘      │
                    └─────────┼──────────────┼─────────────┘
                              │              │
                     ┌────────▼───┐   ┌──────▼──────────┐
                     │  Qdrant    │   │  Azure OpenAI   │
                     │  Vector DB │   │  (GPT-4o-mini)  │
                     └────────────┘   └─────────────────┘
                              ▲
                              │
                    ┌─────────┴────────────────────────────┐
                    │        EmbeddingService                │
                    │      (.NET Background Worker)         │
                    │                                      │
                    │  Jira Tickets → text-embedding-3-small│
                    │              → Qdrant Vectors         │
                    └──────────────────────────────────────┘
```

### Data Flow

1. **Embed** — The `EmbeddingService` reads Jira ticket exports (stories, bugs, subtasks), generates vector embeddings via Azure OpenAI (`text-embedding-3-small`), and stores them in Qdrant.

2. **Retrieve** — When a recommendation is requested, the `QdrantRetrieverService` performs hybrid semantic search: feature-specific queries for each tenant's active features + contextual queries based on risk tolerance and usage patterns.

3. **Reason** — The `LLMReasoningService` constructs a context-rich prompt with the tenant profile, release metadata, and retrieved tickets, then calls GPT-4o-mini to produce a probabilistic recommendation with reasoning.

4. **Recommend** — Results are returned via REST API and saved to disk for auditability, including the full prompts sent to the LLM.

## Probabilistic Decision Framework

The system uses probability-based reasoning rather than simple rule matching:

| Recommendation | Probability of Impact | Example Scenario |
|---|---|---|
| **MUST** | > 70% | Critical bug in a feature the tenant uses daily with low risk tolerance |
| **SHOULD** | 30–70% | Major bug in a moderately-used feature; beneficial but not urgent |
| **SKIP** | < 30% | Minor bug in a feature the tenant doesn't use or an edge case unlikely to occur |

Factors that influence the probability assessment:
- **Feature overlap** — Does the tenant actively use features affected by this release?
- **Usage intensity** — High daily usage increases likelihood of hitting bugs
- **Risk tolerance** — LOW tolerance tenants get a lower threshold (more conservative)
- **Bug severity** — Critical/security issues weigh more heavily than minor patches

## Tech Stack

| Layer | Technology |
|---|---|
| Language | C# / .NET 8.0 |
| Web API | ASP.NET Core |
| LLM | Azure OpenAI (GPT-4o-mini) |
| Embeddings | Azure OpenAI (text-embedding-3-small, 1536 dims) |
| Vector Database | Qdrant Cloud |
| Containerization | Docker (multi-stage builds) |
| API Docs | Swagger / OpenAPI (Swashbuckle) |

## Project Structure

```
├── Ai.Orchestrator/                # Recommendation engine (Web API)
│   ├── Controllers/
│   │   └── RecommendationController.cs
│   ├── Services/
│   │   ├── RecommendationEngine.cs       # Pipeline orchestrator
│   │   ├── LLMReasoningService.cs        # GPT-4 probabilistic reasoning
│   │   └── QdrantRetrieverService.cs     # Hybrid semantic search
│   ├── Models/
│   │   ├── UpgradeRecommendation.cs
│   │   ├── TenantProfile.cs
│   │   └── ReleaseContext.cs
│   ├── Dockerfile
│   └── appsettings.json
│
├── EmbeddingService/               # Vector embedding worker
│   ├── Services/
│   │   ├── AzureOpenAIService.cs         # Embedding API wrapper
│   │   ├── QdrantService.cs              # Vector DB operations
│   │   └── JiraTicketProcessor.cs        # JSONL parsing + orchestration
│   ├── Dockerfile
│   └── appsettings.json
│
├── data/
│   ├── jira/                       # Jira ticket exports (JSONL)
│   └── tenants.jsonl               # Tenant profile data
│
├── releases/                       # Release metadata (JSON)
│   └── v1.6.0.json
│
└── schemas/                        # Data schema definitions
    ├── jira_schema_definition.md
    ├── release_content_schema_definition.md
    └── tenant_schema_definition.md
```

## Getting Started

### Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Docker](https://www.docker.com/) (optional, for containerized runs)
- Azure OpenAI resource with deployed models:
  - `text-embedding-3-small` (embeddings)
  - `gpt-4o-mini` (reasoning)
- Qdrant instance (cloud or self-hosted)

### Configuration

Update `appsettings.json` in each service with your credentials:

```jsonc
// Ai.Orchestrator/appsettings.json
{
  "AzureOpenAI": {
    "Endpoint": "https://<your-resource>.openai.azure.com/",
    "ApiKey": "<your-api-key>",
    "DeploymentName": "gpt-4o-mini"
  },
  "Qdrant": {
    "Host": "<your-qdrant-host>",
    "ApiKey": "<your-qdrant-api-key>",
    "CollectionName": "jira_tickets_r6"
  }
}
```

### Run

**Step 1 — Generate embeddings** (run once per release cycle):
```bash
cd EmbeddingService
dotnet run
```

**Step 2 — Start the recommendation API**:
```bash
cd Ai.Orchestrator
dotnet run
```

**Step 3 — Request recommendations**:
```bash
curl -X POST https://localhost:7279/api/recommendations/generate \
  -H "Content-Type: application/json" \
  -d '{
    "releaseVersion": "v1.6.0",
    "tenantIds": ["T-SOUTH-051", "T-SOUTH-052"]
  }'
```

### Docker

```bash
# Build
docker build -t ai-orchestrator ./Ai.Orchestrator
docker build -t embedding-service ./EmbeddingService

# Run
docker run -p 8080:8080 ai-orchestrator
docker run embedding-service
```

## Sample Output

```json
{
  "tenantId": "T-CORP-401",
  "releaseVersion": "v1.6.0",
  "recommendation": "MUST",
  "reasoning": "MUST upgrade: CRITICAL bugs in F3 (proration logic) and F4 (payment retry). Tenant uses both features heavily (daily_usage=85) with LOW risk tolerance. Probability of impact exceeds 80% — upgrading is essential to prevent production billing failures.",
  "affectedFeatures": ["F3_SubscriptionMgmt", "F4_BillingPayments"],
  "estimatedImpact": "high"
}
```

## Key Design Decisions

- **RAG over fine-tuning** — The system retrieves actual ticket data at inference time rather than baking knowledge into the model. This means it works with any release without retraining.
- **Probabilistic reasoning over rules** — Instead of hard-coded if/else logic, the LLM evaluates probability of impact, making nuanced decisions that account for interacting factors.
- **Hybrid semantic search** — Combines feature-specific queries with contextual queries (risk-aware, usage-aware) to avoid single-query bias in retrieval.
- **Prompt auditability** — Every LLM prompt is saved to disk, enabling post-hoc review of the reasoning chain behind each recommendation.

## The SaaS Context

This system is built for a **Subscription Management SaaS** platform serving 50+ B2B tenants. The platform manages subscription lifecycles, billing, invoicing, usage metering, and tenant administration across 10 feature modules (F1–F10) and 6 releases (v1.0.0–v1.6.0). Tenants vary widely in feature usage, daily activity, and risk appetite — making one-size-fits-all upgrade strategies ineffective.

## License

This project is provided for educational and demonstration purposes.
