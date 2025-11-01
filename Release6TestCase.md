# Release 6 Test Case Definition

## Purpose
This release serves as the **primary test case** for validating the AI-Orchestrated Release Management System. The AI must correctly classify tenant upgrade recommendations as:
- **MUST upgrade** - Critical bug affects tenant's active features
- **SHOULD upgrade** - Major bug or valuable new feature
- **CAN SKIP** - No relevant changes for tenant

---

## Release Overview

### Release Information
- **Version:** v1.6.0
- **Release Date:** 2025-12-15T10:00:00Z
- **Deployment Complexity:** MEDIUM
- **Required Predecessor Versions:** ["v1.5.0"]
- **Release Summary:** "Introduces F10 tenant administration controls and addresses critical bugs in F3, F4, and F6."

### Change Composition
- **8 Stories** (F10 features)
- **16 Subtasks** (supporting F10 implementation)
- **5 CRITICAL Bugs** (explicit test cases)
- **7 MAJOR Bugs**
- **5 MINOR Bugs**
- **2 TECH_DEBT Tickets**

**Total: 43 tickets**

---

## Critical Bugs (Test Case Definition)

### Bug 1: F3.4 Proration Logic Failure
```json
{
  "issue_key": "JIRA-6001",
  "issue_type": "Bug",
  "summary": "CRITICAL: Multi-year contract proration fails on mid-cycle plan change",
  "status": "Resolved",
  "linked_feature_id": "F3_SubscriptionMgmt",
  "priority": "Highest",
  "description": "Tenants with multi-year subscription contracts experience fatal errors when attempting to change plans mid-cycle. The proration engine fails to calculate prorated amounts correctly for contract periods exceeding 365 days. This was first reported by T-CORP-401 when attempting to upgrade their annual contract to a 2-year commitment. The error results in a 500 response and leaves the subscription in an inconsistent state requiring manual intervention.",
  "resolution_details": "Root Cause: The proration calculation used a hardcoded 365-day assumption for all contract periods. Multi-year contracts triggered integer overflow when calculating daily rates. Fix: Refactored proration engine to use BigDecimal for all monetary calculations and dynamically calculate daily rates based on actual contract length. Added comprehensive test coverage for 1-month, 1-year, 2-year, and 3-year contract scenarios. Deployment Impact: NONE (code-only change).",
  "impacted_modules": ["subscription-service", "proration-engine", "azure-sql-schema-f3"],
  "customer_context": {
    "customer_id": "T-CORP-401",
    "is_key_account": true
  },
  "fix_version": "v1.6.0",
  "release_notes_excerpt": "Fixes a critical issue where multi-year subscription contracts could not be modified mid-cycle due to proration calculation errors."
}
```

**Affected Tenants:** T-CORP-401, T-MEGA-500, T-GAMMA-150
- All 3 use F3_SubscriptionMgmt
- All 3 have multi-year contracts
- **Expected Recommendation:** MUST upgrade

---

### Bug 2: F4.5 Stripe Retry Logic Failure
```json
{
  "issue_key": "JIRA-6002",
  "issue_type": "Bug",
  "summary": "CRITICAL: Stripe payment retry exhaustion causes incorrect charge reversal",
  "status": "Resolved",
  "linked_feature_id": "F4_BillingPayments",
  "priority": "Highest",
  "description": "When Stripe payment retries are exhausted after multiple failed attempts, the system incorrectly triggers a charge reversal instead of marking the payment as failed. This results in legitimate failed payments being treated as disputed charges, triggering refunds for subscriptions that should be suspended. First discovered when T-ALPHA-050 reported unexpected refunds for 12 subscriptions that should have been marked delinquent. Investigation revealed that retry exhaustion edge case was not properly handled in the payment state machine.",
  "resolution_details": "Root Cause: Retry exhaustion event was misinterpreted as a chargeback/dispute event due to shared error code handling. Fix: Separated retry exhaustion logic into distinct state transition. Added explicit check for retry count before triggering any refund workflows. Implemented idempotency keys for all Stripe refund operations to prevent duplicate refunds. Testing: Validated with 50+ retry exhaustion scenarios across development and staging environments. Deployment Impact: NONE (code-only change).",
  "impacted_modules": ["billing-service", "payment-api", "stripe-integration", "refund-processor"],
  "customer_context": {
    "customer_id": "T-ALPHA-050",
    "is_key_account": false
  },
  "fix_version": "v1.6.0",
  "release_notes_excerpt": "Resolves an issue where payment retry exhaustion could trigger unintended refunds instead of properly marking subscriptions as delinquent."
}
```

