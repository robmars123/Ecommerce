# Slice 2 — Inventory · Plan (Rob)

Mode: Autonomous · Verdict: READY · Author: Rob · 2026-11-06

**Read:** arch §2, §3.4, §6.2, §7.1 (`inventory_stock`, `inventory_reservations`, `audit_log` rows), §7.2, §7.3 (Reservation), §7.4 (`Inventory:LowStockThreshold` row), §8.2 (invariants + rules 2, 3, 7), §8.3, §8.7, §9 (endpoint table + **Admin inventory contract (Slice 2)**), §11.2 tests 1, 10, 20; §12.3 row `2`; §13.2 grep `Slice 2`. App context §4.10. Quality bar §1, §2, §4. Code: `backend/src/Modules/Inventory`, `backend/src/Modules/Catalog/Ecommerce.Catalog/{Application,Infrastructure}`, `Orders/Application/OrderFulfillmentService.cs` + `Orders/Api/AdminOrdersController.cs` (audit + actor pattern to copy), `Identity/Infrastructure/Bootstrap/AdminBootstrapHostedService.cs`, `Ecommerce.Persistence/{EfUnitOfWork,Audit/AuditLogEntry}.cs`, `Ecommerce.Api/Program.cs` (dev-seed gate), `tests/Ecommerce.Api.IntegrationTests/{ApiFactory.cs,Orders/OrdersFulfillmentApiTests.cs}`.

## 0. Run context

