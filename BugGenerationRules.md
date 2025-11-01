# Bug Generation Rules

## Overview
This document defines the rules for generating realistic bug tickets across Releases 2-6. Bugs should follow patterns that reflect:
- Cumulative complexity (more features = more bugs)
- Feature maturity (new features have more bugs)
- Cross-feature interactions (bugs span multiple features)
- Real-world severity distribution

---

## Bug Severity Distribution

### Standard Distribution (Releases 2-5)
```
CRITICAL:  10-15% of bugs (high impact, affects key accounts or revenue)
MAJOR:     25-30% of bugs (significant impact, workarounds exist)
MINOR:     40-50% of bugs (low impact, cosmetic or edge cases)
PATCH:     10-15% of bugs (trivial, typos, minor UI issues)
```

### Release 6 (Test Case) Distribution
```
CRITICAL:  5 bugs (explicit test case definition)
MAJOR:     6-8 bugs
MINOR:     5-7 bugs
PATCH:     2-3 bugs
```

---

## Feature Impact Frequency
*Which features break most often?*

### Release 2 (15 bugs total)
**Feature Breakdown:**
- **F3 (Subscription):** 40% (6 bugs)
  - Reason: Most complex business logic, new downgrade/cancel features
- **F4 (Billing):** 30% (4-5 bugs)
  - Reason: External payment gateway integrations (Stripe added)
- **F1 (Auth):** 20% (3 bugs)
  - Reason: SSO addition creates edge cases
- **F2/F5/F7:** 10% (1-2 bugs)
  - Reason: Simpler features, less critical

**Example Bugs:**
- CRITICAL: F3.2 downgrade fails on proration calculation
- CRITICAL: F4.2 Stripe webhook signature validation fails
- MAJOR: F1.2 SSO redirect loop for multi-team users
- MAJOR: F4.5 Payment retry logic creates duplicate charges
- MINOR: F2.3 User invitation email formatting broken
- MINOR: F5.2 Analytics dashboard shows stale data

---

### Release 3 (25 bugs total)
**Feature Breakdown:**
- **F3 (Subscription):** 30% (7-8 bugs)
  - Reason: Trial + proration logic adds complexity
- **F4 (Billing):** 25% (6-7 bugs)
  - Reason: Refund processing interactions with payment gateways
- **F6 (Tax - NEW):** 20% (5 bugs)
  - Reason: New feature, complex tax rules
- **F1 (Auth):** 15% (3-4 bugs)
  - Reason: OAuth addition creates more edge cases
- **F2/F5/F7:** 10% (2-3 bugs)

**Example Bugs:**
- CRITICAL: F3.4 Proration fails for multi-currency upgrades
- CRITICAL: F6.1 Tax calculation incorrect for VAT reverse charge
- CRITICAL: F4.6 Refund processing creates negative balance
- MAJOR: F3.3 Trial period doesn't honor grace period
- MAJOR: F1.3 OAuth token refresh fails silently
- MINOR: F7.1 Renewal reminder sent 7 days early instead of 3

---

### Release 4 (28 bugs total)
**Feature Breakdown:**
- **F3/F4 Cross-Feature:** 25% (7 bugs)
  - Reason: Mature features start interacting in unexpected ways
- **F6 (Tax):** 20% (5-6 bugs)
  - Reason: Credit note generation edge cases
- **F8 (Reporting - NEW):** 20% (5-6 bugs)
  - Reason: New feature, data aggregation complexity
- **F1 (Auth):** 15% (4 bugs)
  - Reason: MFA addition creates UX edge cases
- **F5/F7:** 10% (2-3 bugs)
- **F2:** 10% (2-3 bugs)

**Example Bugs:**
- CRITICAL: F3+F4 Subscription cancellation doesn't trigger refund
- CRITICAL: F8.1 MRR calculation double-counts prorated subscriptions
- MAJOR: F1.4 MFA enrollment breaks for SSO-only users
- MAJOR: F6.3 Credit note doesn't adjust tax correctly
- MAJOR: F7.2 Webhook retry floods customer systems
- MINOR: F8.2 Dashboard export truncates data at 10,000 rows

---

### Release 5 (30 bugs total)
**Feature Breakdown:**
- **F3/F4/F6 (Core Billing Stack):** 35% (10-11 bugs)
  - Reason: Complex interactions in mature features
- **F8/F9 (Reporting + API - NEW):** 35% (10-11 bugs)
  - Reason: API integration exposes edge cases, reporting aggregation bugs
