# Talabat POS Middleware API — Audit (current contract vs. our code)

> SLA Item 2 — "Talabat API updates". Audited the live OpenAPI specs against the endpoints
> our integration actually calls. Date of audit: June 2026.

## Source specs (live)

| Spec | URL |
|------|-----|
| POS Middleware API (OpenAPI 3.1, v1.0.0) | https://integration-middleware.me.restaurant-partners.com/apidocs/middlewareExternalApi.yaml |
| POS Plugin API (v1.0.0) | https://integration-middleware.me.restaurant-partners.com/apidocs/pluginApi.yaml |
| Shared components | https://integration-middleware.me.restaurant-partners.com/apidocs/shared-components.yaml |
| Catalog schema | https://integration-middleware.me.restaurant-partners.com/apidocs/catalog-schema.yaml |
| Docs (UI) — middleware / plugin | …/apidocs/pos-middleware-api · …/apidocs/pos-plugin-api |

## ✅ Auth / "the user" — UNCHANGED

`POST /v2/login` (`application/x-www-form-urlencoded`), `LoginRequest` requires exactly:
`username`, `password`, `grant_type` (static `client_credentials`).

Our `TalabatAuthClient` sends exactly these three fields. **Changing "our user" therefore needs
no code change — only swapping the credentials** (in `TalabatAccount` in the DB per vendor, or
the `Talabat:Username/Password/Secret` fallback in `appsettings.secrets.json`).

## ✅ Catalog submission (main flow) — matches

`PUT /v2/chains/{chainCode}/catalog` — our `TalabatCatalogClient.SubmitV2CatalogAsync` uses this. Current.

## ⚠️ Endpoints in our code that no longer match the current contract

| Function | Our code | Current contract |
|----------|----------|------------------|
| Import logs | `GET /v2/chains/{chainCode}/catalog/import-log` | `GET /v2/chains/{chainCode}/vendors/{posVendorId}/menu-import-logs` |
| Item availability | `PUT /v2/catalogs/stores/{vendorCode}/items/availability` | `PUT /v2/chains/{chainCode}/vendors/{posVendorId}/catalog/items/availability` |
| Vendor/store availability | `POST /vendors/{vendorCode}/availability` | `PUT /v2/chains/{chainCode}/remoteVendors/{posVendorId}/availability` |

These should be migrated (path + method). The availability ones overlap directly with the Phase 2
"Item & Modifier Availability Management" feature, so migrate them as part of that workstream.

## ⚠️ Deprecated endpoints (3) — we don't call them outbound

- `POST /v2/chains/{chainCode}/remoteVendors/{posVendorId}/menuImport` (XML menu) — **deprecated**
- `POST /v2/menu/{vendorCode}/{menuImportId}` (XML menu via trigger) — **deprecated**
- `PUT /v2/chains/{chainCode}/remoteVendors/{posVendorId}/posReachabilityStatus` — **deprecated**

We do not *call* these. We do still expose a webhook handler for the old XML trigger flow
(`menu.import.requested` → `TalabatWebhookController.MenuImportRequestAsync`). Since the live
catalog flow is the JSON `PUT …/catalog`, that XML path can be retired later.

## 🆕 New endpoints worth adopting

| Endpoint | Use |
|----------|-----|
| `PUT /v2/chains/{chainCode}/vendors/{posVendorId}/catalog/items/availability` | Item/modifier in/out-of-stock — **Phase 2 availability** |
| `PUT /v2/chains/{chainCode}/remoteVendors/{posVendorId}/availability` | Open/close a branch |
| `GET /v2/chains/{chainCode}/vendors/{posVendorId}/catalog/items/unavailable` | Read current unavailable items *(in development)* |
| `POST /v2/orders/{orderToken}/adjust-preparation-time` | Dynamic prep-time (see [03-dynamic-prep-time-api](./03-dynamic-prep-time-api.md)) |
| `POST /v2/orders/{orderToken}/preparation-completed` | Mark order prepared |
| `POST /v2/order/{orderToken}/modifications/product` | Modify order products |

## Changes applied (this session)

- ✅ **Import logs migrated** to the current contract `GET /v2/chains/{chainCode}/vendors/{posVendorId}/menu-import-logs`
  (was the retired `…/catalog/import-log`). `GetCatalogImportLogAsync` now takes `vendorCode`, parses the
  per-vendor log response, and adapts it to `TalabatCatalogImportLogResponse`. Callers updated
  (`TalabatCatalogSyncService.GetImportStatusAsync`, `TalabatTestController`). **Verified live: HTTP 200**
  against `tlbt-pick` / `PH-SIDDIQ-002`. Note: the live response is keyed by vendor code at the top level
  (`{"PH-SIDDIQ-002":[…]}`) — different from the spec's `menuImportLogs` wrapper — so the parser tolerates both.

## Deferred (recommended for the Phase 2 availability feature)

- **Item availability** (`PUT …/vendors/{posVendorId}/catalog/items/availability`, body
  `CatalogItemAvailabilityUpdateRequest` = enable / disable / disable-until-next-business-day / disable-until-timestamp)
  — this endpoint *is* the Phase 2 "Item & Modifier Availability" feature (For-a-Day / Till-Further-Notice). Our
  current `UpdateItemAvailabilityAsync` / `UpdateBranchItemAvailabilityAsync` use a retired path
  (`/v2/catalogs/stores/{vendorCode}/items/availability`) and should be rebuilt against this contract in Phase 2.
- **Vendor availability** — `UpdateVendorAvailabilityV2Async` already matches the current
  `PUT …/remoteVendors/{posVendorId}/availability` contract; the legacy `UpdateVendorAvailabilityAsync`
  (`POST /vendors/{vendorCode}/availability`) and its callers should be consolidated onto V2 during Phase 2.

## Bottom line

- **Live/critical flows are safe:** catalog submit (`PUT …/catalog`) and login (`/v2/login`) already match the
  current contract; the order flow makes **no** outbound Talabat calls (inbound webhook → Foodics only).
- **"Change the user" = just new credentials, no code change** (auth contract unchanged).
- **Import-log migrated now.** Availability endpoint migrations are best done as part of the Phase 2 availability feature.