**Affected Tenants:** T-ALPHA-050, T-BETA-100, T-DELTA-200, T-OMEGA-300, T-SIGMA-400
- All 5 use F4_BillingPayments with Stripe
- All 5 have active retry logic enabled
- **Expected Recommendation:** MUST upgrade

---

### Bug 3: F6.1 VAT Calculation Error
```json
{
  "issue_key": "JIRA-6003",
  "issue_type": "Bug",
  "summary": "CRITICAL: VAT calculation incorrect for EU cross-border transactions",
  "status": "Resolved",
  "linked_feature_id": "F6_InvoicingTax",
  "priority": "Highest",
  "description": "For EU cross-border B2B transactions, the system incorrectly applies domestic VAT rates instead of implementing VAT reverse charge mechanism. This results in customers being overcharged and creates tax compliance issues. T-EURO-500 (German customer purchasing from UK entity) reported being charged 20% UK VAT instead of 0% under reverse charge rules. This affects all EU tenants conducting cross-border transactions and exposes them to tax audit risks.",
  "resolution_details": "Root Cause: Tax calculation service did not properly validate VAT registration numbers or implement reverse charge logic for qualifying B2B transactions. Fix: Integrated VIES VAT number validation API. Implemented reverse charge detection based on supplier/customer country combinations and valid VAT registration status. Added comprehensive tax scenario testing for all EU member states. Deployment Impact: HELM_CHANGE (new external API configuration for VIES integration).",
  "impacted_modules": ["tax-calculator", "invoice-generator", "tax-db-schema"],
  "customer_context": {
    "customer_id": "T-EURO-500",
    "is_key_account": false
  },
  "fix_version": "v1.6.0",
  "release_notes_excerpt": "Corrects VAT calculation for EU cross-border transactions to properly apply reverse charge mechanism, ensuring tax compliance."
}
```

**Affected Tenants:** T-EURO-500, T-EURO-600
- Both use F6_InvoicingTax
- Both operate in EU with cross-border customers
- **Expected Recommendation:** MUST upgrade

---

### Bug 4: F4.4 Payment Method Deletion Cascade Failure
```json
{
  "issue_key": "JIRA-6004",
  "issue_type": "Bug",
  "summary": "CRITICAL: Payment method deletion cascade fails, orphans active subscriptions",
  "status": "Resolved",
  "linked_feature_id": "F4_BillingPayments",
  "priority": "Highest",
  "description": "When a primary payment method is deleted while active subscriptions are still referencing it, the cascade deletion logic fails to properly reassign or suspend affected subscriptions. This leaves subscriptions in an orphaned state where renewal attempts fail silently. T-ULTRA-600 discovered this when 45 subscriptions failed to renew after their finance team rotated expired credit cards. No alerts were generated and subscriptions remained in 'active' status despite failed payment attempts.",
  "resolution_details": "Root Cause: Database foreign key constraint was set to ON DELETE SET NULL instead of using application-level cascade logic with proper state transitions. Fix: Implemented application-level payment method deletion workflow that: (1) Identifies all active subscriptions using the payment method, (2) Requires explicit reassignment or suspension decision, (3) Generates admin alerts for affected subscriptions, (4) Blocks deletion if active subscriptions exist without alternative payment method. Deployment Impact: DB_MIGRATION (foreign key constraint modification).",
  "impacted_modules": ["billing-service", "payment-api", "subscription-service", "alert-manager"],
  "customer_context": {
    "customer_id": "T-ULTRA-600",
    "is_key_account": true
  },
  "fix_version": "v1.6.0",
  "release_notes_excerpt": "Prevents orphaned subscriptions by enforcing proper payment method reassignment or suspension before allowing payment method deletion."
}
```

**Affected Tenants:** T-ULTRA-600 (primary), potentially any tenant using F4.4
- **Expected Recommendation:** MUST upgrade for T-ULTRA-600 (directly affected key account)
- **Expected Recommendation:** SHOULD upgrade for all other tenants using F4.4

---

