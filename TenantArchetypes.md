## Archetype 1: Key Account Enterprise (5 tenants)
- risk_tolerance: LOW
- daily_usage_score: 85-95
- failed_jobs_percent: 0.01-0.05
- active_features: F1, F3, F4, F6, F7, F9 (6-8 features)
- is_key_account: true
- total_subscriptions: 10000-50000
- Tenant IDs: T-CORP-401, T-MEGA-500, T-ULTRA-600, T-PRIME-700, T-ELITE-800

## Archetype 2: Heavy User (10 tenants)
- risk_tolerance: MEDIUM
- daily_usage_score: 70-84
- failed_jobs_percent: 0.05-0.15
- active_features: F1, F3, F4, F5, F8 (5-6 features)
- is_key_account: false
- total_subscriptions: 5000-10000
- Tenant IDs: T-ALPHA-050, T-BETA-100, ...

## Archetype 3: Standard B2B (25 tenants)
- risk_tolerance: MEDIUM
- daily_usage_score: 40-69
- failed_jobs_percent: 0.10-0.25
- active_features: F1, F3, F4 (3-5 features)
- is_key_account: false
- total_subscriptions: 1000-5000

## Archetype 4: Light User (10 tenants)
- risk_tolerance: HIGH
- daily_usage_score: 10-39
- failed_jobs_percent: 0.20-0.40
- active_features: F1, F3 (2-3 features)
- is_key_account: false
- total_subscriptions: 100-1000