- **F1/F2/F5/F7:** 30% (8-9 bugs)
  - Reason: Maintenance bugs in established features

**Example Bugs:**
- CRITICAL: F9.1 REST API rate limiting blocks legitimate traffic
- CRITICAL: F4+F6 Invoice generation fails for multi-currency + tax
- CRITICAL: F8.3 LTV calculation includes churned customers
- MAJOR: F9.3 CRM integration webhook signature mismatch
- MAJOR: F3+F5 Quota enforcement doesn't reset on subscription renewal
- MINOR: F9.5 API documentation portal broken links

---

### Release 6 (15-18 bugs total - TEST CASE)
**Defined explicitly in Release6TestCase.md**

---

## Bug Priority Distribution

### Priority vs Severity Mapping
```
CRITICAL bugs → Priority: Highest (100%)
MAJOR bugs    → Priority: High (70%), Medium (30%)
MINOR bugs    → Priority: Medium (60%), Low (40%)
PATCH bugs    → Priority: Low (100%)
```

---

## Bug-to-Tenant Linkage Rules

### CRITICAL Bugs
**Linkage Requirements:**
- **60%** MUST reference a specific tenant in `customer_context.customer_id`
- **40%** of those should be key accounts (`is_key_account: true`)
- **100%** must list specific `impacted_modules` (1-3 modules)

**Example:**
```json
{
  "issue_key": "JIRA-3015",
  "severity": "CRITICAL",
  "customer_context": {
    "customer_id": "T-CORP-401",
    "is_key_account": true
  },
  "impacted_modules": ["subscription-service", "proration-engine", "azure-sql-schema-f3"]
}
```

### MAJOR Bugs
**Linkage Requirements:**
- **30%** should reference specific tenant
- **70%** discovered internally (customer_id: null)
- **100%** must list `impacted_modules`

### MINOR/PATCH Bugs
**Linkage Requirements:**
- **90%** discovered internally (customer_id: null)
- **10%** reported by customers (low priority for them)
- `impacted_modules` optional (can be empty array)

---

## Bug Naming Conventions

### Summary Format
```
{SEVERITY}: {Affected Feature ID} - {What fails/breaks} ({Context})

Examples:
- CRITICAL: F3.4 Proration logic throws SQL error on plan downgrade (key account impacted)
- MAJOR: F4.2 Stripe webhook signature validation fails intermittently
- MINOR: F7.1 Email notification template has typo in renewal reminder
- PATCH: F2.4 Profile management UI misaligned button on mobile
```

---

## Bug Description Structure

### Required Fields in Description
1. **What happened** (symptom)
2. **Reproduction steps** (if applicable)
3. **Expected behavior**
4. **Actual behavior**
5. **Impact assessment** (how many tenants, revenue impact, etc.)

**Example:**
```
The Subscription Management API throws a fatal SQL constraint violation when 
processing a plan downgrade if the customer has more than 5 historical invoices. 

Reproduction:
1. Tenant with 6+ historical invoices
2. Attempt to downgrade from Plan A to Plan B
3. Proration calculation triggered
4. SQL constraint violation thrown

Expected: Downgrade completes with prorated refund
Actual: 500 error, subscription stuck in invalid state

Impact: Reported by T-CORP-401 (key account), affecting their production operations.
Estimated 3-5 other tenants may be affected based on invoice count analysis.
```

---

## Resolution Details Structure

### Required Fields
1. **Root cause** (technical explanation)
2. **Fix description** (what was changed)
3. **Testing performed** (validation)
4. **Deployment impact** (HELM_CHANGE, DB_MIGRATION, NONE)

**Example:**
```
Root Cause: The proration query was joining all historical invoices without 
pagination, causing a table lock when invoice count exceeded database connection 
timeout threshold.

Fix: Optimized proration query to use a subquery that handles historical invoice 
joins without locking the full table. SQL constraint relaxed and replaced with 
application-level validation.

Testing: Full regression suite passed. Tested with tenant data sets of 1, 5, 10, 
50, and 100 historical invoices.

Deployment Impact: NONE (code-only change)
```

---

## Impacted Modules Reference

