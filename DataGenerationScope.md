# Data Generation Scope
## Jira Tickets: ~300 Total

### Release 1 (MVP) - v1.0.0
- 15 Stories + 30 Subtasks = 45 tickets
- 0 Bugs (nothing to break yet)
- Total: 45 tickets

### Release 2 - v1.2.0
- 15 Stories + 30 Subtasks = 45 tickets
- 15 Bugs (regressions from R1 features)
- Total: 60 tickets

### Release 3 - v1.3.0

- 12 Stories + 24 Subtasks = 36 tickets
- 25 Bugs (R1 + R2 features breaking)
- Total: 61 tickets

### Release 4 - v1.4.0

- 10 Stories + 20 Subtasks = 30 tickets
- 28 Bugs (cumulative complexity)
- Total: 58 tickets

### Release 5 - v1.5.0

- 10 Stories + 20 Subtasks = 30 tickets
- 30 Bugs (mature product, edge cases)
- Total: 60 tickets

### Release 6 - v1.6.0 (TEST CASE)

- 8 Stories + 16 Subtasks = 24 tickets
- 3-5 CRITICAL Bugs (affecting F3, F4, F6)
- 10 MAJOR/MINOR Bugs
- Total: 37-39 tickets

#### GRAND TOTAL: ~324 tickets
----
## Tenants: 50 Total

- All at current_version: "v1.5.0"
- Feature usage distribution:
  - 5 Key Accounts (LOW risk, HIGH usage, F1-F9)
  - 10 Heavy Users (MEDIUM risk, F1-F8)
  - 25 Standard (MEDIUM risk, F1-F5 or F1-F6)
  - 10 Light Users (HIGH risk, F1-F4 only)

---
## Release 6 Test Case

- 3-5 CRITICAL bugs affecting:
  - F3_SubscriptionMgmt (proration edge case)
  - F4_BillingPayments (Stripe retry logic)
  - F6_InvoicingTax (tax calculation for multi-currency)
- 8 tenants directly affected (15% of 50)
  - These are tenants using F3/F4/F6 features
  - Mix of key accounts + standard tenants
- Expected AI behavior:
  - 7-8 affected tenants → MUST upgrade
  - 15-20 tenants using related features → SHOULD upgrade
  - 25-30 tenants not using affected features → CAN SKIP