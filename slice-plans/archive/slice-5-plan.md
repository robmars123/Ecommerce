# Slice 5 — Orders + Fulfillment (plan)

**Mode:** Autonomous · **Planner:** Rob · **Builder:** Marco · 2026-11-06

**Read (Marco's whole list):** CLAUDE.md (loaded) · arch §2, §3.4 · §6.1, §6.2 · §7.1 rows `orders`, `order_items`, `order_status_history`, `shipments`, `audit_log` · §7.2 · §7.3 · §7.4 row `Shipping:Carriers` · §8.6 · §9 rows `GET /orders`, `/admin/orders/{id}/…` + "Catalog read contract" (pagination) + "Auth and admin contract" + "Orders contract (Slice 5)" · §11.2 rows 7, 29, 30 · §11.3 row F12 · §12.3 row 5 · §5.1–5.3 · app context §4.6, §4.11 · decision log: `grep -n -i "slice 5\|override\|audit_log\|carrier" docs/architecture/13.2-decision-log.md` (rows added by this plan). Patterns to copy: `Modules/Catalog` (layout, `AdminResult` → 409/400), `AuthController` (`sub`), `IdentityTestEnv.cs` (tokens), `features/catalog`.

---

## 0. Slice-order override and the existing working tree

- **Owner override (Nathaniel, 2026-11-06, verbatim "do the 5 first"):** Slice 5 is built before 2, 3, 4. Informed override of §12.2 rule 2 and quality-bar §3 "Slice order" only. **Not** an override of §2 or §3.4: if any part of this plan needs a fake order, fake payment or fake stock movement on a production path, the slice is BLOCKED. Rob's review records the order check as "owner-overridden", not failed.
- **Uncommitted frontend work already in the tree** (Shop default route, sign-in modal, Register restyle + password checklist, related test and doc edits). Made at Nathaniel's request outside any plan. **My Slice 5 review covers the whole diff, these included**, in a separately labelled section. Quality bar §1 makes "an unrelated refactor mixed into a slice" blocking, so: **recommended** — Nathaniel commits that work before Marco starts; if he does not, the review treats it as owner-requested out-of-slice work and judges it at the same bar, but does not count the mixing itself as a failure. Marco: do not modify those files beyond what this plan lists (`AccountBar.tsx`, `tests/routes.test.tsx`).

## 1. Dependency analysis — what 5 assumes from 2–4, and what is honest now

| Slice 5 assumes | Comes from | Status | What this slice does |
|---|---|---|---|
| Orders exist, created by checkout with server totals and price snapshots | 4 | not built | **No order-creation path.** No `Order.Place`, no POST, no seeder. Integration tests seed rows with SQL in a test fixture only (D3) |
| Order reaches `Paid` via verified webhook (rule 5) | 4 | not built | `PendingPayment → Paid` **not built** (D2). The status enum and legal-edge table include it, nothing calls it |
| `PendingPayment → Cancelled` on reservation expiry; inventory commit on Paid | 2 + 4 | Inventory = 1a stub only | Not built. Fulfillment never touches stock (Paid already committed it in 4) |
| `payments` rows → `paymentStatus` in detail | 4 | no table | Field **omitted** from the Slice 5 response; Slice 4 adds it (additive) |
| `refunds` → detail `refunds[]`; `→ Refunded/Returned`; process/ship 409 while full refund Pending | 6 | no table | Omitted / not built; Slice 6 carry-in |
| Outbox for `OrderShipped` → email, SignalR live status | 4 (table) + 6 (consumers) | no table | No event emitted (no side effect exists, so rule 6 holds). Slice 6 adds the outbox insert to the `ship` transaction (carry-in) |
| `ICurrentUser` | 3 carry-in | not built | Not added. Controllers read `sub` like `AuthController` and pass `userId` in |
| `audit_log` | first needed here | no table | **Built here** as a platform table (D5) |

**Buildable honestly now:** Orders module (two projects), the four Orders tables + `audit_log`, the full §7.3 legal-edge table in the domain, `GET /orders`, `GET /orders/{id}` (owner-scoped), admin `process` / `ship` / `deliver` / `status-override`, `Shipping:Carriers` config + server-built `trackingUrl`, `/orders` and `/orders/[id]` screens.

**Out of scope:** checkout, payments, webhooks, inventory, outbox, notifications, SignalR, refunds, returns, customer cancel, `GET /admin/orders` list/detail and admin order screens (feature 11 list/filter is not in any §12.3 row — open question for Nathaniel, §8 Risks).

## 2. Required tests — now vs deferred

| Test | Now (Slice 5) | Deferred — **stays open, never marked done** |
|---|---|---|
| **7** | Read: B's `GET /orders/{id}` → 403; B's orders never appear in A's `GET /orders` | **Refund** part → Slice 6 (admin refund / return request). **Cancel** part: §9 has no customer cancel endpoint — doc gap for Nathaniel. `retry-payment` ownership → Slice 4 |
| **29** | List only caller's, newest first, paginated; detail with item snapshots, address, method, timeline rows in order, shipment with carrier, tracking number, `trackingUrl` | **Payment status** → Slice 4 (no payments table) |
| **30** | Full: every one of the 64 (from, to) pairs against §7.3 in the domain; API 409 + no history row for illegal process/ship/deliver/override; override legal-only + reason + audit | Webhook/expiry/refund-driven edges get their API-level 409 tests in 4 and 6 (they reuse this machine) |
| **F12** | `/orders` number, date, total, status, tracking link; `/orders/[id]` items, address, method, timeline in order, tracking link; error + retry | **Payment status** display → Slice 4 |

The Slice 5 final report must list 7, 29 and F12 as **partial** with these deferrals; Rob's review blocks a report that calls them done.

## 3. Decisions

| # | Decision | Reason | Alternative rejected |
|---|---|---|---|
| D1 | Build 5 before 2–4 with the §1 boundary | Owner override; everything listed as "now" needs no money, payment or stock | Stub checkout or a dev "create order" endpoint — breaks rules 1, 2, 5 |
| D2 | State machine: domain holds **all** 11 §7.3 edges as data; Slice 5 builds only the three fulfillment actions (`Paid→Processing`, `Processing→Shipped`, `Shipped→Delivered`). Reachable start for this slice = `Paid` | Test 30 needs the whole table; the other edges belong to 4 (webhook, expiry) and 6 (refund) | Build `MarkPaid`/`Cancel` now "for completeness" — no legal caller exists |
| D3 | Test orders are inserted by an integration-test fixture with parameterised SQL (`OrderSeed`); no production factory, constructor or endpoint creates an order | No production path bypasses pricing or payment; the fixture also exercises the real columns and CHECKs | Internal "rehydrate" constructor or EF seeding through internal types — production surface with no production caller |
| D4 | Override allowed set = §7.3 edges **minus** those owned by money/stock/payment paths: only `Paid→Processing` and `Shipped→Delivered`. Every other target → 409 (legal-but-reserved included) | `PendingPayment→Paid` would break rule 5; `→Cancelled` skips reservation release (rule 3); `→Refunded/Returned` moves order state without money (§8.5); `→Shipped` needs a shipment row (use `ship`) | Any §7.3 edge — lets an admin mark an unpaid order Paid |
| D5 | `audit_log` is a platform table in `Ecommerce.Persistence` (§6.2), written through `SharedKernel.Audit.IAuditLog.Add(AuditEntry)`; `EfAuditLog` only adds to the scoped context, so it saves in the caller's transaction | One audit path for Orders now and Inventory/Payments later; same transaction as the change | Orders-owned audit table (Inventory and Payments would need their own or a cross-module write) |
| D6 | Audit every admin transition (process, ship, deliver, override), not only overrides | §9 marks them "Audited"; rule 13 requires overrides | Override-only audit |
| D7 | Concurrency: `orders.version` (int) is an EF concurrency token, +1 per transition; a lost race → `ConcurrencyConflictException` → 409 | No double ship/history under parallel admin clicks; no client version needed | `SELECT … FOR UPDATE` (fine too, but optimistic matches Catalog and costs no lock) · `xmin` (§7.1 names `version`) |
| D8 | `Shipping:Carriers` options validated on start when present (unique codes `^[a-z0-9-]+$`, name 1–100, absolute `https` template containing `{trackingNumber}`); list **may be empty** (startup Warning; `ship` → 400). Dev/test config uses a reserved-domain placeholder (`https://tracking.example.com/{trackingNumber}`); real carriers are owner-supplied | Never invent providers; prod must still start | Hard-coding real carriers |
| D9 | `shipments.carrier` stores the code; `carrierName` and `trackingUrl` are built at read time from config (number URL-encoded). Code missing from config → `carrierName` = code, `trackingUrl` = null | §7.4 "built server-side"; no extra column | Snapshot the URL at ship time (schema change outside §7.1) |
| D10 | `GET /orders*` = any authenticated user, scoped to `user_id = sub`; another user's id → 403, unknown → 404, non-GUID → 404 (route constraint) | §9 "Owner", test 7 says 403 | Customer-role policy (admins have orders too) |
| D11 | Orders columns deferred: `idempotency_key`, `stripe_payment_intent_id` (Slice 4 migration), `billing_address_snapshot` (never collected in v1). No cross-module FKs (`user_id`, `product_variant_id` plain `uuid`); FKs only inside Orders | No writer this slice; extraction test §3.1.4. No row can exist before Slice 4, so 4 may add `NOT NULL UNIQUE` safely | Create the full §7.1 shape now (fixtures would invent keys) |
| D12 | Admin transition endpoints return **204** | No admin UI consumes a body | A DTO with no consumer |
| D13 | Home "best sellers" / detail "related products" (1a deferral) → **Slice 6b**, served from the projected read model (dashboard "top products") | They need Paid order data, which arrives with 4 and is aggregated by 6b projectors | Query Orders from Catalog at request time |
| D14 | `Ecommerce.Orders.Contracts` created empty | §6.1: contracts land with their first consumer | Speculative `IOrderReader` |

Rob appended D1–D3+D11, D4, D5+D6, D8+D9 and D13 to the decision log and updated the §12.3 note for D13.

## 4. Files, contracts, migration

**Backend — new projects** (add to `Ecommerce.slnx`; mirror `Ecommerce.Catalog.csproj`; no new packages — `Npgsql.EntityFrameworkCore.PostgreSQL`, `FluentValidation` already centrally pinned):
- `backend/src/Modules/Orders/Ecommerce.Orders.Contracts/` — csproj only (SharedKernel ref).
- `backend/src/Modules/Orders/Ecommerce.Orders/` — refs SharedKernel, Persistence, Orders.Contracts, Identity.Contracts (policies). `InternalsVisibleTo`: `Ecommerce.Orders.Tests`, `Ecommerce.Api.IntegrationTests`.
  - `OrdersModule.cs` — only public type; `AddOrdersModule(services, configuration)`.
  - `Domain/OrderStatus.cs` — 8 values (§7.3), stored as text.
  - `Domain/OrderStatusMachine.cs` — `IsLegal(from, to)` (11 edges), `IsOverridable(from, to)` (D4). Pure, static.
  - `Domain/Order.cs` — aggregate (private setters, private EF ctor): `Process(actorId, now)`, `Ship(actorId, carrierCode, trackingNumber, now)` (adds `Shipment`), `Deliver(actorId, now)` (sets `delivered_at`), `Override(actorId, to, reason, now)`. Each checks the machine, throws `DomainException` (code `illegal-transition`) before any mutation, appends exactly one `OrderStatusChange`, bumps `Version`, sets `UpdatedAt`. Never touches totals or items.
  - `Domain/OrderItem.cs`, `Domain/OrderStatusChange.cs` (history; `changed_by` nullable), `Domain/Shipment.cs`, `Domain/ShippingAddress.cs` (owned/jsonb value).
  - `Application/OrderQueryService.cs` — `ListAsync(userId, page, pageSize)`, `GetAsync(userId, orderId)` → `Ok | NotFound | Forbidden`.
  - `Application/OrderFulfillmentService.cs` — loads order, calls domain, writes `AuditEntry` (actor, `order.process|ship|deliver|override`, entity `order`/id, before `{status, version}`, after `{status, version[, carrier, trackingNumber]}`, reason), one `IUnitOfWork.SaveChangesAsync`. Result `Ok | NotFound | Invalid | Conflict`. Log scope with `OrderId` (§2 observability). `IClock` for every timestamp.
  - `Application/IOrderStore.cs` (write: load tracked order with shipments), `IOrderReadStore.cs` (paged list + detail projections, `AsNoTracking`), result/view records, `ShippingCarriers.cs` (lookup + `BuildTrackingUrl` with `Uri.EscapeDataString`).
  - `Infrastructure/Persistence/OrdersDbModel.cs` + configurations; `Infrastructure/OrderStore.cs`, `OrderReadStore.cs`; `Infrastructure/ShippingCarriersOptions.cs` + validator (`ValidateOnStart`).
  - `Api/OrdersController.cs` — `GET /api/v1/orders`, `GET /api/v1/orders/{id:guid}`.
  - `Api/AdminOrdersController.cs` — `[Authorize(Policy = AuthorizationPolicies.Admin)]`; `POST /api/v1/admin/orders/{id:guid}/process|ship|deliver|status-override`.
  - `Api/OrderRequests.cs`, `OrderValidators.cs`, `OrderResponses.cs`.
- **Platform audit (D5):** `SharedKernel/Audit/IAuditLog.cs` + `AuditEntry` record (no EF). `Persistence/Audit/AuditLogEntry.cs` (internal entity), `EfAuditLog.cs`; `EcommerceDbContext.OnModelCreating` configures `audit_log` after the module loop; register `IAuditLog` in `PersistenceServiceCollectionExtensions`.
- Host: `ModuleComposition` adds `OrdersModule` assembly + `.AddOrdersModule(...)`; `appsettings.Development.json` gets one placeholder carrier (D8); `appsettings.json` gets `"Shipping": { "Carriers": [] }`.

**Contracts (wire, §9 money/pagination conventions):**
- `GET /orders?page=&pageSize=` — page ≥ 1, pageSize 1–100, out of range → 400. Order `created_at DESC, id DESC`. `{ items: [{ id, orderNumber, createdAt, status, grandTotal: Money, trackingUrl|null }], page, pageSize, totalCount, totalPages }`.
- `GET /orders/{id}` — `{ id, orderNumber, createdAt, status, items: [{ variantId, sku, name, unitPrice, quantity, lineTotal }], subtotal, discountTotal, shippingTotal, taxTotal, grandTotal, shippingMethod: { code, name }, shippingAddress, timeline: [{ fromStatus|null, toStatus, at, reason|null }], shipments: [{ carrierName, trackingNumber, trackingUrl|null, shippedAt, deliveredAt|null }] }`. No `changed_by`, no `user_id`. (`paymentStatus` → 4, `refunds` → 6.)
- `ship` body `{ carrierCode, trackingNumber }` — tracking trimmed, 1–64 chars, no control chars; unknown carrier → 400. `status-override` body `{ toStatus, reason }` — `toStatus` a known `OrderStatus` name (else 400), reason trimmed 1–500 (else 400). Strict binding: unknown fields → 400.
- Codes: illegal/reserved transition or lost race → 409; unknown order → 404; anonymous → 401; Customer/Support → 403.

**Migration `AddOrdersAndAuditLog`** (expand-only):
- `orders` per §7.1 minus D11 columns: money `numeric(19,4)`, `currency char(3)`, `status text` + CHECK in the 8 values, `order_number` unique, `shipping_address_snapshot jsonb NOT NULL`, `version int NOT NULL DEFAULT 0`, `timestamptz`; CHECK all totals ≥ 0.
- `order_items` (FK orders, CHECK quantity > 0), `order_status_history` (FK, `from_status` nullable, both CHECKed), `shipments` (FK; `tracking_number varchar(64)`), `audit_log` (§7.1).
- Indexes §7.2: `orders(user_id, created_at DESC)`, `orders(status, created_at DESC)`, `order_items(order_id)`; plus `order_status_history(order_id, created_at)`, `shipments(order_id)` (FK lookups for detail).
- Drift check must pass; do not hand-edit the snapshot.

**Frontend** (`frontend/apps/store`, no new packages):
- `features/orders/types.ts`, `api.ts` (+`api.test.ts`), `status-labels.ts`, `hooks/useOrdersQuery.ts`, `hooks/useOrderQuery.ts` (+`hooks.test.tsx`), `components/OrderListView.tsx`, `OrderDetailView.tsx`, `OrderTimeline.tsx`, `TrackingLink.tsx` (+ `.test.tsx` for list, detail, tracking link).
- `lib/query-keys.ts` — `orders.list(page)`, `orders.detail(id)` (+ test).
- `app/orders/index.tsx`, `app/orders/[id].tsx` — thin; signed out → `/login` with `returnTo` (existing `return-to.ts`).
- `features/auth/components/AccountBar.tsx` — "Orders" link when signed in. `tests/routes.test.tsx` — orders rows no longer stubs.
- Money via `lib/money.ts` only; no arithmetic on the client. `TrackingLink` renders only an `https:` URL (else plain tracking number) and opens it with RN `Linking` (no `platform.ts` change).

## 5. Build order (security and invariant tests first; quote each red)

1. Projects, composition, slnx; arch tests: `SolutionLayout.ExpectedModules` + `"Orders"`, `ModuleModelOwnershipTests` asserts Orders contributes. Green before going on.
2. **Test-first (Test 30, domain):** `OrderStatusMachineTests` — all 64 pairs vs §7.3; `IsOverridable` exactly the D4 pairs. Red → implement → green.
3. Migration + `OrdersSchemaTests` (status CHECK rejects an unknown value; quantity CHECK; tables/indexes exist).
4. `OrderSeed` fixture (`Api.IntegrationTests/Orders/`): parameterised SQL insert of an order in a chosen status with items, history rows and optional shipment, for a given `user_id`.
5. **Test-first (Test 7, read):** A and B registered via the API; B's order → 403 for A; A's list excludes B's; anonymous → 401. Red → endpoints → green.
6. **Test-first (Test 30, API):** for each illegal (from, action) — e.g. `process` on `PendingPayment`/`Shipped`/`Cancelled`, `ship` on `Paid`, `deliver` on `Processing`, override to `Paid`/`Cancelled`/`Refunded`/`Shipped`/same status — 409 and history row count unchanged. Legal process → ship → deliver: 204 each, one history row each, one audit row each. Override `Paid→Processing` with reason → 204 + audit with actor, before, after, reason; without reason → 400. **Race:** two parallel `ship` → one 204, one 409, one shipment, one history row.
7. Auth/validation: 401, 403 (Customer and Support) on all four admin endpoints; 400 for unknown carrier, tracking 0/65 chars, unknown field, bad `toStatus`, page/pageSize out of range.
8. **Test 29 (partial):** list newest first + pagination edges (past last page → empty, real total); detail snapshots, address, method, timeline ascending, shipment with `trackingUrl` URL-encoded (number with a space/`/`), no actor id in JSON.
9. Unit: `ShippingCarriers` URL building and missing-code fallback; options validator (dup code, http template, missing placeholder → fails; empty list → valid).
10. Frontend F12: api + hooks tests (fetch mock), list and detail screens (loading, error with retry, empty, tracking link present only when shipped, timeline order), query keys, routes test.
11. `bash scripts/verify.sh` (full) → `slice-5-marco-report.md`.

**Mutation checks to report** (break, see red, restore): drop the `user_id` filter/ownership check (Test 7 red) · let `OrderStatusMachine.IsLegal` return true (Test 30 red) · remove the `version` concurrency token (race test red) · remove the `IAuditLog.Add` in override (audit assertion red).

## 6. Changed tests

Arch `SolutionLayout`/`ModuleModelOwnership` (a module added: strengthening) and `tests/routes.test.tsx` orders rows (stub to real screen). Each is listed and justified in the report; no other existing test changes.

## 7. Carry-ins created by this slice

- **Slice 4:** add `idempotency_key`, `stripe_payment_intent_id` columns; `paymentStatus` in detail (Test 29, F12 rest); `PendingPayment` creation, `→Paid`, `→Cancelled` via the existing `OrderStatusMachine`; Test 7 `retry-payment` ownership.
- **Slice 6:** outbox `OrderShipped` insert inside the `ship` transaction + email/SignalR; `refunds[]` in detail; `→Refunded/Returned`; `process`/`ship` 409 while a full refund is Pending; Test 7 refund part.
- **Slice 2:** reuse `IAuditLog` for stock adjust and admin bootstrap.
- **Slice 6b:** best sellers / related products (D13).

## 8. Risks and open questions

- **Feature 11 gap:** `GET /admin/orders`, `/admin/orders/{id}` and admin order screens are in §9 / app context §5 but in no §12.3 row. Admin actions are API-only until Nathaniel schedules them.
- **Test 7 "cancel":** no customer cancel endpoint in §9; Nathaniel to confirm the test wording or scope one.
- Until Slice 4 no real order exists; screens show the empty state (correct).
- `Shipping:Carriers` empty in `appsettings.json` → `ship` returns 400 until the owner supplies carriers (deploy-time value, like the email provider).
- Out-of-order slices: Slice 4's plan must re-check this schema (D11) and reuse `OrderStatusMachine` rather than add a second transition path.
