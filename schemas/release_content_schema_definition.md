# Release Content Schema Definition
| Field Name                | Data Type         | Description                                                                                                      | Rationale for AI Recommendation Engine                                                                                                         |
|---------------------------|-------------------|------------------------------------------------------------------------------------------------------------------|------------------------------------------------------------------------------------------------------------------------------------------------|
| release_version           | STRING            | The semantic version number of the release (e.g., v1.5.0).                                                       | Primary identifier. Used to compare against tenant.current_version.                                                                            |
| release_date              | STRING (ISO 8601) | Date the release was stabilized in canary deployment.                                                            | For timeline tracking and reporting.                                                                                                           |
| deployment_complexity     | ENUM              | An estimated risk score for the deployment process itself (LOW, MEDIUM, HIGH).                                   | Affects the overall upgrade recommendation risk, independent of code changes. HIGH complexity suggests caution for LOW risk tolerance tenants. |
| release_summary           | STRING            | A brief, human-readable overview of the release's main theme (e.g., "Performance tuning and F3 security patch"). | Helps the AI generate high-level reasoning.                                                                                                    |
 | required_predecessor_versions| ARRAY of STRING|A list of version tags the tenant must be on before upgrading to this version. An empty list implies direct upgrade is possible from any previous version. |CRITICAL LOGIC: If a tenant's `current_version` is not in this list, the AI recommends a sequential upgrade path.|
| content_breakdown         | ARRAY<OBJECT>     | The detailed list of all changes included in this release.                                                       | This is the core data structure that links tickets/changes to features.                                                                        |
| $\quad$ change_id         | STRING            | The Jira/internal ticket ID (e.g., JIRA-4567).                                                                   | Used to retrieve the full ticket text/description from the Vector DB.                                                                          |
| $\quad$ change_type       | ENUM              | The nature of the change (FEATURE, BUG_FIX, TECH_DEBT, SPIKE).                                                   | The AI weighs BUG_FIX (especially critical ones) much higher than TECH_DEBT.                                                                   |
| $\quad$ linked_feature_id | STRING            | The ID of the feature affected (e.g., F3_SubscriptionMgmt).                                                      | CRITICAL LINK: Used to match the release content to tenant.active_features.                                                                    |
| $\quad$ severity          | ENUM              | The impact/priority of the change (CRITICAL, MAJOR, MINOR, PATCH).                                               | A CRITICAL BUG_FIX on an F3 feature results in a MUST upgrade.                                                                                 |
| $\quad$ deployment_impact | ENUM              | Does this change require special deployment steps (HELM_CHANGE, DB_MIGRATION, NONE).                             | If DB_MIGRATION is present, the risk profile goes up for all tenants.                                                                          |

## Hotfix 
| Release Type | Example             | release_version | Impact                                                                                     |
|--------------|---------------------|-----------------|--------------------------------------------------------------------------------------------|
| Major/Minor  | Planned feature set | v1.5.0          | High deployment_complexity, many features/fixes.                                           |
| Hotfix       | Critical bug patch  | v1.5.1          | Low deployment_complexity (ideally), only one or two BUG_FIX entries in content_breakdown. |

## Hotfix Example data
| Hotfix Example Data          | Why It Works                                       |
|------------------------------|----------------------------------------------------|
| release_version": "v1.5.1    | Clearly identifies it as a direct patch to v1.5.0. |
| deployment_complexity": "LOW | Tells the AI the deployment risk is minimal.       |

## Difference Between Release Content Entry and Full Jira Ticket
| Component             | Location                                  | Role in the AI System                                                             |
|-----------------------|-------------------------------------------|-----------------------------------------------------------------------------------|
| Release Content Entry | Inside the Release Content Schema JSON    | Metadata for Structured Query & Filtering (The "What happened?")                  |
| Full Jira Ticket      | Stored in the Vector Database (Vector DB) | Unstructured Context for Generation (The "Why it happened and how it was fixed.") |

### 1. Release Content Entry (The "Pointer" and "Filter")
The entry inside the content_breakdown is essentially a metadata pointer and a structured filter.
| Field                  | Purpose in Release Content                                                                                                    |
|------------------------|-------------------------------------------------------------------------------------------------------------------------------|
| change_id (JIRA-9013)  | The pointer to the full ticket data in the Vector DB.                                                                         |
| change_type (BUG_FIX)  | Allows the AI to quickly filter and weigh changes (e.g., ignore TECH\_DEBT for low-risk tenants).                             |
| linked_feature_id (F3) | Allows the AI to quickly match against tenant.active_features without reading any text. This is the critical structural link. |
| severity (CRITICAL)    | Provides the primary numeric weight for the recommendation algorithm.                                                         |

### 2. Full Jira Ticket (The "Context" for RAG)
The full Jira ticket is the unstructured text that you will embed and store in your Vector DB. This is what the LLM uses for the Generation part of RAG.
| Component          | Content                                                                                                                                                                  | Purpose in RAG                                                                                       |
|--------------------|--------------------------------------------------------------------------------------------------------------------------------------------------------------------------|------------------------------------------------------------------------------------------------------|
| Ticket Title       | CRITICAL: Proration logic fails on cross-currency plan upgrades.                                                                                                         | Provides context for the reason/severity.                                                            |
| Ticket Description | When a customer upgrades from Plan A (USD) to Plan B (EUR), the currency conversion in the proration calculation throws an exception, resulting in subscription failure. | Provides the detailed reasoning the AI will use to generate the final human-readable recommendation. |
| Developer Comments | Root cause identified as outdated currency exchange library. Updated library and added regression tests for cross-currency scenarios.                                    | Provides technical proof of resolution.                                                              |
