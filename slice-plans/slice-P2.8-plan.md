# Slice P2.8 — Admin search API

Tracker row P2.8 (`docs/admin-dashboard-feature-tracker.md`). Arch §12.3 row "P2 Admin UI (Phase 2)". App context §4.14 ("Search") and §2 (Support: orders and customers only, never revenue). Contract: §13.2 row "Admin search UI and contract" (now with a **Server (P2.8)** part). The P2.7 UI consumes it: `frontend/apps/store/features/admin-shell/{types,api}.ts`. Do not change the frontend.

Mode: Autonomous. **Risk tier: High** (CLAUDE.md §5). New endpoint with a role policy and a per-role data filter (Support must never get products or SKUs). That is an auth rule (§3.4 rule 11), so Rob does a full review and runs the §3 mutations below. No money moves and no state changes.

## Read:

- `CLAUDE.md` (already loaded) · arch §3.4 rules 7, 8, 9, 11 (`docs/architecture/03-principles-hard-rules.md`, `grep -n "^### 3.4"`)
- §13.2 row "Admin search UI and contract": `grep -n "Admin search UI" docs/architecture/13.2-decision-log.md` (one line, the whole contract)
- `docs/architecture/09-api.md` lines 20–30 (admin endpoint table, including the new search row)
- Pattern to copy: `backend/src/Modules/Admin/Ecommerce.Admin/Api/AdminCustomersController.cs`, `.../Application/CustomerQueryService.cs`, `.../AdminModule.cs`
- ILIKE escape precedent: `backend/src/Modules/Identity/Ecommerce.Identity/Infrastructure/Repositories.cs` `SearchCustomersAsync` (lines 59–71)
- Catalog reader to sit beside: `backend/src/Modules/Catalog/Ecommerce.Catalog/Infrastructure/ProductReader.cs` · DI: `.../Catalog/Ecommerce.Catalog/CatalogModule.cs` line 43
- Orders: `backend/src/Modules/Orders/Ecommerce.Orders/Infrastructure/OrderStores.cs` `ListForAdminAsync` (lines 84–110) · DI: `.../Orders/Ecommerce.Orders/OrdersModule.cs` line 60
- Test helpers: `backend/tests/Ecommerce.Api.IntegrationTests/Admin/AdminCustomersApiTests.cs` (fixture, tokens, `OrderSeed.InsertAsync` with `number`), `.../Inventory/InventoryEnv.cs` `AddProductAsync`, `backend/tests/Ecommerce.Admin.Tests/CustomerQueryServiceTests.cs` (fake style)

Nothing else. Do not read the whole decision log.

## Decisions