### Bug 5: F3.2 Subscription Cancellation Refund Logic
```json
{
  "issue_key": "JIRA-6005",
  "issue_type": "Bug",
  "summary": "CRITICAL: Subscription cancellation doesn't trigger prorated refund for prepaid terms",
  "status": "Resolved",
  "linked_feature_id": "F3_SubscriptionMgmt",
  "priority": "Highest",
  "description": "When customers cancel subscriptions mid-cycle with 'cancel immediately' option, the system marks the subscription as canceled but fails to calculate and issue prorated refunds for the unused portion of prepaid billing periods. T-PRIME-700 reported that 8 customers who canceled quarterly subscriptions mid-term did not receive expected refunds, leading to customer satisfaction issues and chargeback disputes.",
  "resolution_details": "Root Cause: Cancellation workflow had two code paths (cancel at period end vs cancel immediately) but only the 'cancel at period end' path was integrated with the refund calculation service. Fix: Unified cancellation workflow to always calculate refund eligibility based on cancellation policy and prepaid balance. Added configurable cancellation policy support (full refund, prorated refund, no refund). Implemented automatic refund processing with customer notification. Deployment Impact: NONE (code-only change).",
  "impacted_modules": ["subscription-service", "proration-engine", "refund-processor", "notification-service"],
  "customer_context": {
    "customer_id": "T-PRIME-700",
    "is_key_account": true
  },
  "fix_version": "v1.6.0",
  "release_notes_excerpt": "Ensures immediate subscription cancellations properly calculate and issue prorated refunds for unused prepaid periods."
}
```

**Affected Tenants:** T-PRIME-700, T-BETA-100
- Both use F3_SubscriptionMgmt
- Both have customers on quarterly/annual prepaid plans
- **Expected Recommendation:** MUST upgrade

---

## Major Bugs (7 total)

### Bug 6: F1.3 OAuth Token Refresh Silent Failure
```json
{
  "issue_key": "JIRA-6006",
  "issue_type": "Bug",
  "summary": "MAJOR: OAuth token refresh fails silently causing unexpected logouts",
  "status": "Resolved",
  "linked_feature_id": "F1_Authentication",
  "priority": "High",
  "severity": "MAJOR",
  "customer_context": {
    "customer_id": null,
    "is_key_account": false
  },
  "fix_version": "v1.6.0"
}
```

### Bug 7: F8.1 MRR Calculation Double-Counting
```json
{
  "issue_key": "JIRA-6007",
  "issue_type": "Bug",
  "summary": "MAJOR: MRR calculation double-counts prorated subscription upgrades",
  "status": "Resolved",
  "linked_feature_id": "F8_ReportingAnalytics",
  "priority": "High",
  "severity": "MAJOR",
  "customer_context": {
    "customer_id": null,
    "is_key_account": false
  },
  "fix_version": "v1.6.0"
}
```

### Bug 8: F5.3 Quota Enforcement Cache Invalidation
```json
{
  "issue_key": "JIRA-6008",
  "issue_type": "Bug",
  "summary": "MAJOR: Quota limits don't reset properly on subscription renewal",
  "status": "Resolved",
  "linked_feature_id": "F5_UsageTracking",
  "priority": "High",
  "severity": "MAJOR",
  "customer_context": {
    "customer_id": "T-ZETA-900",
    "is_key_account": false
  },
  "fix_version": "v1.6.0"
}
```

### Bug 9: F7.2 Webhook Retry Flood
```json
{
  "issue_key": "JIRA-6009",
  "issue_type": "Bug",
  "summary": "MAJOR: Webhook retry logic floods customer endpoints during outages",
  "status": "Resolved",
  "linked_feature_id": "F7_NotificationsAlerts",
  "priority": "High",
  "severity": "MAJOR",
  "customer_context": {
    "customer_id": "T-KAPPA-200",
    "is_key_account": false
  },
  "fix_version": "v1.6.0"
}
```

### Bug 10: F6.3 Credit Note Tax Adjustment
```json
{
  "issue_key": "JIRA-6010",
  "issue_type": "Bug",
  "summary": "MAJOR: Credit notes don't correctly adjust tax amounts on partial refunds",
  "status": "Resolved",
  "linked_feature_id": "F6_InvoicingTax",
  "priority": "Medium",
  "severity": "MAJOR",
  "customer_context": {
    "customer_id": null,
    "is_key_account": false
  },
  "fix_version": "v1.6.0"
}
```

### Bug 11: F9.3 CRM Integration Auth Expiry
```json
{
  "issue_key": "JIRA-6011",
  "issue_type": "Bug",
  "summary": "MAJOR: CRM integration stops syncing after OAuth token expires",
  "status": "Resolved",
  "linked_feature_id": "F9_APIIntegrations",
  "priority": "High",
  "severity": "MAJOR",
  "customer_context": {
    "customer_id": "T-LAMBDA-350",
    "is_key_account": false
  },
  "fix_version": "v1.6.0"
}
```