- **Owner override (Nathaniel, 2026-11-06):** Slices 2 → 3 → 4 → 6 run back to back, each with its own plan, build, review and ≤ 3 fix rounds, stopping at the first BLOCKED. Recorded in §13.2 ("Slices 2 → 3 → 4 → 6 back to back"). Order, gates and §3.4 are unchanged.
- **Slice 5 was built first** (orders start at `Paid`, test-seeded). Slice 2 does not touch Orders. Slice 4 will call `IInventoryReserver` inside checkout and add commit.
- **Existing Inventory module:** `IInventoryReader` + `StockStatus` (contracts, keep), `UnknownInventoryReader` + `UnknownInventoryReaderTests` (delete), `InventoryModule` (rewrite). Everything else is the delta below.
- **Separating the Slice 2 delta (review):** the working tree already holds uncommitted Slice 5 + frontend work. `slice-plans/slice-2-baseline.txt` records `git hash-object` of every dirty/untracked file before Slice 2 (148 entries). In review I re-hash: a path that is new or whose hash differs is Slice 2 delta; everything else is out of my review. Overlap files already dirty before Slice 2 that this slice edits: `backend/src/Ecommerce.Api/appsettings.json`, `appsettings.Development.json`, and docs `06`, `09`, `12`, `13.2` (Rob's edits). **Marco:** in your report, quote the exact hunks you added to the two appsettings files.

## 1. Scope

**In:** `inventory_stock` + `inventory_reservations` (migration `AddInventory`, CHECK constraints, §7.2 indexes); real `IInventoryReader` (§7.4 classification), stub deleted, Catalog emits `Out` (never `Unknown`) for a missing status; `IInventoryReserver.ReserveAsync` (row locks, one transaction); reservation expiry worker (`Active → Expired`, releases `reserved`); audited `POST /admin/inventory/adjust` and `GET /admin/inventory` (§9 contract); Catalog `IProductReader` (variant lookup + admin variant page); `Inventory:LowStockThreshold` (default 5) and `Inventory:ReservationSweepSeconds` (default 30) in config, `ValidateOnStart`; Development stock seed; carry-in: admin bootstrap writes `audit_log`.

**Out:** commit / release-on-cancel (Slice 4, first consumer), order cancellation on expiry (Slice 4, test 4), checkout, TTL config (Slice 4 passes `expiresAt`), outbox, the `/admin/inventory` screen and the admin "Out of Stock" filter (feature 9/10, no §12.3 row — open item), Redis cache of the indicator (Slice 7), any frontend change (the badge already renders `InStock`/`Low`/`Out`; `Unknown` stays in the TS type because the v1 enum is never narrowed, §9).

## 2. Decisions

| # | Decision | Reason | Rejected |
|---|---|---|---|
| D1 | `ReserveAsync` runs inside the caller's `IUnitOfWork.InTransactionAsync`; the store throws `InvalidOperationException` if no transaction is open | §8.2: reserve + order insert in one transaction (Slice 4) | Reserver opening its own transaction (would split checkout) |
| D2 | Lock with one query: `SELECT … FROM inventory_stock WHERE product_variant_id = ANY(@ids) ORDER BY product_variant_id FOR UPDATE`. Order is decided in SQL, never in C# (`Guid` and `uuid` sort differently) | Rule 3, deadlock-free | Per-row loops; sorting in C# |
| D3 | Check every line first, then change; any shortfall → `Rejected` with every short line `(variantId, requested, available)`, nothing changed. Missing stock row = available 0 | §8.3 "which item failed and why"; clean failures, not exceptions | Fail-fast on first line |
| D4 | Global lock order `orders → inventory_reservations → inventory_stock` (asc variant ID). Recorded in §13.2 | Slice 4 adds order rows to the expiry and webhook paths | Ad-hoc per path |
| D5 | Expiry worker: find ≤ 100 order IDs with `Active` and `expires_at ≤ now` (no lock), then **per order** in its own transaction: lock that order's `Active` reservations `FOR UPDATE`, re-check `expires_at ≤ now`, lock stock rows (D2), `Expire`, `reserved −= qty`. Idempotent under two instances | Slice 4 adds "lock order, cancel" into the same per-order transaction | One big batch transaction; `SKIP LOCKED` batch (harder to extend per order) |
| D6 | Adjust = `{ variantId, delta, expectedOnHand, reason }` (§9). Stale `expectedOnHand` → 409, nothing written. Would-be available < 0 → 409 + audit row `inventory.adjust.rejected` committed in its own (successful) transaction with no stock change. Success → audit `inventory.adjust` | Double submit cannot apply twice; §8.7 audited rejection | Absolute set; Idempotency-Key table (not built until Slice 4) |
| D7 | First adjust creates the row: `INSERT … ON CONFLICT (product_variant_id) DO NOTHING`, then lock (D2) | Two concurrent first adjusts never race on the PK | Insert-then-catch unique violation |
| D8 | Variant existence and admin list names via Catalog `IProductReader` (Catalog.Contracts). No FK from Inventory to `product_variants` | Rule 8; FK would need an EF-invisible migration hack | Cross-module FK or SQL |
| D9 | Publish does **not** require a stock row (1b carry-in) | No row = `Out`, unbuyable; avoids Catalog → Inventory on a write path | Publish-time check |
| D10 | Catalog fallback for a missing status is `Out`; `StockStatusRollup` returns `Out` when no variant is InStock/Low | §7.4: no row = Out, `Unknown` never emitted | Keep `Unknown` default |
| D11 | Reader returns an entry for **every** requested ID (missing row → `Out`), one `AsNoTracking` query | Test 20, no Unknown | Partial map |
| D12 | `xmin` mapped as concurrency token on `inventory_stock` (§7.1) | Belt and braces next to `FOR UPDATE` | Omit |
| D13 | Dev stock seed: inside Program.cs's existing `Catalog:DevSeed:Enabled` (Development-only) block, `AddInventoryDevSeed()` registered **after** `AddCatalogDevSeed()`. Runs only when `inventory_stock` is empty; walks `IProductReader.ListVariantsAsync` pages, index `i % 6`: 0–3 → +25, 4 → +3, 5 → no row; via the adjust service (actor null, reason "Development seed") | Web shows all three states; audited; no Catalog SQL | Raw SQL seed; a second seed flag |
| D14 | Admin bootstrap audit: one `identity.admin-bootstrap` row (actor null, entity `User`, after `{"role":"Admin"}`, no email) saved in the same `SaveChangesAsync` as the user | 1b carry-in, rule 13 | Separate save |
| D15 | Commit/release methods are **not** added to `IInventoryReserver` now | No consumer until Slice 4 (minimal code) | Full §6.1 surface now |
| D16 | `/admin/inventory` screen + "Out of Stock" filter deferred; open item for Nathaniel | Features 9/10 are outside the 1–5 push and in no §12.3 row | Building them unplanned |
| D17 | Shop card "Select options" → `/products/[slug]`, list API not widened (owner, for Slice 3). Recorded now; nothing built in Slice 2 | Owner decision | Card add-to-cart |

Decision-log rows appended by Rob: back-to-back override; Select options; reserve and lock order (D1–D5, D15); admin adjust (D6–D8); publish without stock (D9); dev stock seed (D13). Doc edits: §6.2 layout lines, §9 admin inventory contract, §12.3 Slice 2 carry-in line.

## 3. Files, contracts, migration

No new NuGet/npm package. `Ecommerce.Inventory.csproj` gains references already used elsewhere in the repo: `Ecommerce.Persistence`, `Catalog.Contracts`, `Identity.Contracts` (Admin policy), packages `Npgsql.EntityFrameworkCore.PostgreSQL` and `FluentValidation` (central versions exist), `InternalsVisibleTo Ecommerce.Api.IntegrationTests` (§6.2 allows it). No architecture-test change is expected; any change needs a reason in your report.

**Contracts**
- `Inventory.Contracts/IInventoryReserver.cs`: `Task<ReservationResult> ReserveAsync(Guid orderId, IReadOnlyList<ReservationLine> lines, DateTimeOffset expiresAt, CancellationToken ct)`. Records `ReservationLine(Guid VariantId, int Quantity)`, `ReservationShortfall(Guid VariantId, int Requested, int Available)`, `ReservationResult(bool Succeeded, IReadOnlyList<ReservationShortfall> Shortfalls)`. Empty lines, quantity ≤ 0 or a duplicate variant → `ArgumentException` (caller bug).
- `Catalog.Contracts/IProductReader.cs`: `GetVariantsAsync(IReadOnlyCollection<Guid> variantIds, ct) → IReadOnlyList<VariantRef>`; `ListVariantsAsync(int page, int pageSize, ct) → VariantRefPage(IReadOnlyList<VariantRef> Items, int TotalCount)`, every variant incl. inactive, ordered product name, SKU, id. `VariantRef(Guid VariantId, Guid ProductId, string ProductName, string Sku, string VariantName, bool IsActive)` (`IsActive` = product and variant active).

**Inventory module** (`Ecommerce.Inventory`, all `internal` except `InventoryModule`)
- `Domain/InventoryStock.cs` (`ProductVariantId`, `QuantityOnHand`, `QuantityReserved`, `Available`; `Reserve(qty)`, `Release(qty)`, `Adjust(delta)` — each throws `DomainException` if a guard (§7.1 CHECK) would break). `Domain/InventoryReservation.cs` (`Create(...)` Active; `Expire()` only from `Active`). `Domain/ReservationStatus.cs` (`Active, Committed, Released, Expired`). `Domain/StockLevel.cs` — `Classify(int available, int threshold)` per §7.4.
- `Application/InventoryPorts.cs` (`IInventoryStore`: `EnsureRowAsync`, `LockAsync(ids)`, `GetAsync(ids)` no-tracking, `FindExpiredOrderIdsAsync(now, max)`, `LockActiveReservationsAsync(orderId)`, `AddReservation`). `Application/InventoryReserver.cs` (`IInventoryReserver`). `Application/InventoryAdminService.cs` (list + adjust; results `Ok | NotFound | Stale | Insufficient`). `Application/ReservationExpiryService.cs` (`SweepAsync(ct)` → count expired).
- `Infrastructure/InventoryOptions.cs` (`LowStockThreshold` ≥ 1, `ReservationSweepSeconds` ≥ 1). `Infrastructure/InventoryStockReader.cs` (`IInventoryReader`, scoped). `Infrastructure/InventoryStore.cs` (raw-SQL locks per D2/D7, throws without a transaction). `Infrastructure/ReservationExpiryWorker.cs` (`BackgroundService`, `PeriodicTimer`, new scope per tick, logs and continues on error, never `DateTime.UtcNow`). `Infrastructure/Persistence/{InventoryDbModel,InventoryStockConfiguration,InventoryReservationConfiguration}.cs`. `Infrastructure/DevSeed/InventoryDevSeedHostedService.cs` (D13; skip with warning if migrations pending, like Catalog's).
- `Api/AdminInventoryController.cs` (`[Authorize(Policy = AuthorizationPolicies.Admin)]`, route `api/v1/admin/inventory`, actor from `sub` like `AdminOrdersController`), `Api/AdminInventoryRequests.cs` + validator, `Api/AdminInventoryResponses.cs`. Thin: validate → service → map. 409 is Problem Details with `onHand`, `reserved`, `available` extensions.
- `InventoryModule.cs`: options with `ValidateOnStart`, DbModel, store, reader, reserver, services, worker; public `AddInventoryDevSeed(this IServiceCollection)`.
- Delete `Infrastructure/UnknownInventoryReader.cs`.

**Catalog:** `Infrastructure/ProductReader.cs` (`IProductReader`, `AsNoTracking`, own tables only) registered in `CatalogModule`; `CatalogBrowseService` + `AdminProductService.PreviewAsync`: `GetValueOrDefault(…, StockStatus.Out)`; `StockStatusRollup` final return `Out` (doc comment updated).
**Identity:** `AdminBootstrapHostedService` adds the D14 audit row via `IAuditLog`.
**Api host:** `Program.cs` calls `AddInventoryDevSeed()` after `AddCatalogDevSeed()` in the existing block. `appsettings.json`: `"Inventory": { "LowStockThreshold": 5, "ReservationSweepSeconds": 30 }`. Nothing in `appsettings.Development.json` unless needed.

**Migration `AddInventory`** (additive only, rule 18):
- `inventory_stock`: `product_variant_id uuid PK`, `quantity_on_hand int`, `quantity_reserved int`, `ck_inventory_stock_quantities CHECK (quantity_on_hand - quantity_reserved >= 0 AND quantity_reserved >= 0)`; `xmin` token (system column, no DDL).
- `inventory_reservations`: `id uuid PK`, `order_id uuid`, `product_variant_id uuid` FK → `inventory_stock` (same module), `quantity int CHECK > 0`, `status varchar(20) CHECK IN ('Active','Committed','Released','Expired')`, `expires_at`, `created_at` timestamptz; unique partial `ux_inventory_reservations_order_variant_active (order_id, product_variant_id) WHERE status = 'Active'`; partial `ix_inventory_reservations_expires_at_active (expires_at) WHERE status = 'Active'`. No FK to `orders` (cross-module).
- `dotnet-ef migrations add AddInventory --project backend/src/Ecommerce.Persistence --startup-project backend/src/Ecommerce.Api`; `drift` must pass.

## 4. Build order (quote each red)

1. Migration + schema test (tables, constraint names, both indexes; a raw `UPDATE` making reserved > on_hand → `23514`).
2. **Test 1 first** (red: reserver not registered/throws) → store locks + reserver → green.
3. **Test 10 first** + endpoint auth tests (red) → `InventoryAdminService`, controller, `IProductReader.GetVariantsAsync` → green.
4. **Test 20 first** (red: `Unknown` from the stub) → reader, delete stub + its tests, Catalog `Out` fallback → green.
5. Expiry service + worker (unit, then integration).
6. `GET /admin/inventory` + `ListVariantsAsync`.
7. Dev seed, bootstrap audit, config validation.
8. Mutation checks (§5), full `bash scripts/verify.sh`, smoke (§5).

## 5. Required tests

Integration tests live in `tests/Ecommerce.Api.IntegrationTests/Inventory/` (Testcontainers Postgres; seed catalog through existing helpers or the admin API; read `audit_log` with parameterised SQL like `OrdersFulfillmentApiTests`). Unit tests in `tests/Ecommerce.Inventory.Tests/{Domain,Application}` with fakes.

**Test 1 — test-first.** 1 unit on hand. 10 parallel tasks, each its own scope: `IUnitOfWork.InTransactionAsync(ReserveAsync(newOrderId, [variant×1], now+15 min))`, released together (barrier). Assert: exactly 1 `Succeeded`, 9 `Rejected` with `(variant, 1, 0)` and **no exception**; one `Active` reservation row; final `on_hand 1, reserved 1`; a sampler or final read shows available never < 0. Plus: two variants A, B (2 units each), 20 tasks alternating lines `[A,B]` / `[B,A]` → completes within a timeout (no deadlock), reservations ≤ stock. Plus: `ReserveAsync` outside a transaction → `InvalidOperationException`. Slice 4 re-runs test 1 through `POST /checkout`.

**Test 10 — test-first.** Variant with on hand 5, reserved 3 (via `ReserveAsync`). Admin `adjust {delta:-3, expectedOnHand:5}` → 409 with `available: 2`; stock unchanged; exactly one `audit_log` row `inventory.adjust.rejected` with `actor_id` = the admin, before `{onHand:5,reserved:3}`, after carrying the attempted delta, `reason` = the sent reason. Also: success → 200 + `inventory.adjust` row (before/after); stale `expectedOnHand` → 409, no audit, no change; 401 anonymous, 403 Customer (for both endpoints); 400 for `delta 0`, `|delta| > 1,000,000`, `expectedOnHand < 0`, blank or 501-char reason; 404 unknown variant; first adjust on a variant with no row creates it; concurrent `adjust −N` vs `ReserveAsync` on the same row → available never < 0, both outcomes clean.

**Test 20 — test-first.** Real reader with threshold 5: available 6 → `InStock`, 5 → `Low`, 1 → `Low`, 0 → `Out`, on hand 3 / reserved 3 → `Out`, no row → `Out`. A reservation moves the indicator (on hand 6 → reserve 1 → `Low`). `GET /products` and `GET /products/{slug}` with mixed variants: list rollup per §7.4, no `Unknown` anywhere in either response. Unit: `StockLevel.Classify` boundaries (−1, 0, 1, threshold, threshold+1, threshold 1); reader returns one entry per distinct requested ID.

**Also required (same-slice rule):**
- Domain: `InventoryStock` guards (reserve over available, adjust below reserved, release over reserved); `InventoryReservation.Expire` only from `Active`.
- Expiry: expired `Active` → `Expired`, `reserved` decremented once; unexpired untouched; running `SweepAsync` twice (and two scopes concurrently) releases once.
- Admin list: paging 400s as the catalog list, a variant without a row shows 0/0/0 `Out`, inactive variants included, 401/403.
- `IProductReader`: both methods against Postgres (unknown ID omitted, order stable, inactive flag).
- Config: `Inventory:LowStockThreshold = 0` or `ReservationSweepSeconds = 0` → host fails to start (existing startup-test pattern).
- Bootstrap: first start writes one `identity.admin-bootstrap` row with no email in it; a second start writes none.
- Dev seed: on an empty table seeds the 25/3/none pattern with audit rows; a second run adds nothing.

**Mutation checks (report each red test):** remove `FOR UPDATE` → test 1; remove `ORDER BY` from the lock query → deadlock test (state if it is not deterministically red); remove the `expectedOnHand` check → stale test; drop the rejected-audit `Add` → test 10; remove the Admin policy → 403 test; remove the expiry re-check → double-sweep test; restore each.

**Smoke:** API on :5090 (not the owner's :5080) against the local dev DB with migrations applied and Development seed on; quote `GET /api/v1/products?pageSize=30` showing `InStock`, `Low` and `Out` and no `Unknown`. A screenshot of web is not required; the badge already has Jest coverage for all three states.

## 6. Changed and deleted tests (each justified)

- Delete `Ecommerce.Inventory.Tests/UnknownInventoryReaderTests.cs` (3) — §12.3: the stub is deleted.
- `CatalogStockAndImageTests.With_the_default_registration_every_stock_status_is_Unknown` → replace with a "never Unknown" assertion (or remove it in favour of test 20) — §7.4.
- `StockStatusRollupTests`, `CatalogBrowseServiceTests` (lines ~64, ~135), `AdminFakes` fake reader: `Unknown` fallback → `Out` — D10. Change only the expected value for missing/Unknown inputs; every other case stays.
List any other changed test in your report with its reason.

## 7. Risks

- Test 1 here proves the reserver, not checkout; Slice 4 must reuse `ReserveAsync` unchanged and repeat test 1 via HTTP.
- Expiry leaves an order `PendingPayment` once Slice 4 exists until Slice 4 adds the cancel into the D5 transaction. No `PendingPayment` order can exist before Slice 4, so nothing is exposed now.
- Admins can only adjust through the API until the `/admin/inventory` screen is scheduled.
- `13.2-decision-log.md` is ~29 KB, over the ~10 KB file rule; splitting it is a doc chore for Nathaniel to approve (it is append-only and grep-read, so no functional risk).

## 8. Open items for Nathaniel

1. **Schedule features 9 (Out of Stock filter) and 10 (`/admin/inventory` screen)** — no §12.3 row owns them (same gap as feature 11 in Slice 5).
2. **App-context edit (carried from the archived Slice 5 review — not yet pasted).** Agents do not edit that file. Verbatim:

> ### For Nathaniel: app-context edit
> You chose option (b) for item 2. Paste these into `docs/ecommerce-app-context.md`. Agents do not edit that file.
>
> **Line 55** (§4.1 Browse, item 1). Replace the whole line with:
> ```
> 1. **Shop (first screen):** `/` opens the product list; best sellers, categories and search live there.
> ```
>
> **Line 163** (§5 Screens). Replace the `/` row with:
> ```
> | `/` | Public | Redirects to `/products` (Shop) |
> ```
>
> **Line 164** (the `/products` row). Replace it with:
> ```
> | `/products` | Public | Shop (first screen): best sellers, search, filter, sort, pagination |
> ```
>
> Rob has already updated the decision log (new "Store opens on Shop" row, and the best-sellers row now points at `/products`) and the §12.3 note.
