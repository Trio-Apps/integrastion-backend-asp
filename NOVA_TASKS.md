# NOVA Tasks — Phase 2 Feature Delivery (Talabat × Foodics / PICK)

> Derived from `docs/phase2/` (00-mini-brs, 01-delivery-plan, 02-sla-support-plan,
> 04-item-prices-discounts-mapping, 05-talabat-api-audit) and the current code on `develop`.
> Current phase: **Phase 2 feature delivery** (started 28 Jun 2026, ~40 MD, target mid-August).
> The Week-1 SLA sprint (centralized 429/timeout handler, import-log contract migration) is done.

---

## 0. SLA Support sprint — DONE (for context)

- [x] Centralized outbound rate-limit / 429 handler with backoff + jitter (`src/OrderXChange.Application/Resilience/`)
- [x] Talabat API audit vs. live OpenAPI specs (`docs/phase2/05-talabat-api-audit.md`)
- [x] Import-log migrated to `GET /v2/chains/{chainCode}/vendors/{posVendorId}/menu-import-logs` (verified live)

---

## 1. User / Role / Branch Authorization — 5 MD (BRS §1) — in progress

- [x] Granular permissions defined (`OrderXChangePermissions`: `Orders`, `Availability(.Manage)`, `FoodicsAccounts`, `TalabatAccounts`, `Branches.Manage`, `Branches.All`) + en/ar strings
- [x] `RoleBranch` entity + `AppRoleBranches` table (migration `20260702130000_AddRoleBranches`) — grants a role access to a Foodics branch
- [x] `RoleBranchAppService` (`/api/app/role-branch/*`, guarded by `Branches.Manage`): GetAvailableBranches / GetForRole / UpdateForRole
- [x] `ICurrentUserBranchProvider` / `CurrentUserBranchProvider` + `UserBranchScope` (fail-closed; `Branches.All` ⇒ unrestricted)
- [x] **Seed `Branches.All` + new Phase 2 permissions to the admin role** (data-seed contributor + DbMigrator) — MUST land before any query enforces branch scope, or admin is locked out
- [x] Angular: role → branch assignment screen (extend the ABP `/identity` roles UI with a "Branches" action calling `/api/app/role-branch`; multi-select from `GetAvailableBranches`)
- [x] Angular: apply permission guards / nav visibility for the new permissions (`angular/src/app/guards/`, `route.provider.ts`) — Orders, Availability, Branch assignment menu entries
- [x] Wire `ICurrentUserBranchProvider` into feature queries as they are built (Orders Console §2, Availability §5) — map `TalabatOrderSyncLog.VendorCode` → branch via `TalabatAccount.FoodicsBranchId`
- [x] Verify: non-admin user with 1 branch sees only that branch's data everywhere; user with no grants sees nothing (fail-closed); admin unaffected

## 2. Orders Console — 6 MD (BRS §2)

- [x] Extend `TalabatOrderSyncLog` with listing fields parsed from the webhook at receipt: CustomerId, CustomerName, CustomerAddress, PaymentMethod, ExpeditionType / **TMP-vs-TGO indicator** (platform vs. vendor delivery), Channel, GrandTotal, DiscountTotal (+ EF migration; new migration id must sort after `20260702130000`; use the local EF tool 9.0.5 via `dotnet tool run`)
- [x] Backfill job/script for existing rows from the stored `WebhookPayloadJson`
- [x] Order-details endpoint `GetDetailsAsync(id)`: parse `WebhookPayloadJson` → Date, Items, Modifiers (selectedToppings), Quantity, unit/paid Amount, Total Amount, Discounts — including the new original-price + discount/sponsorship fields (`unitPrice`, `paidPrice`, `discounts[].sponsorships[]` per `docs/phase2/04`)
- [x] Update `TalabatOrderLogAppService.GetListAsync` to return the new listing columns; guard the service with `OrderXChangePermissions.Orders`; apply branch scope from §1
- [x] Verify the order → Foodics dispatch mapping still prices correctly under the doc-04 payload semantics (original prices + product-level discounts); run the doc-04 test-scenario matrix (no discount / base / base+booster / variation / mixed)
- [x] Angular `talabat-orders`: new columns (Order ID, Customer ID, Customer Name, Customer Address, Channel, TMP/TGO, Payment Method) + expandable row with the details above
- [x] Near-real-time refresh of the orders list (auto-refresh polling interval; consider SignalR later)

## 3. Advanced Order Filtering — 2 MD (BRS §5)