### Valid Module Names (by Feature)
```
F1 (Auth):
- auth-service
- sso-provider
- oauth-gateway
- mfa-service
- rbac-engine
- session-manager

F2 (User Management):
- user-service
- org-hierarchy-db
- invitation-service
- profile-api

F3 (Subscription):
- subscription-service
- proration-engine
- trial-manager
- plan-config-api
- azure-sql-schema-f3

F4 (Billing):
- billing-service
- payment-api
- stripe-integration
- paypal-integration
- invoice-generator
- refund-processor
- helm-chart-f4

F5 (Usage Tracking):
- metering-service
- quota-enforcer
- analytics-collector
- usage-api

F6 (Tax):
- tax-calculator
- invoice-delivery
- credit-note-service
- tax-db-schema

F7 (Notifications):
- notification-service
- email-worker
- webhook-dispatcher
- alert-manager

F8 (Reporting):
- reporting-service
- analytics-dashboard
- ltv-calculator
- export-service

F9 (API):
- rest-api-gateway
- webhook-manager
- crm-connector
- accounting-integration
- api-docs-portal

F10 (Tenant Admin):
- tenant-provisioner
- feature-flag-service
- branding-service
- compliance-service
- ai-orchestrator
```

---

## Bug Status Workflow

### All Bugs Follow This Lifecycle
```
Open → In Progress → Code Review → QA Testing → Resolved → Closed
```

**For synthetic data generation:**
- All historical bugs (R2-R5): status = "Resolved" or "Closed"
- Release 6 bugs: status = "Resolved" (fixed in v1.6.0)

---

## Cross-Feature Bug Examples

### Pattern: Feature A + Feature B Interaction
These bugs become more common in Release 4+

**Example 1: F3 + F4**
```
JIRA-4025: CRITICAL - Subscription cancellation doesn't trigger prorated refund
- Affects: F3.2 (Subscription Lifecycle) + F4.6 (Refund Processing)
- Root cause: Event bus message ordering race condition
- Impact: 2 tenants (T-BETA-100, T-GAMMA-150)
```

**Example 2: F5 + F3**
```
JIRA-5018: MAJOR - Quota enforcement doesn't reset on subscription renewal
- Affects: F5.3 (Quota Enforcement) + F3.2 (Subscription Lifecycle)
- Root cause: Cache invalidation timing issue
- Impact: 5 tenants experiencing rate limit errors post-renewal
```

**Example 3: F8 + F3 + F4**
```
JIRA-5032: CRITICAL - MRR calculation double-counts prorated subscriptions
- Affects: F8.1 (Revenue Reports) + F3.4 (Proration) + F4.3 (Invoice Generation)
- Root cause: Proration events not deduplicated in reporting aggregation
- Impact: All tenants using F8 - incorrect revenue reporting
```

---

## Tech Debt Tickets

### Distribution
Each release should have 2-3 tech debt tickets

### Common Patterns
- Dependency upgrades (security patches)
- Code refactoring (maintainability)
- Database optimization (performance)
- Test coverage improvements

**Examples:**
```
JIRA-3050: TECH_DEBT - Upgrade PayPal SDK to v2.0 (security patch)
- Priority: Medium
- Deployment Impact: HELM_CHANGE

JIRA-4061: TECH_DEBT - Refactor F3 proration engine for maintainability
- Priority: Low
- Deployment Impact: NONE

JIRA-5072: TECH_DEBT - Add database indexes for F5 analytics queries
- Priority: Medium
- Deployment Impact: DB_MIGRATION
```

---

## Bug Generation Checklist

For each bug ticket, ensure:
- [ ] `issue_key` follows pattern: JIRA-{RELEASE_NUM}{SEQUENTIAL}
- [ ] `severity` matches priority guidelines
- [ ] `linked_feature_id` references valid F1-F10 feature
- [ ] `impacted_modules` contains valid module names
- [ ] CRITICAL bugs have realistic `customer_context` (60% with tenant ID)
- [ ] `description` includes what/why/impact
- [ ] `resolution_details` includes root cause + fix
- [ ] `fix_version` matches the release version where bug was fixed
- [ ] `release_notes_excerpt` is customer-friendly, non-technical

---

## Summary: Quick Reference

| Release | Total Bugs | Critical | Major | Minor | Top Features Affected |
|---------|------------|----------|-------|-------|----------------------|
| R2 | 15 | 2 | 4 | 9 | F3 (40%), F4 (30%) |
| R3 | 25 | 3 | 6 | 16 | F3 (30%), F4 (25%), F6 (20%) |
| R4 | 28 | 3 | 7 | 18 | F3+F4 (25%), F6 (20%), F8 (20%) |
| R5 | 30 | 3 | 8 | 19 | F3/F4/F6 (35%), F8/F9 (35%) |
| R6 | 15-18 | 5 | 6-8 | 5-7 | F3, F4, F6 (explicit test case) |
