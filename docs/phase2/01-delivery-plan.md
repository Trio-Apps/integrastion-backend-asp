# Talabat × Foodics Integration — Phase 2 Work Plan & Delivery Timeline

> **Feature delivery start:** Sunday, 28 June 2026
> **Total effort:** 40 man-days (hardening items tracked separately in the [SLA Support plan](./02-sla-support-plan.md))
> **Target completion:** mid-August 2026

## 1. Overview

This plan sequences the Phase 2 feature workstreams of the Talabat × Foodics integration. The
operational hardening items — Foodics timeout / 429 handling and Talabat API updates — are tracked
separately in the SLA Support plan and run as the Week-1 priority sprint (21–25 June). Phase 2
feature delivery follows from 28 June across an eight-week timeline. Work assumes a single
developer; one working day ≈ one man-day, on a Sunday–Thursday work week.

## 2. Man-Day Estimation

| Area | MD | Risk | Notes |
|------|----|------|-------|
| Discovery & API contract review | 2 | Medium | Initial review of integration contracts and feature scope. |
| User / role / branch authorization | 5 | Medium | Permission model, branch/action matrix, backend enforcement, Angular guards. |
| Orders console enhancements | 6 | Medium | Expanded DTOs, table/details UI, TMP/TGO, near-real-time refresh. |
| Advanced order filters | 2 | Low | Date/branch/status/customer/phone, combined queries. |
| Smart search | 4 | Medium | Unified search across orders, customers, items, modifiers, branches. |
| Item/modifier availability backend | 7 | High | Availability state model, Talabat updates, Foodics inactive handling, audit. |
| Availability management UI | 6 | High | Grid, search, toggle, multi-branch popup, duration, opening hours. |
| Auto-restore scheduler & safeguards | 3 | High | For-a-day restore jobs, retry handling, rate-limit safety. |
| Integration / regression QA | 3 | Medium | Order flow, menu sync, availability, roles, filters, UAT. |
| Deployment, monitoring, handover | 2 | Low | Docker deployment, release notes, handover. |
| **Total estimated effort** | **40** | | feature workstreams — ≈ mid-August 2026 |

## 3. Delivery Timeline

| Week | Dates | Work | MD |
|------|-------|------|----|
| 1 | 28 Jun – 2 Jul | Discovery & contract review (2) + authorization start (3) | 5 |
| 2 | 5–9 Jul | Authorization (2) + orders console start (3) | 5 |
| 3 | 12–16 Jul | Orders console (3) + advanced filters (2) | 5 |
| 4 | 19–23 Jul | Smart search (4) + availability backend start (1) | 5 |
| 5 | 26–30 Jul | Availability backend | 5 |
| 6 | 2–6 Aug | Availability backend (1) + availability UI start (4) | 5 |
| 7 | 9–13 Aug | Availability UI (2) + auto-restore scheduler (3) | 5 |
| 8 | 16–20 Aug | Integration / regression QA (3) + deployment & handover (2) | 5 |

## 4. Key Risks & Dependencies

| Risk / Dependency | Impact | Mitigation |
|-------------------|--------|------------|
| Talabat/Foodics API contract gaps | Availability for modifiers, branch updates, and opening hours must be confirmed. | Validate endpoints in Week 1; run a proof-of-concept before UI work. |
| Volume of deprecated endpoints | If many endpoints are deprecated on a live system, migration + regression may spill an extra day. | Confirmed by the Day-4 audit; raise early if scope exceeds 2 MD. |
| API rate limiting (429) | Burst availability toggles across branches can trigger 429 on Talabat/Foodics. | Centralized handler, per-tenant throttling, batching/debounce, backoff+jitter. |
| Live system regression | Order processing is live; changes must not break the current webhook flow. | Feature-flag risky flows and run regression before each deployment. |
| ID mapping accuracy | Wrong product/modifier/branch mapping can update the wrong availability. | Use existing staging data, add validation, audit every action. |
