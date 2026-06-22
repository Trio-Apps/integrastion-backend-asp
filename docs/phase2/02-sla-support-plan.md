# Talabat × Foodics Integration — SLA Support Plan

> **Start date:** Sunday, 21 June 2026
> **Priority:** Week 1 (target by Thursday 25 June)

## 1. Overview

This SLA Support plan covers the operational hardening items split out from Phase 2: the Foodics
timeout / Too Many Requests (429) handling and the Talabat API updates. These are addressed first,
as a priority sprint in Week 1, and are committed as service-level guarantees rather than feature
man-day items. The remaining Phase 2 feature workstreams are tracked in the separate
[Phase 2 Delivery Plan](./01-delivery-plan.md).

## 2. Scope

**Target:** both items completed by end of Thursday 25 June 2026.

> **Note:** the Talabat API updates assume a limited number of affected endpoints. If the initial
> audit reveals many deprecated calls on a live system, migration and regression may require an
> additional day.

| Item | Scope |
|------|-------|
| Foodics timeout / 429 handling | Centralized rate-limit & timeout handler for Foodics and Talabat, backoff + jitter, per-tenant throttling, batching/debounce of availability toggles, caching, and 429/timeout monitoring & alerting. |
| Talabat API updates | Audit all consumed Talabat (and Foodics) endpoints against the latest API contracts, identify deprecated endpoints/fields, migrate to current versions, and run regression on order flow and menu sync. |

## 3. Service-Level Commitments

The targets below define the behaviour the integration guarantees under Talabat/Foodics timeouts
and rate limiting.

| Commitment | Target | How it is met |
|------------|--------|---------------|
| Automatic retry | Every timeout / 429 retried automatically, up to 5 attempts | Exponential backoff + jitter, unified with the existing DLQ retry strategy. |
| No data loss | 0 dropped operations due to timeouts or rate limiting | Failed requests are queued / routed to the DLQ and guaranteed eventual execution. |
| Throughput protection | Requests stay within published Talabat/Foodics limits | Per-tenant token-bucket throttling; one tenant cannot exhaust another's quota. |
| Burst control | Availability toggles batched to avoid bursts | Batching / debouncing of toggle operations — the main trigger of 429. |
| Auto-recovery | Transient failures resolved ≤ 60 seconds, no manual action | Backoff schedule plus caching of repeated reads. |
| Eventual success rate | ≥ 99.5% of requests succeed despite timeouts / limits | Combined retry, queueing and throttling layers. |
| Alerting | Timeout / 429 spikes surfaced ≤ 5 minutes | Operational monitoring with threshold-based alerts. |

## 4. Assumptions

- Talabat and Foodics API credentials and endpoints are available for both audit and testing.
- Published rate limits and timeout behaviour for both providers are accessible or can be confirmed.
- Changes are validated against a staging environment before deployment to the live system.
