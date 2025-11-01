# AI-Orchestrated Release Management System - Product Summary
An intelligent recommendation engine that analyzes release content (features, bugs, tech debt) against tenant profiles (active features, usage patterns, risk tolerance) to determine upgrade necessity. After a new release proves stable in canary deployment, the system generates per-tenant recommendations (MUST/SHOULD/CAN SKIP upgrade) with detailed reasoning. Human operators review recommendations and approve upgrades, which trigger automated PR creation in Helm repos for GitOps deployment.

```
┌─────────────────────────────────────────────────────────┐
│                    AI Orchestrator                       │
│              (Azure OpenAI GPT-4 + Functions)            │
└─────────────────────────────────────────────────────────┘
                           │
        ┌──────────────────┼──────────────────┐
        ▼                  ▼                  ▼
┌───────────────┐  ┌──────────────┐  ┌──────────────┐
│  Tenant DB    │  │  Release DB  │  │   Jira API   │
│  (Azure SQL)  │  │  (Azure SQL) │  │  (or export) │
└───────────────┘  └──────────────┘  └──────────────┘
        │                  │                  │
        └──────────────────┴──────────────────┘
                           │
                ┌──────────▼──────────┐
                │   RAG System        │
                │  (Release Notes +   │
                │   Tenant History)   │
                └─────────────────────┘
```

## Infrastructure & Deployment - Summary
The **subscription management** SaaS runs on Azure Kubernetes Service (AKS) with multi-tenant isolation via namespace-per-tenant architecture. Helm charts define tenant-specific configurations (feature flags, resource limits, database connections), versioned in a GitOps repository. ArgoCD continuously monitors the Helm repo and automatically syncs approved changes to the cluster, enabling zero-downtime tenant upgrades with rollback capabilities. Each tenant upgrade is a Helm release bump triggered by PR merge, with ArgoCD handling progressive rollout and health checks.

## AI Orchestration System Architecture - Summary
The AI recommendation engine is built on Azure AI Foundry, using GPT-4 for decision reasoning and natural language explanation generation. Jira ticket data (features, bugs, tech debt descriptions) are embedded using text-embedding-3-small and stored in Qdrant vector database for semantic search and retrieval. When a release stabilizes, the system retrieves relevant tickets based on tenant feature usage, constructs context-rich prompts, and invokes Azure OpenAI to generate MUST/SHOULD/SKIP recommendations with detailed justifications. The RAG pattern ensures the AI reasons only from actual release content, preventing hallucinations while enabling explainable decision-making that humans can audit before approving GitOps PRs.

