# Phase 2 — Talabat × Foodics Integration (PICK)

This folder holds the Phase 2 reference documentation, converted to Markdown from the
original source documents (`.docx` / `.pdf`).

## Documents

| # | Document | Purpose |
|---|----------|---------|
| 00 | [Mini BRS — Phase 2](./00-mini-brs.md) | Business requirements for Phase 2 (the "what"). |
| 01 | [Phase 2 Delivery Plan](./01-delivery-plan.md) | Work plan, man-day estimation, timeline, risks. |
| 02 | [SLA Support Plan](./02-sla-support-plan.md) | Week-1 hardening sprint: timeout / 429 handling + Talabat API updates. |
| 03 | [Dynamic Order Preparation Time Adjustment API](./03-dynamic-prep-time-api.md) | Talabat POS API: dynamically adjust order prep time. |
| 04 | [Item Prices & Discounts Mapping](./04-item-prices-discounts-mapping.md) | Talabat POS payload changes: original prices + product-level discounts/sponsorships. |

## Scope at a glance

Phase 2 enhances the integration console with:

- **User / role / branch authorization** — multi-user, role-based access, branch & action scoping.
- **Orders console** — list + expandable details of all Talabat orders, near-real-time sync.
- **Item & modifier availability management** — in/out-of-stock toggles, multi-branch, "For a Day" / "Till Further Notice", Foodics active/inactive sync.
- **Smart search** — unified search across orders, items, modifiers, customers, branches.
- **Advanced order filtering** — date, branch, status, customer name, phone (combinable).

Operational hardening (Foodics timeout / 429 handling, Talabat API contract updates) is split
into the **SLA Support Plan** and runs as a priority Week-1 sprint.

## Timeline summary

- **SLA Support sprint:** 21–25 June 2026 (Week 1 priority).
- **Phase 2 feature delivery:** starts Sunday 28 June 2026, ~40 man-days, target completion mid-August 2026.
- Single developer; one working day ≈ one man-day; Sunday–Thursday work week.
