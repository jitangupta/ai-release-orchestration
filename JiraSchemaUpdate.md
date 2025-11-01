# Jira Schema Update - Subtask Support

## Overview
This document defines the schema additions needed to support Story → Subtask relationships in the Jira ticket data model.

---

## New Fields

### For Story Tickets

#### `subtasks` (ARRAY<STRING>)
- **Data Type:** ARRAY<STRING>
- **Description:** List of subtask issue keys that belong to this story
- **Purpose:** Enables traversal from parent story to its implementation tasks
- **Example:** `["JIRA-2001-1", "JIRA-2001-2"]`
- **Constraints:** 
  - Empty array if story has no subtasks
  - All referenced subtask IDs must exist in the Jira dataset
  - Maximum 10 subtasks per story (realistic constraint)

---

### For Subtask Tickets

#### `parent_issue_key` (STRING)
- **Data Type:** STRING
- **Description:** The issue key of the parent story this subtask belongs to
- **Purpose:** Enables reverse lookup from subtask to parent story
- **Example:** `"JIRA-2001"`
- **Constraints:**
  - Must be null for non-subtask issue types (Bug, Story, Tech Debt, Spike)
  - Must reference a valid Story issue_key if issue_type = "Subtask"

---

## Updated Schema Definition

### Extended Jira Ticket Schema

| Field Name             | Data Type     | Description                                                                              | Applies To                  |
|------------------------|---------------|------------------------------------------------------------------------------------------|----------------------------|
| issue_key              | STRING        | The unique Jira ID (e.g., JIRA-9013).                                                    | All ticket types            |
| issue_type             | ENUM          | The kind of work (Bug, Story, Spike, Technical Debt, **Subtask**).                      | All ticket types            |
| **parent_issue_key**   | STRING/NULL   | The parent story's issue_key (only for Subtasks).                                        | **Subtasks only**           |
| **subtasks**           | ARRAY<STRING> | List of subtask issue_keys belonging to this story.                                      | **Stories only**            |
| summary                | STRING        | The short title of the ticket.                                                           | All ticket types            |
| status                 | ENUM          | Current status (e.g., Resolved, Done).                                                   | All ticket types            |
| linked_feature_id      | STRING        | The ID of the feature affected (e.g., F3_SubscriptionMgmt).                              | All ticket types            |
| priority               | ENUM          | Internal engineering criticality (Highest, High, Medium, Low).                           | All ticket types            |
| description            | TEXT (LONG)   | The original full text detailing the problem or requested feature.                       | All ticket types            |
| resolution_details     | TEXT (LONG)   | Details on how the issue was fixed, including root cause analysis.                       | All ticket types            |
| impacted_modules       | ARRAY<STRING> | Technical components affected (e.g., api-gateway, db-migration-f3, helm-chart).          | All ticket types            |
| customer_context       | OBJECT        | Details about the tenant who reported/requested the issue.                               | All ticket types            |
| fix_version            | STRING        | The application version where the fix/feature was released (e.g., v1.5.0).               | All ticket types            |
| release_notes_excerpt  | TEXT (MEDIUM) | A clean, human-readable summary intended for customer release notes.                     | All ticket types            |

---

## Validation Rules

### Story Tickets
```python
def validate_story(ticket):
    assert ticket['issue_type'] == 'Story'
    assert ticket['parent_issue_key'] is None  # Stories have no parent
    assert isinstance(ticket['subtasks'], list)  # Must have subtasks array (can be empty)
    assert len(ticket['subtasks']) <= 10  # Maximum 10 subtasks per story
    
    # All subtasks must exist in dataset
    for subtask_id in ticket['subtasks']:
        assert subtask_exists(subtask_id)
```

### Subtask Tickets
```python
def validate_subtask(ticket):
    assert ticket['issue_type'] == 'Subtask'
    assert ticket['parent_issue_key'] is not None  # Subtasks must have parent
    assert ticket['subtasks'] == []  # Subtasks cannot have their own subtasks
    
    # Parent must exist and be a Story
    parent = get_ticket(ticket['parent_issue_key'])
    assert parent['issue_type'] == 'Story'
    assert ticket['issue_key'] in parent['subtasks']  # Bidirectional consistency
```

### Bug/Tech Debt/Spike Tickets
```python
def validate_non_story(ticket):
    assert ticket['issue_type'] in ['Bug', 'Tech Debt', 'Spike']
    assert ticket['parent_issue_key'] is None  # These types have no parent
    assert ticket['subtasks'] == []  # These types have no subtasks
```

---

## Example Data Structures

### Story with Subtasks
```json
{
  "issue_key": "JIRA-2001",
  "issue_type": "Story",
  "parent_issue_key": null,
  "subtasks": ["JIRA-2001-1", "JIRA-2001-2"],
  "summary": "Implement SSO Integration (F1.2)",
  "status": "Done",
  "linked_feature_id": "F1_Authentication",
  "priority": "High",
  "description": "Add SAML 2.0 and OAuth SSO support to enable enterprise customers to integrate their identity providers with our authentication system.",
  "resolution_details": "Completed SAML 2.0 provider implementation with metadata exchange support. Added OAuth 2.0 client configuration UI. Integrated with existing RBAC system.",
  "impacted_modules": ["auth-service", "sso-provider", "oauth-gateway"],
  "customer_context": {
    "customer_id": "T-CORP-401",
    "is_key_account": true
  },
  "fix_version": "v1.2.0",
  "release_notes_excerpt": "Enterprise customers can now integrate their corporate identity providers using SAML 2.0 or OAuth 2.0 for seamless single sign-on."
}
```