| Decision | Reason | Alternative rejected |
|---|---|---|
| SKUs come from **Catalog**, not Inventory | `sku` is a Catalog column (`ux_product_variants_sku`); Inventory only gets it through `IProductReader`. "Each list from its own module's contract" means the owner. | An Inventory contract that proxies Catalog (an extra hop, and it has no consumer of its own) |
| Two new narrow contracts: `IOrderSearch` (Orders.Contracts) and `ICatalogSearch` (Catalog.Contracts) | Rule 8. One consumer each (Admin). Existing tests are not touched. | Adding methods to `IOrderReader` / `IProductReader`. Their fakes in `Admin.Tests`, `Notifications.Tests` and `Cart.Tests` would need stubs, so this slice would change unrelated tests |
| Match: case-insensitive substring, `EF.Functions.ILike(col, pattern, "\\")`, with `pattern = "%" + escape(term) + "%"`, escaping `\` first, then `%`, then `_` | Same as the Identity precedent. EF passes the pattern as a parameter, so there's no SQL injection. Without the escaping, `%` would match everything and `_` would match any character. | Full-text `search_vector` for products: it does no substring or SKU matching, and its result differs from what the user typed. Raw SQL |
| Escaping is inline in each store (a 1-line private static), not a SharedKernel helper | Two call sites, the same as the Identity precedent, and integration tests pin it. A new SharedKernel type would need its own test home. | A `SharedKernel` `LikePattern` helper. Refactoring Identity is out of scope |
| Columns: orders `order_number`; products `name`; variants `sku` | Contract groups. Searching orders by email would leak or need Identity, and the contract has no email field. | Matching by customer email or display name |
| Ordering: orders `created_at DESC, id DESC`; products `name, id`; SKUs `sku, id` | Deterministic, so `Take(5)` is stable and testable. Orders match the admin list order (§13.2 "Admin order list"). | Relevance ranking: needs pg_trgm or FTS, and there is no measured need |
| Every status, active or not, is included | The targets `/admin/orders/[id]` and `/admin/products/[id]` open drafts, inactive products and any order status. Admin is the audience. | Only active or only paid (staff would not find a draft or an expired order) |
| `q`: trim, then 2–100 chars (length after the trim). Missing, blank or out of range → 400 `ValidationProblem`, key `q` | Contract. Same shape as the customers controller. | Treating missing `q` as an empty result (the contract says 400) |
| The validation is a static `AdminSearchTerm.TryParse(string? raw, out string term, out string? error)` in Admin `Application` | Unit-testable, and the controller stays thin (rule 7) | Inline in the controller (no unit test) or a FluentValidation package (new dependency) |
| Support filter: `AdminSearchService.SearchAsync(term, includeCatalog, ct)`. The controller passes `User.IsInRole(IdentityRoles.Admin)`. When it is false, `ICatalogSearch` is **not called** and products and SKUs are `[]` | Same guard as customers `GetAsync(id, includeTotal)`: the role decides, not the client. Not calling Catalog at all also saves work. | Filtering in the controller after fetching. A second policy or endpoint |
| Reads are awaited one after another (orders, then products, then SKUs), with no `Task.WhenAll` | One scoped `EcommerceDbContext`. Running them in parallel throws `InvalidOperationException` (a second operation on the context). | Parallel reads with separate scopes (more code, no measured need) |
| Max 5 per group: `AdminSearchService.MaxPerGroup = 5`, passed as `max` to each contract, which applies `Take(max)` in SQL | Contract. The limit lives in Admin, and the contracts stay generic. | Fetching everything and trimming in memory |
| No index and no migration | ILIKE `'%x%'` can't use a btree index. It would need pg_trgm, which is a new extension. Staff-only traffic, a 300 ms client debounce, single-store table sizes. Quality bar §3 "Data" covers hot paths, and this isn't one. | GIN trigram indexes (revisit if a metric proves the need) |
| No new rate limit | Authenticated staff only, and each request costs at most 3 queries capped at `LIMIT 5`. Quality bar §3 asks for rate limits to be "considered", and this records that. | A per-user limiter policy |
| `status` is the enum name as a string (`Status.ToString()`), mapped after the query materialises | Matches the `OrderStatus` union in `types.ts` and `RecentOrder.Status` | Numeric enum |

Docs (Rob, done in this plan): §13.2 row "Admin search UI and contract" gained the **Server (P2.8)** part. `09-api.md` gained the `GET /admin/search?q=` row. Marco edits no docs except the tracker.

## Files

**New contracts (public, in `*.Contracts`)**

| File | Content |
|---|---|
| `backend/src/Modules/Orders/Ecommerce.Orders.Contracts/IOrderSearch.cs` | `public sealed record OrderSearchHit(Guid Id, string OrderNumber, string Status);` · `public interface IOrderSearch { Task<IReadOnlyList<OrderSearchHit>> SearchByNumberAsync(string term, int max, CancellationToken ct); }` Doc comment: case-insensitive substring on the order number, any status, newest first, at most `max`. |
| `backend/src/Modules/Catalog/Ecommerce.Catalog.Contracts/ICatalogSearch.cs` | `public sealed record ProductSearchHit(Guid Id, string Name);` · `public sealed record SkuSearchHit(string Sku, Guid ProductId, string ProductName);` · `public interface ICatalogSearch { Task<IReadOnlyList<ProductSearchHit>> SearchProductsAsync(string term, int max, CancellationToken ct); Task<IReadOnlyList<SkuSearchHit>> SearchSkusAsync(string term, int max, CancellationToken ct); }` Doc: active and inactive, case-insensitive substring on name or SKU. |

**Implementations (internal)**

| File | Content |
|---|---|
| `backend/src/Modules/Orders/Ecommerce.Orders/Infrastructure/OrderSearch.cs` | `internal sealed class OrderSearch(EcommerceDbContext db) : IOrderSearch`. `AsNoTracking`, `Where(ILike(o.OrderNumber, pattern, "\\"))`, `OrderByDescending(CreatedAt).ThenByDescending(Id)`, `Take(max)`. Select a private named record `(Id, OrderNumber, OrderStatus Status)`, then map to `OrderSearchHit` with `Status.ToString()` in memory. Has a private static `Contains(string term)` that builds the escaped pattern. |
| `backend/src/Modules/Catalog/Ecommerce.Catalog/Infrastructure/CatalogSearch.cs` | `internal sealed class CatalogSearch(EcommerceDbContext db) : ICatalogSearch`. Products: `db.Set<Product>()`, ILike on `Name`, order by `Name, Id`, `Take(max)`, project to `ProductSearchHit`. SKUs: variant join product **inside Catalog** (same module, allowed), ILike on `v.Sku`, order by `v.Sku, v.Id`, `Take(max)`, project to `SkuSearchHit(v.Sku, p.Id, p.Name)`. Filter and order on the entities before the constructor projection (see the comment in `ProductReader.Ordered`). Same private `Contains` helper. |
| `.../Orders/Ecommerce.Orders/OrdersModule.cs` | `services.AddScoped<IOrderSearch, OrderSearch>();` |
| `.../Catalog/Ecommerce.Catalog/CatalogModule.cs` | `services.AddScoped<ICatalogSearch, CatalogSearch>();` |

**Admin module (internal)**

| File | Content |
|---|---|
| `backend/src/Modules/Admin/Ecommerce.Admin/Application/AdminSearchTerm.cs` | `internal static class AdminSearchTerm { public const int MinLength = 2; public const int MaxLength = 100; public static bool TryParse(string? raw, out string term, out string? error); }`. Trims first. Null or blank → error "q is required." Under 2 or over 100 after the trim → error "q must be between 2 and 100 characters." |
| `backend/src/Modules/Admin/Ecommerce.Admin/Application/AdminSearchService.cs` | `internal sealed record AdminSearchView(IReadOnlyList<OrderSearchHit> Orders, IReadOnlyList<ProductSearchHit> Products, IReadOnlyList<SkuSearchHit> Skus);` · `internal sealed class AdminSearchService(IOrderSearch orders, ICatalogSearch catalog) { public const int MaxPerGroup = 5; public async Task<AdminSearchView> SearchAsync(string term, bool includeCatalog, CancellationToken ct) }`. Orders always. If `includeCatalog`, products then SKUs (awaited in sequence), otherwise `[]` and `[]`. Doc comment: Support never sees catalog results (app context §2). |
| `backend/src/Modules/Admin/Ecommerce.Admin/Api/AdminSearchController.cs` | Response records `AdminSearchOrderResponse(Guid Id, string OrderNumber, string Status)`, `AdminSearchProductResponse(Guid Id, string Name)`, `AdminSearchSkuResponse(string Sku, Guid ProductId, string ProductName)`, `AdminSearchResponse(IReadOnlyList<…> Orders, … Products, … Skus)`, all `internal sealed record`. Controller: `[ApiController] [Route("api/v1/admin/search")] [Authorize(Policy = AuthorizationPolicies.AdminOrSupport)] internal sealed class AdminSearchController(AdminSearchService search)`. `[HttpGet("")] Get([FromQuery] string? q, CancellationToken ct)`. If `TryParse` fails → `ModelState.AddModelError("q", error)` and `return ValidationProblem(ModelState)`. Otherwise call the service with `User.IsInRole(IdentityRoles.Admin)` and map. No other logic. |
| `.../Admin/Ecommerce.Admin/AdminModule.cs` | `services.AddScoped<AdminSearchService>();` |

There are no csproj changes. `Ecommerce.Admin.csproj` already references the Orders and Catalog contracts, and `InternalsVisibleTo` already covers `Ecommerce.Admin.Tests` and the integration tests.

**Tests**

| File | Content |
|---|---|
| `backend/tests/Ecommerce.Admin.Tests/AdminSearchTermTests.cs` | Unit tests U1–U3 (below) |
| `backend/tests/Ecommerce.Admin.Tests/AdminSearchServiceTests.cs` | Unit tests U4–U6, with private fakes for `IOrderSearch` and `ICatalogSearch` that record their calls |
| `backend/tests/Ecommerce.Api.IntegrationTests/Admin/AdminSearchApiTests.cs` | Integration tests I1–I10 |

**Tracker:** `docs/admin-dashboard-feature-tracker.md`. After Rob's PASS, set P2.8 → `Done` and P2.7 → `Done` (the P2.7 plan said P2.8's PASS makes it plain Done).

No new packages, migrations, frontend files or config.

## Build order

1. Contracts (`IOrderSearch`, `ICatalogSearch`) and the Admin stubs (`AdminSearchTerm`, `AdminSearchService`, and the controller with its attributes, returning an empty result) so it all compiles.
2. **Test-first, security:** write I1–I4 and I6 (401, 403, Support gets empty catalog groups, validation 400). Run them and quote the red output (for example, Support getting non-empty products from a stub that ignores the flag, and 400s missing). Then implement `AdminSearchTerm` (U1–U3) and the service's role branch (U4–U5) until green.
3. Implement `OrderSearch` and `CatalogSearch` with the escaping, and register them in DI.
4. Write I5 and I7–I10 (shape, max 5 and order, escaping, case). Make them green.
5. Run `bash scripts/verify.sh 2>&1 | tail -n 30`, then write `slice-P2.8-marco-report.md` (≤ 6 KB) with the red output from step 2 and the mutation results.

## Required tests

Use the customers fixture style: tokens for Admin, Support and Customer; orders via `OrderSeed.InsertAsync(..., number: "ORD-…")`; products and SKUs via `InventoryEnv.AddProductAsync`. **TF** = test-first.

**Unit (`Ecommerce.Admin.Tests`)**
- U1 **TF**: `TryParse` rejects null, `""`, `"   "`, `" a "` (1 char after the trim) and 101 chars. Each returns false with an error.
- U2 **TF**: accepts exactly 2 and exactly 100 chars, and `"  ab  "` → `term == "ab"`. Also accepts 100 chars padded with spaces (the length counts after the trim).
- U3: the error text names `q`.
- U4 **TF**: `includeCatalog: false` → products and SKUs are empty, the catalog fake gets **zero calls**, and orders come back from the fake.
- U5 **TF**: `includeCatalog: true` → both catalog methods are called once with the same trimmed term and `max == 5`. The orders call also gets `max == 5`.
- U6: the service returns the fakes' lists unchanged (no reordering or truncation in Admin).

**Integration (`AdminSearchApiTests`, real Postgres)**
- I1 **TF**: anonymous `GET /api/v1/admin/search?q=ord` → 401.
- I2 **TF**: Customer token → 403.
- I3 **TF**: `q` missing, `q=a`, `q=%20%20`, `q=%20a%20`, and `q=` with 101 chars → 400 problem details with an error key `q`. `q` with 100 chars → 200.
- I4 happy Admin: seed an order `ORD-2026-000777`, a product "Blue Mug" with SKU `MUG-BLU-01`, and an unrelated product and order. `q=mug` → 1 product `{id, name}` and 1 SKU `{sku, productId, productName}` with the right IDs. `q=000777` → 1 order `{id, orderNumber, status}` with the status name string (for example `"Paid"`).
- I5: inactive matches are included. A product with `productActive: false` and a variant with `variantActive: false` are both found by Admin.
- I6 **TF**, Support filter: with the same seed as I4, Support `q=mug` → 200 with `products: []` and `skus: []`. Support `q=000777` → the order is present. Also, a product named "ORD-2026-000777 Stand" with a matching SKU exists, and for Support those groups stay empty while the order is returned.
- I7, max 5 and ordering: 6 matching orders with distinct `created_at` → exactly 5, the newest first, and the oldest is missing. 6 matching products → 5 by name ascending. 6 matching SKUs → 5 by SKU ascending.
- I8, no money or email: for an Admin result with all three groups non-empty, assert the **exact property-name set** of the root (`orders, products, skus`) and of each item (`id, orderNumber, status` / `id, name` / `sku, productId, productName`). Also assert that the raw body contains none of `grandTotal`, `amount`, `price`, `email`, `customerEmail` (case-insensitive).
- I9, escaping: products "50% Off Tee" and "500 Club Tee", SKUs `A_B-1` and `AXB-1`. `q=0%` (URL-encoded) → only "50% Off Tee". `q=a_b` → only `A_B-1`. `q=%5Cz` (a backslash followed by `z`) → 200 with empty groups, and no 500 from a broken escape.
- I10, case: `q=ord-2026` finds `ORD-2026-…`. `q=MUG` finds "Blue Mug".

No existing test is changed or deleted. If one has to change, list it in the report with the reason.

## Mutations (Rob, High tier; Marco quotes red for each)

Break → red → restore from a scratch copy → `cmp` → report.

- **M1, Support filter:** in `AdminSearchController`, replace `User.IsInRole(IdentityRoles.Admin)` with `true` → I6 red.
- **M2, role policy:** change `[Authorize(Policy = AuthorizationPolicies.AdminOrSupport)]` to `[Authorize]` → I2 red.
- **M3, input guard (LIKE escaping):** in `CatalogSearch`, drop `.Replace("_", "\\_")` → I9 red.

## Risks

- **DbContext concurrency:** a `Task.WhenAll` over the three reads throws at runtime against one scoped context. The plan requires sequential awaits, and Rob will check.
- **Constructor projection before the filter:** EF can't translate `Where` or `OrderBy` after a `new Record(...)` select. Filter and order on the entities first (as `ProductReader.Ordered` does).
- **Enum `ToString()` in SQL:** map the status after materialising, or EF fails or does client evaluation.
- **Seed collisions:** order numbers have a unique index and SKUs are unique. Use distinct values per test (`OrderSeed` `number`, unique SKU prefixes per test), because the fixture shares one Postgres.
- **Contract drift:** the frontend types are the spec. I8 pins the names, so a renamed record property fails there and not in production.
- **Scan cost:** sequential ILIKE scans grow with the tables. This is accepted for now (see Decisions). It's a backlog item if admin search latency becomes a metric.