- [x] Extend `GetTalabatOrderLogsInput` + query: filter by Branch, Customer Name, Customer Phone (needs §2 columns); Date range & Status already exist — all filters combinable
- [x] Angular: filter bar on the Orders page (date range, branch dropdown limited to the user's branch scope, status, customer name, phone)

## 4. Smart Search — 4 MD (BRS §4)

- [x] Unified search app service: one keyword / partial-text query across orders (`TalabatOrderSyncLog` incl. customer fields), items & modifiers (`FoodicsProductStaging`), and branches (Foodics branch lookup) — grouped results, branch-scoped, permission-trimmed
- [ ] Angular: global smart-search UI (search box + grouped results, deep-links to order/item)

## 5. Item & Modifier Availability — backend — 7 MD (BRS §3, audit §"Deferred")

- [ ] **Migrate the Talabat item-availability client to the current contract**: `PUT /v2/chains/{chainCode}/vendors/{posVendorId}/catalog/items/availability` with `CatalogItemAvailabilityUpdateRequest` (`enable` / `disable` / `disable-until-next-business-day` / `disable-until-timestamp`) — replaces the retired `PUT /v2/catalogs/stores/{vendorCode}/items/availability` used by `UpdateItemAvailabilityAsync` / `UpdateBranchItemAvailabilityAsync` in `TalabatCatalogClient.cs`
- [ ] Consolidate vendor/branch open-close on `UpdateVendorAvailabilityV2Async` (`PUT /v2/chains/{chainCode}/remoteVendors/{posVendorId}/availability`); remove the legacy `POST /vendors/{vendorCode}/availability` client methods and repoint callers (`TalabatTestController`, `TalabatDashboardAppService`, `TalabatMenuManagementController`)
- [ ] Availability state model: new entity (item/modifier ref, FoodicsAccountId, branch, InStock/OutOfStock, mode **ForADay** / **TillFurtherNotice**, RestoreAtUtc, who/when audit) + EF migration
- [ ] Foodics active/inactive sync: inactive items/modifiers from Foodics excluded from the console AND from the Talabat catalog submission; honor per-branch `is_in_stock` / `is_active` pivot data (extend `FoodicsMenuDtos` if fields are missing)
- [ ] `AvailabilityAppService`: list items + modifiers with search by item/modifier code & name; toggle In/Out of Stock for one-or-many branches in a single call; `Availability` (view) / `Availability.Manage` (toggle) permissions; branch scope from §1; write an audit record for every toggle
- [ ] Route all toggle calls through the centralized rate-limit handler with batching/debounce (burst toggles across branches are the main 429 trigger — SLA plan §2)
- [ ] Reflect Talabat-side out-of-stock back into the console: adopt `GET /v2/chains/{chainCode}/vendors/{posVendorId}/catalog/items/unavailable` (spec marks it *in development* — validate first; fall back to polling/own-state reconciliation if unusable)
- [ ] Branch opening hours: fetch from Foodics branch data (extend `FoodicsBranchDto` with opening/closing times if needed) — needed for console display and For-a-Day restore times
- [ ] Proof-of-concept the new availability endpoint against staging (`tlbt-pick` / `PH-SIDDIQ-002`) **before** building the UI (plan §4 risk mitigation)

## 6. Availability Management UI — 6 MD (BRS §3)

- [ ] Angular availability page: grid of items + modifiers with status per branch, search by code/name, In-Stock/Out-of-Stock toggle
- [ ] On toggle: popup to select one or multiple branches (limited to the user's branch scope)
- [ ] Out-of-Stock duration choice: **'For a Day'** vs. **'Till Further Notice'**
- [ ] Display branch opening hours for reference in the popup/page
- [ ] Hide toggle for users without `Availability.Manage`; hide page without `Availability`

## 7. Auto-Restore Scheduler & Safeguards — 3 MD (BRS §3)

- [ ] Hangfire recurring job: restore 'For a Day' items to In Stock at the next day's branch opening hour (respect branch timezone from Foodics)
- [ ] Safeguards: idempotent restore, skip items manually changed since the toggle, retry via the DLQ strategy, batch restores through the rate-limit handler
- [ ] Audit the automatic restore actions (same audit trail as manual toggles)

## 8. Integration / Regression QA — 3 MD

- [ ] Regression: live order webhook flow (no outbound-Talabat regressions), menu sync (catalog `PUT …/catalog` unchanged), availability toggles, role/branch scoping, filters, smart search
- [ ] Feature-flag risky flows touching the live webhook path (plan §4)
- [ ] UAT pass with PICK on staging (tfapi-dev / tfconsole-dev)

## 9. Deployment, Monitoring & Handover — 2 MD

- [ ] Deploy to testing first; then live per the established surgical procedure (**never overwrite live `appsettings.json`**; URLs come from each server's `.env`)
- [ ] Verify 429/timeout monitoring & alerting covers the new availability traffic (SLA commitments: alert ≤ 5 min, auto-recovery ≤ 60 s)
- [ ] Release notes + handover docs; merge `develop` → `main` at the milestone

---

## Backlog / not scheduled in the 40-MD plan (from the API audit)

- [ ] Adopt `POST /v2/orders/{orderToken}/adjust-preparation-time` (dynamic prep-time, `docs/phase2/03`) — parse `PreparationTimeAdjustments` from dispatch payload; Platform-Delivery orders only; valid until rider assigned
- [ ] Adopt `POST /v2/orders/{orderToken}/preparation-completed` (mark order prepared)
- [ ] Retire the deprecated XML menu-import webhook path (`menu.import.requested` → `TalabatWebhookController.MenuImportRequestAsync`)
- [ ] Secrets remediation follow-up: rotate previously committed credentials + purge git history