### Bug 12: F4.3 Invoice PDF Generation Memory Leak
```json
{
  "issue_key": "JIRA-6012",
  "issue_type": "Bug",
  "summary": "MAJOR: Invoice PDF generation causes memory leak for invoices with 100+ line items",
  "status": "Resolved",
  "linked_feature_id": "F4_BillingPayments",
  "priority": "High",
  "severity": "MAJOR",
  "customer_context": {
    "customer_id": null,
    "is_key_account": false
  },
  "fix_version": "v1.6.0"
}
```

---

## Minor Bugs (5 total)

### Bug 13-17: Lower Priority Issues
```
JIRA-6013: MINOR - F2.4 Profile page mobile layout broken on iOS Safari
JIRA-6014: MINOR - F7.1 Email template formatting issues with long plan names
JIRA-6015: MINOR - F5.1 API usage chart shows incorrect timezone
JIRA-6016: PATCH - F1.5 RBAC permission label typo in admin UI
JIRA-6017: PATCH - F9.5 API documentation portal broken anchor links
```

---

## New Features (F10 - Tenant Administration)

### Stories (8 total)

```
JIRA-6020: Implement tenant provisioning automation (F10.1)
├── JIRA-6020-1: Design tenant configuration schema
└── JIRA-6020-2: Build tenant provisioning API

JIRA-6021: Build feature flag management system (F10.2)
├── JIRA-6021-1: Create feature flag database schema
└── JIRA-6021-2: Implement real-time flag toggle API

JIRA-6022: Develop tenant branding customization (F10.3)
├── JIRA-6022-1: Build brand asset upload service
└── JIRA-6022-2: Create theme configuration UI

JIRA-6023: Implement data isolation controls (F10.4)
├── JIRA-6023-1: Add data residency configuration
└── JIRA-6023-2: Build compliance audit logging

JIRA-6024: Build AI-Orchestrated Release Management (F10.5)
├── JIRA-6024-1: Integrate Azure OpenAI for recommendation engine
├── JIRA-6024-2: Build RAG system with Qdrant vector database
├── JIRA-6024-3: Create GitOps PR automation
└── JIRA-6024-4: Develop operator approval dashboard

JIRA-6025: Implement tenant health monitoring (F10.1 enhancement)
├── JIRA-6025-1: Build real-time metrics collector
└── JIRA-6025-2: Create alerting dashboard

JIRA-6026: Add multi-region tenant deployment support (F10.1)
├── JIRA-6026-1: Design region-aware routing
└── JIRA-6026-2: Implement tenant migration tools

JIRA-6027: Build tenant lifecycle management (F10.1)
├── JIRA-6027-1: Create tenant suspension/reactivation workflows
└── JIRA-6027-2: Implement tenant deletion with data archival
```

---

## Tech Debt (2 tickets)

```
JIRA-6030: TECH_DEBT - Upgrade to .NET 8 LTS for all microservices
- Priority: Medium
- Deployment Impact: HELM_CHANGE

JIRA-6031: TECH_DEBT - Refactor payment gateway abstraction layer
- Priority: Low
- Deployment Impact: NONE
```

---

## Expected AI Recommendation Outcomes

### MUST Upgrade (7-8 tenants)
**Tenants directly affected by CRITICAL bugs:**

| Tenant ID | Reason | Affected Bug |
|-----------|--------|--------------|
| T-CORP-401 | Uses F3, directly affected by Bug 1 | JIRA-6001 |
| T-MEGA-500 | Uses F3, multi-year contracts | JIRA-6001 |
| T-GAMMA-150 | Uses F3, multi-year contracts | JIRA-6001 |
| T-ALPHA-050 | Uses F4/Stripe, directly affected by Bug 2 | JIRA-6002 |
| T-BETA-100 | Uses F3+F4, affected by Bug 2 & Bug 5 | JIRA-6002, JIRA-6005 |
| T-ULTRA-600 | Key account, uses F4, affected by Bug 4 | JIRA-6004 |
| T-EURO-500 | Uses F6, EU VAT issue | JIRA-6003 |
| T-PRIME-700 | Key account, uses F3, affected by Bug 5 | JIRA-6005 |

