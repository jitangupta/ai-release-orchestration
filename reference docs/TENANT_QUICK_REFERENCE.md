# Tenant Data Quick Reference

## Key Tenant IDs for Bug Generation

### Explicitly Mentioned in Release 6 Test Case Bugs

**CRITICAL Bugs:**
- **JIRA-6001** (F3 Multi-year proration): T-CORP-401
- **JIRA-6002** (F4 Stripe retry): T-ALPHA-050
- **JIRA-6003** (F6 EU VAT): T-EURO-500
- **JIRA-6004** (F4 Payment deletion): T-ULTRA-600
- **JIRA-6005** (F3 Cancellation refund): T-PRIME-700

**MAJOR Bugs:**
- **JIRA-6008** (F5 Quota reset): T-ZETA-900
- **JIRA-6009** (F7 Webhook flood): T-KAPPA-200
- **JIRA-6011** (F9 CRM OAuth): T-LAMBDA-350

### Key Accounts (for HIGH priority bugs)
```
T-CORP-401, T-MEGA-500, T-ULTRA-600, T-PRIME-700, T-ELITE-800
```

### Heavy Stripe Users (for payment bugs)
```
T-ALPHA-050, T-BETA-100, T-DELTA-200, T-SIGMA-400, T-OMEGA-300
```

### EU Tenants (for tax bugs)
```
T-EURO-500, T-EURO-600
```

### Multi-Year Contract Tenants (for proration bugs)
```
T-CORP-401, T-MEGA-500, T-GAMMA-150
```

---

## Feature Usage Map

### F3_SubscriptionMgmt (100% adoption)
All 50 tenants - use for any subscription bugs

### F4_BillingPayments (98% adoption)
49 tenants - missing only T-ZETA-900

### F6_InvoicingTax (18% adoption)
```
T-CORP-401, T-MEGA-500, T-ULTRA-600, T-PRIME-700, T-GAMMA-150,
T-KAPPA-500, T-EURO-500, T-EURO-600, T-ARTEMIS-150
```

### F8_ReportingAnalytics (36% adoption)
```
T-CORP-401, T-ELITE-800, T-ALPHA-050, T-BETA-100, T-ETA-350,
T-IOTA-450, T-NU-650, T-OMICRON-750, T-CHI-600, T-EURO-600,
(+ 8 more)
```

### F9_APIIntegrations (10% adoption)
```
T-CORP-401, T-MEGA-500, T-ULTRA-600, T-ELITE-800, T-LAMBDA-350
```

---

## Archetype-Based Targeting

### For CRITICAL bugs (60% should reference customer)
**Key Accounts (40% of those 60%)**:
- T-CORP-401, T-MEGA-500, T-ULTRA-600, T-PRIME-700, T-ELITE-800

**Heavy/Standard Users (remaining 60%)**:
- T-ALPHA-050, T-BETA-100, T-GAMMA-150, T-DELTA-200, T-EPSILON-250

### For MAJOR bugs (30% should reference customer)
**Any tenant except Light Users** (pick from 40 tenants with MEDIUM/LOW risk)

### For MINOR/PATCH bugs (10% reference customer)
**Light Users or low-priority Standard**:
- T-NOVA-100, T-SPARK-150, T-PULSE-200, T-ZENITH-250, T-VORTEX-300

---

## Bug Generation Prompt Template

```
Generate [N] Bug tickets for Release [X] following BugGenerationRules.md.

Valid tenant IDs from tenants.jsonl:
- Key Accounts: T-CORP-401, T-MEGA-500, T-ULTRA-600, T-PRIME-700, T-ELITE-800
- Heavy Users: T-ALPHA-050, T-BETA-100, [...]
- [include 20-30 tenant IDs as reference]

For CRITICAL bugs:
- 60% must reference a tenant via customer_context.customer_id
- 40% of those should be key accounts (is_key_account: true in tenant profile)

Example:
{
  "customer_context": {
    "customer_id": "T-CORP-401",  // Valid tenant ID
    "is_key_account": true         // Matches tenant profile
  }
}
```

---

## Validation Checklist for Bug Generation

When generating bugs that reference tenants:

1. **Tenant ID exists** in tenants.jsonl
2. **Feature match**: Bug's `linked_feature_id` ∈ tenant's `active_features`
3. **Key account flag**: `is_key_account` matches tenant profile
4. **Severity alignment**: Key account bugs should be HIGH/HIGHEST priority

Example check:
```python
tenant = get_tenant("T-CORP-401")
bug_feature = "F3_SubscriptionMgmt"

assert bug_feature in tenant['active_features']  # ✓ T-CORP-401 uses F3
assert tenant['risk_tolerance'] == 'LOW'          # ✓ Key account
```

---

## Statistics for Generation Planning

| Metric | Value | Use Case |
|--------|-------|----------|
| Total tenants | 50 | Total pool for customer_context references |
| Key accounts | 5 | CRITICAL bug targets (high priority) |
| Heavy users | 10 | MAJOR bug targets |
| Standard | 25 | MINOR bug occasional references |
| Light users | 10 | PATCH bug occasional references |
| F3 users | 50 | Any subscription bug |
| F4 users | 49 | Any billing bug |
| F6 users | 9 | Tax bug targets (limited pool) |
| EU tenants | 2 | VAT bug specific targets |

---

## Sample Tenant Profiles (for reference)

### Key Account Example
```json
{
  "tenant_id": "T-CORP-401",
  "risk_tolerance": "LOW",
  "active_features": ["F1", "F3", "F4", "F6", "F7", "F8", "F9"],
  "daily_usage_score": 92,
  "key_metrics": {"total_subscriptions": 42000}
}
```
**Use for**: CRITICAL bugs, high-impact issues

### Standard User Example
```json
{
  "tenant_id": "T-GAMMA-150",
  "risk_tolerance": "MEDIUM",
  "active_features": ["F1", "F3", "F4", "F6", "F8"],
  "daily_usage_score": 75,
  "key_metrics": {"total_subscriptions": 7800}
}
```
**Use for**: MAJOR/MINOR bugs, typical user scenarios

### Light User Example
```json
{
  "tenant_id": "T-SPARK-150",
  "risk_tolerance": "HIGH",
  "active_features": ["F1", "F3"],
  "daily_usage_score": 18,
  "key_metrics": {"total_subscriptions": 420}
}
```
**Use for**: MINOR/PATCH bugs, low-priority reports