### Subtask Example 1
```json
{
  "issue_key": "JIRA-2001-1",
  "issue_type": "Subtask",
  "parent_issue_key": "JIRA-2001",
  "subtasks": [],
  "summary": "Add SAML 2.0 provider configuration",
  "status": "Done",
  "linked_feature_id": "F1_Authentication",
  "priority": "High",
  "description": "Implement SAML 2.0 metadata exchange, assertion validation, and attribute mapping for enterprise SSO providers.",
  "resolution_details": "Built SAML metadata parser, integrated with signing certificate validation, implemented attribute claim mapping to internal user profile schema.",
  "impacted_modules": ["auth-service", "sso-provider"],
  "customer_context": {
    "customer_id": null,
    "is_key_account": false
  },
  "fix_version": "v1.2.0",
  "release_notes_excerpt": null
}
```

### Subtask Example 2
```json
{
  "issue_key": "JIRA-2001-2",
  "issue_type": "Subtask",
  "parent_issue_key": "JIRA-2001",
  "subtasks": [],
  "summary": "Build SSO redirect flow UI",
  "status": "Done",
  "linked_feature_id": "F1_Authentication",
  "priority": "Medium",
  "description": "Create user interface for SSO provider selection, redirect handling, and error state management.",
  "resolution_details": "Implemented React-based SSO provider selection screen, added loading states for SAML/OAuth redirects, built error handling for failed authentication attempts.",
  "impacted_modules": ["auth-service", "frontend-app"],
  "customer_context": {
    "customer_id": null,
    "is_key_account": false
  },
  "fix_version": "v1.2.0",
  "release_notes_excerpt": null
}
```

### Bug Example (No Subtasks)
```json
{
  "issue_key": "JIRA-3015",
  "issue_type": "Bug",
  "parent_issue_key": null,
  "subtasks": [],
  "summary": "CRITICAL: Proration logic throws SQL error on plan downgrade (F3).",
  "status": "Resolved",
  "linked_feature_id": "F3_SubscriptionMgmt",
  "priority": "Highest",
  "description": "The Subscription Management API throws a fatal SQL constraint violation when processing a plan downgrade if the customer has more than 5 historical invoices.",
  "resolution_details": "The proration query was optimized to use a subquery that handles historical invoice joins without locking the full table.",
  "impacted_modules": ["subscription-service", "proration-engine", "azure-sql-schema-f3"],
  "customer_context": {
    "customer_id": "T-CORP-401",
    "is_key_account": true
  },
  "fix_version": "v1.5.1",
  "release_notes_excerpt": "Addresses a critical issue where plan downgrades could fail due to a database constraint error during proration calculation."
}
```

---

## Naming Conventions

### Subtask Issue Key Pattern
```
Parent Story: JIRA-{RELEASE_NUM}{SEQUENTIAL_NUM}
Subtask:      JIRA-{RELEASE_NUM}{SEQUENTIAL_NUM}-{SUBTASK_NUM}

Examples:
Story:    JIRA-2001
Subtask1: JIRA-2001-1
Subtask2: JIRA-2001-2

Story:    JIRA-3042
Subtask1: JIRA-3042-1
Subtask2: JIRA-3042-2
Subtask3: JIRA-3042-3
```

---

## Impact on RAG System

### Vector Database Storage
**Store both Stories and Subtasks as separate documents:**

**Why:** LLM needs granular technical detail for impact analysis

**Story Document (High-level):**
```
Title: Implement SSO Integration (F1.2)
Content: Add SAML 2.0 and OAuth SSO support to enable enterprise customers...
Feature: F1_Authentication
Metadata: {type: Story, parent: null, subtasks: [JIRA-2001-1, JIRA-2001-2]}
```

**Subtask Document (Technical detail):**
```
Title: Add SAML 2.0 provider configuration
Content: Implement SAML 2.0 metadata exchange, assertion validation...
Feature: F1_Authentication
Metadata: {type: Subtask, parent: JIRA-2001}
```

### RAG Retrieval Strategy
1. **Semantic search** retrieves relevant stories/subtasks
2. If subtask matches, **also retrieve parent story** for full context
3. If story matches, **optionally retrieve subtasks** for technical depth

**Example Query:** "Does this release affect SSO authentication?"
- Retrieves: JIRA-2001 (Story) + JIRA-2001-1, JIRA-2001-2 (Subtasks)
- LLM sees both high-level feature and technical implementation details

---

## Migration Notes

### For Existing Data
If you have existing Jira data without subtasks:
```python
def migrate_existing_tickets():
    for ticket in all_tickets:
        if ticket['issue_type'] in ['Bug', 'Tech Debt', 'Spike']:
            ticket['parent_issue_key'] = None
            ticket['subtasks'] = []
        elif ticket['issue_type'] == 'Story':
            ticket['parent_issue_key'] = None
            ticket['subtasks'] = []  # No subtasks defined yet
```

---

## Summary

**New Fields:**
- `parent_issue_key` (STRING/NULL) - For subtasks only
- `subtasks` (ARRAY<STRING>) - For stories only

**Validation:**
- Bidirectional consistency (parent.subtasks includes child, child.parent_issue_key references parent)
- Stories cannot be subtasks
- Subtasks cannot have their own subtasks
- Bugs/Tech Debt/Spikes have no parent/subtask relationships

**RAG Impact:**
- Store all ticket types as separate vector DB documents
- Retrieve parent story when subtask matches
- Enables granular technical context for AI recommendations