**AI Reasoning Example:**
```
Tenant T-CORP-401 MUST upgrade to v1.6.0

Critical Issue: JIRA-6001 directly affects this tenant's active feature F3_SubscriptionMgmt.
The proration calculation bug prevents this key account from modifying their multi-year 
contracts, blocking business operations. This tenant was explicitly mentioned in the bug 
report as the initial reporter.

Risk Assessment: While deployment_complexity is MEDIUM, the operational impact of not 
upgrading far exceeds deployment risk. This tenant has risk_tolerance=LOW but the bug 
creates higher operational risk than the upgrade itself.

Recommendation: Schedule upgrade within 48 hours with standard rollback procedures.
```

---

### SHOULD Upgrade (15-20 tenants)
**Tenants using affected features but not directly hit by bugs:**

**Category 1: Uses F3/F4/F6 but not directly affected**
- 10-12 tenants using subscription/billing features
- No immediate operational impact but preventive upgrade recommended
- Example: T-DELTA-200, T-SIGMA-400, T-OMEGA-300

**Category 2: Would benefit from F10 features**
- 5-8 tenants that could use feature flags or branding
- New functionality aligns with their usage patterns
- Example: Large tenants with high daily_usage_score

**AI Reasoning Example:**
```
Tenant T-DELTA-200 SHOULD upgrade to v1.6.0

Relevance: This tenant uses F4_BillingPayments with Stripe integration. While not 
directly affected by the payment retry bug (JIRA-6002), they use the same code paths 
and could encounter this issue in the future.

Value Assessment: Upgrade provides preventive fix for a critical payment processing 
issue. Additionally, this tenant would benefit from F10.2 feature flag management 
given their high daily_usage_score (72).

Recommendation: Schedule upgrade during next maintenance window (within 2 weeks).
```

---

### CAN SKIP (25-30 tenants)
**Tenants not using affected features:**

**Category 1: Different feature set**
- Uses F1/F2/F5/F7/F8/F9 only
- No exposure to F3/F4/F6 bugs
- Example: T-ZETA-900 (uses only F1, F2, F5)

**Category 2: Light users with high risk tolerance**
- Low daily_usage_score (<30)
- risk_tolerance = HIGH
- Can defer upgrade until next major release
- Example: T-IOTA-450, T-THETA-800

**AI Reasoning Example:**
```
Tenant T-ZETA-900 CAN SKIP v1.6.0

Feature Analysis: This tenant's active features [F1, F2, F5] are not affected by any 
critical or major bugs in this release. The F10 tenant administration features are not 
relevant to their current usage patterns.

Risk Assessment: Zero operational risk from skipping this release. Their 
risk_tolerance=HIGH and daily_usage_score=25 indicate they prefer stability over 
rapid feature adoption.

Recommendation: This tenant can safely remain on v1.5.0 until v1.7.0 or until they 
express interest in F10 features.
```

---

## Validation Criteria

### AI System Must Demonstrate:

1. **Feature Matching Accuracy**
   - Correctly identify which bugs affect which tenant's active_features
   - 100% accuracy on CRITICAL bug to tenant mapping

2. **Risk Assessment Logic**
   - Weight bug severity appropriately
   - Consider tenant risk_tolerance in final recommendation
   - Flag key_account tenants for special attention

3. **Reasoning Transparency**
   - Provide specific JIRA ticket references
   - Explain why tenant is/isn't affected
   - Quantify operational vs deployment risk

4. **Edge Case Handling**
   - Key account with LOW risk_tolerance but CRITICAL bug → MUST upgrade
   - Tenant using deprecated feature → highlight in reasoning
   - Tenant with high failed_jobs_percent → flag deployment caution

---

## Test Case Success Metrics

### Quantitative Targets
- **Precision:** 90%+ (correct MUST recommendations)
- **Recall:** 95%+ (no missed critical upgrades)
- **Reasoning Quality:** Human operator approval rate >85%

### Qualitative Targets
- AI explanations must reference specific JIRA tickets
- Risk assessment must be actionable (not generic)
- Recommendations must be defensible in audit scenarios

---

## Summary

Release 6 provides a **controlled test environment** with:
- ✅ 5 well-defined CRITICAL bugs with specific tenant impact
- ✅ Clear feature-to-bug-to-tenant traceability
- ✅ Expected recommendation outcomes for each tenant archetype
- ✅ Realistic edge cases (key accounts, cross-feature bugs, deployment complexity)

This test case will validate whether the AI can:
1. Retrieve relevant context from Vector DB
2. Match bugs to tenant feature usage
3. Generate accurate, explainable recommendations
4. Handle complex risk assessment scenarios
