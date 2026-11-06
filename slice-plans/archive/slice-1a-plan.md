# Slice 1a — Catalog Browse: Plan

Mode: Plan · Author: Rob · Date: 2026-11-03 · Fix-round cap: 3 (CLAUDE.md)

**Verdict: READY**

---

## Summary

Slice 1a delivers public catalog browsing end to end:

- **Backend Catalog module:** Domain, Application, Infrastructure, and Api layers. One additive EF migration (`AddCatalog`) creates `categories`, `products`, `product_variants`, and `product_images`, plus a stored, generated, weighted `tsvector` column with a GIN index.
- **Three public endpoints:** `GET /api/v1/products`, `GET /api/v1/products/{slug}`, and `GET /api/v1/categories`.
- **Stock indicator:** comes through the Inventory module's public contract `IInventoryReader`. In 1a it is registered with a stub that always returns `Unknown`. Slice 2 swaps the registration.
- **Dev data:** an idempotent seeder that runs only in Development and only when config enables it. It writes through domain entities and EF, never SQL.
- **Frontend:** the Expo screens `/` (search + categories), `/products`, and `/products/[slug]`, built on TanStack Query, `lib/api-client.ts`, and `lib/query-keys.ts`.

Identity/Auth and admin create move to **Slice 1b**.

Slice 1 has no hard §11.2 invariant (tests 1–19 belong to Slices 2–6b). Its §12.3 "done" bar for the browse half is "public browse/search/paginate". The visibility rule ("inactive is invisible even by direct link", app context §4.2.3) and the page-size cap are treated as invariants here and are **test-first**.

---

## 1. Scope

**In (1a):**
- Catalog domain entities and rules.
- `AddCatalog` migration.
- Full-text search, category filter, sort, and pagination, all server-side.
- Product detail: active variants, ordered images, price, stock indicator.
- `GET /categories`.
- CORS for the configured frontend origins.
- The `IInventoryReader` contract and its `Unknown` stub.
- The dev seeder.
- `lib/api-client.ts`, `lib/query-keys.ts`, `lib/query-client.ts`, `lib/money.ts`, `lib/config.ts`, `lib/correlation-id.ts`.
- `features/catalog/**`.
- Three real screens (Home, Product list, Product detail).
- Shared `ErrorState` / `EmptyState` components.
- Doc and README updates (§11).

**Out (any of these in the diff blocks the review — quality bar §1, "two slices at once"):**
- Identity, JWT, `/auth/*`, roles, `[Authorize]`, bearer-token handling, F7.
- `/admin/*` endpoints and screens.
- Product create, update, publish, and unpublish **endpoints**. The domain `Publish()` method is in scope because the seeder and tests need it.
- Inventory tables, stock, reservations.
- Cart, the add-to-cart button, Zustand.
- Redis, caching, rate limits (Slice 7).
- The search projector or read model (6b).
- Home "best sellers" and detail "related products" (D19).
- Image upload or object-storage client.
- FluentValidation (there are no write DTOs yet).
- `lib/platform.ts`.
- Any new NuGet package.

**Slice order check:** Slice 0 PASS (slice-0-rob-review.md). Test-enforcement PASS. 1a is the next work and sits inside Slice 1.

---

## 2. Decisions made (Rob, within the docs)

| # | Decision | Reason |
|---|---|---|
| D1 | **Slice 1 is split into 1a (catalog browse) and 1b (Identity + Auth + admin product create).** Order is 1a → 1b → 2. 1b must PASS before Slice 2 starts. Recorded in architecture §12.3. | This is the owner's request (browse first). Browse has no dependency on auth: every endpoint is public (§9). Auth is large enough on its own (refresh rotation, reuse detection, lockout) that combining the two halves would produce a review too big to do well. |
| D2 | **API style: MVC controllers** (`AddControllers`, `[ApiController]`, attribute routes under `api/v1`). Controllers live in `Ecommerce.Api/Catalog/`. | §6.2 and rule 7 say "controllers". `[ApiController]` gives automatic 400 ProblemDetails on binding failure. It is part of the ASP.NET Core shared framework, so no package is added. |
| D3 | **Stock indicator via the Inventory contract.** `Ecommerce.Application/Inventory/IInventoryReader.cs` declares one batch method: `Task<IReadOnlyDictionary<Guid, StockStatus>> GetStockStatusesAsync(IReadOnlyCollection<Guid> variantIds, CancellationToken ct)`. `StockStatus` is `Unknown \| InStock \| Low \| Out`. In 1a the registration is `Ecommerce.Infrastructure/Inventory/UnknownInventoryReader` (Singleton), which returns `Unknown` for every ID. Inventory does the classification (its low-stock threshold), so Catalog never sees quantities. Catalog calls it **once per request** with every variant ID on the page. Any ID missing from the result is treated as `Unknown`. **Slice 2 lights it up** by replacing the registration with an EF reader over `inventory_stock` (available = on_hand − reserved; `Out` at ≤ 0, `Low` at ≤ threshold, default 5), with no Catalog change. Slice 7 adds the 10s Redis cache behind the same interface. | §3.2 already names `IInventoryReader` as Inventory's contract, so this adds no new concept. Rule 8: no cross-module DbSet or join, so Catalog never touches stock tables. A batch call avoids N+1. Returning `Unknown` is honest: we never claim "In stock" without data. The stub is the minimum contract, not Slice 2 work. |
| D4 | **The UI renders no indicator for `Unknown`** (no "In stock" badge). The detail screen has **no add-to-cart button in 1a** (Cart is Slice 3). | App context §4.2.3: buyable only with available stock. A button that can't work is dead UI. |
| D5 | **Product-level rollup for list cards:** take only the active variants with a known status. If any is `InStock`, the card is `InStock`. Otherwise, if any is `Low`, it is `Low`. Otherwise, if any is `Out`, it is `Out`. If none is known, it is `Unknown`. The rollup is a pure function in `Application/Catalog/StockStatusRollup.cs`. | A card needs one indicator. It should show "out" only when every variant is known to be out. |
| D6 | **Visibility rule:** a product is visible to the public iff `is_active` **and** it has at least one active variant. The list filters on it. Detail returns 404 otherwise. Only active variants are returned. | §9 and app context §4.2.3 (inactive is invisible, even by direct link). A product with no buyable variant has no price to show. |
| D7 | **List contract.** `GET /api/v1/products?search=&category=&sort=&page=&pageSize=`. `page` ≥ 1, default 1. `pageSize` 1–100, default 20. Values out of range return **400** (not clamped). `page` past the last page returns 200 with empty `items` and the real `totalCount`. Response: `{ items, page, pageSize, totalCount, totalPages }`, where `totalPages = ceil(totalCount / pageSize)` (0 when empty). | §9: `pageSize` ≤ 100, plus a total count. Rejecting instead of clamping keeps the contract explicit and testable. |
| D8 | **Sort values (wire):** `relevance`, `price-asc`, `price-desc`, `newest`. The default is `relevance` when `search` is non-empty, otherwise `newest`. An explicit `sort=relevance` without `search` returns 400. Unknown values return 400. Every sort ends in a unique tie-breaker so pages are stable: relevance = `ts_rank desc, created_at desc, id desc`; price = `min active variant price asc\|desc, id asc`; newest = `created_at desc, id desc`. The price of a product is the **minimum price among its active variants**. | §8.1 v1 sorts. Rank is meaningless without a query, so we reject rather than guess. Without a unique tie-breaker, offset pagination duplicates or drops rows. Inactive variants must not set the price. |
| D9 | **Search:** a stored, generated column `search_vector tsvector GENERATED ALWAYS AS (setweight(to_tsvector('english', coalesce(name,'')), 'A') \|\| setweight(to_tsvector('english', coalesce(description,'')), 'B')) STORED`, with a GIN index. The query is `search_vector @@ websearch_to_tsquery('english', :search)`, and rank is `ts_rank(search_vector, query)`. `search` is trimmed. Empty or whitespace means no search. More than 100 characters returns 400. The column is an EF **shadow property** (`NpgsqlTsVector`), so Domain stays free of Npgsql. | §7.2 (generated tsvector + GIN) and §13.2 (Postgres full-text). `websearch_to_tsquery` never throws on user syntax (quotes, `&`, `!`, `:*`), so input can't cause a 500. English stemming lets "shirts" match "shirt". The weights make a name match rank above a description match. |
| D10 | **The 1a search reads the system-of-record `products` table** (strongly consistent). The 6b search projector/read model replaces the query source behind `ICatalogReadStore`, and the HTTP contract stays the same. | §8.8.1 places the search read model on the eventually consistent side. That belongs to 6b and needs the outbox (Slice 4). Searching the source table now is correct, just not yet the scaled design. |
| D11 | **Category filter** takes a category **slug**. It matches that category **and its direct children** (one level in v1, §7.1). An unknown slug returns 200 with empty items, not 404. | A parent category such as "Clothing" should show its children's products. A filter that matches nothing is an empty result, not a missing resource. Stale links show the empty state instead of an error. |
| D12 | **`GET /api/v1/categories`** returns `{ items: [{ slug, name, parentSlug }] }`, ordered by `name`, **unpaginated**. It returns every category, including empty ones. | This is a bounded, one-level set used by the filter UI. Exception to §9's "lists are paginated" rule, recorded in §9. |
| D13 | **Slug rules:** `^[a-z0-9]+(?:-[a-z0-9]+)*$`, 1–100 characters, lowercase, enforced in the domain (`Domain/Catalog/Slug.cs`, `IsValid`). `products.slug` and `categories.slug` are unique. `GET /products/{slug}` with a malformed slug (including uppercase) returns **404** without a DB hit. Slugs are not case-folded. | Canonical, URL-safe identifiers. 404 (not 400) avoids leaking validation detail on a resource lookup and matches the "invisible" rule. |
| D14 | **Money on the wire:** `{ "amount": "24.50", "currency": "USD" }`. `amount` is a **string**, formatted from `decimal` with `"0.00##"` invariant culture. Prices are `numeric(19,4)` plus `currency char(3)` per variant (§7). `compareAtPrice` is the same shape, or `null`. The frontend formats only (`lib/money.ts`, `Intl.NumberFormat`, locale `en-US` in v1). Domain rules: price ≥ 0. `compareAtPrice` is null or > price. Currency matches `^[A-Z]{3}$` and is the **same for every variant of a product**. | Rule 1: the server owns money. A JSON number is parsed as a binary float in JS. A string keeps the server's decimal exactly. One currency per product makes "from" prices well defined. i18n is not a v1 feature. |
| D15 | **Images:** the API returns `url = {Catalog:Images:BaseUrl}/{storage_key}`, with each key path segment `Uri.EscapeDataString`-encoded and no double slash. `Catalog:Images:BaseUrl` is **empty** in committed config, as a placeholder for the §2 open decision (Spaces/CDN domain). When it is empty, `url` is `null` and the client renders a neutral placeholder box carrying the alt text. When it is set, it must be an absolute `http`/`https` URI, validated on start. Images are ordered by `sort_order` then `id`. List cards use the first one. | §2: never invent buckets or domains. §8.7: only keys are stored. A null URL is honest and renders safely. |
| D16 | **Frontend image component: `expo-image`** (SDK 58 version via `npx expo install expo-image`, currently `~58.0.18`, pinned exact as installed). Every image sits in a box with an explicit aspect ratio and size. Only the detail screen's main image is large. | §5.2 budgets ask for lazy images with explicit dimensions and at most one large hero. `expo-image` gives disk and memory caching on native and lazy loading on web, and ships in Expo Go. It **replaces** React Native's `Image` for product images. It is part of the Expo SDK, inside the §2 lock. |
| D17 | **Frontend server state: `@tanstack/react-query` v5** (`npx expo install @tanstack/react-query`, currently 5.105.x, pinned exact). No devtools or persister packages. | Named in §2 and §13.1. It replaces nothing: screens are stubs today. `peerDependencies` (`react ^18 \|\| ^19`) fit React 19.3. |
| D18 | **No new NuGet packages.** Npgsql EF already provides `NpgsqlTsVector`, `EF.Functions.WebSearchToTsQuery`, `Matches`, and `Rank`. CORS, controllers, and `System.Text.Json` are in the shared framework. Table and column names are mapped **explicitly** to snake_case in each `IEntityTypeConfiguration`, without `EFCore.NamingConventions`. A test checks the names. | Rule 16. Explicit mapping costs a few lines and avoids a dependency. The test catches drift. |
| D19 | **Home "best sellers" and detail "related products" are deferred** to the slice that has sales data. Rob schedules them in the Slice 5 plan; best-seller ranking needs Paid order items. Home in 1a = search box + category links + "Browse all products". | App context §4.1.1 and §4.2.4 rank by best sellers, which needs Orders (Slice 5). A "newest" stand-in would be a different feature. Still v1 scope, just a later slice. Recorded in §12.3. |
| D20 | **Dev data seeder.** `Ecommerce.Infrastructure/Catalog/DevSeed/CatalogDevSeeder.cs` (pure: `SeedAsync(EcommerceDbContext, IClock, CancellationToken)`) plus `Ecommerce.Api/Catalog/CatalogDevSeedHostedService.cs`. It runs only if `Catalog:DevSeed:Enabled = true` **and** the environment is Development. If enabled in any other environment, **startup fails fast** with a clear message. If migrations are pending, it logs a warning and skips. It is idempotent: it inserts a category, product, or variant only when its slug or SKU is missing, and never updates or deletes. It writes through domain factories and EF in one transaction. It never uses raw SQL. Data: a fixed in-code dataset of 2 parent categories with 2 children each, about 24 products (enough for 2 pages at 20), 1–3 variants each, currency `USD`, and 3 inactive products plus 1 active product with only inactive variants (to show hiding). Image keys are `dev-seed/{slug}/{n}.jpg`, so URLs are null unless a base URL is configured. `appsettings.Development.json` sets `Enabled: true`. `appsettings.json` sets `false`. | The "no direct SQL edits" rule and §8.7. The Development gate plus fail-fast makes production seeding impossible by config mistake. Integration tests do **not** use the seeder for assertions. They build their own fixed fixtures (§8), so the tests don't depend on seed content. |
| D21 | **CORS:** config `Cors:AllowedOrigins` (string array). It is empty in `appsettings.json` (production origin = open decision). It is `["http://localhost:8081"]` in Development (Expo web dev server). Policy: listed origins only, methods `GET`, request headers `X-Correlation-ID` and `Content-Type`, exposed header `X-Correlation-ID`, **no credentials**. 1b extends this for `Authorization` and POST. | §10.1: configured origins only, never `*` with credentials. Web dev is cross-origin. |
| D22 | **Frontend API base URL:** `EXPO_PUBLIC_API_BASE_URL` is read in `lib/config.ts` (static `process.env.EXPO_PUBLIC_API_BASE_URL` access, so Expo can inline it). There is no hard-coded fallback. When it is missing, `api-client` throws `ApiError` of kind `config`, and the UI shows the error state. Commit `frontend/apps/store/.env.example` with `EXPO_PUBLIC_API_BASE_URL=http://localhost:5080` (the launchSettings port, not an invented domain). Developers copy it to `.env.local`, which is gitignored. On a phone, they use their LAN IP. | §2: no invented domains. Fail-visible rather than calling a wrong host. |
| D23 | **List state lives in URL search params** (`search`, `category`, `sort`, `page`), via `useLocalSearchParams` + `router.setParams`. `pageSize` is fixed at 20 in the UI. Changing search, category, or sort resets `page` to 1. Clearing `search` while `sort=relevance` drops `sort`. Search input is debounced by 300 ms (`useDebouncedValue`, written in-house, no lodash). Queries use `placeholderData: keepPreviousData`. | §5.2: no full reload on filter change. Shareable web links. No client-side filtering (§8.1). No flicker between pages. |
| D24 | **Query client policy** (`lib/query-client.ts`): `staleTime` 30s for catalog. Retries: never on `ApiError` with 4xx or `config` kind; up to 2 retries on `network` / `server` (5xx) / `timeout`. No refetch on window focus. Request timeout is 15s (AbortController + setTimeout, not `AbortSignal.timeout`, for Hermes safety). | Retrying a 404 or 400 wastes calls and delays the error state. Transient failures retry briefly. |
| D25 | **F2 is split:** in 1a, `api-client` sends `X-Correlation-ID` (a new 32-hex ID per request from `lib/correlation-id.ts`, matching the server's `^[A-Za-z0-9._:-]{1,64}$`), maps ProblemDetails and network failures to a plain-language `ApiError`, and exposes retry through the query `refetch`. **Bearer token sending and the "never logs the token" assertion land in 1b** with auth. Recorded in §11.3. | There are no tokens in 1a. Adding a token path now would be dead code. Correlation-ID generation uses `Math.random`, which is fine for a non-security ID. Slice 4's idempotency key needs a real UUID and gets its own decision then. |
| D26 | **Response mapping location:** Application returns records with `decimal` money and storage-resolved image URLs (via `IImageUrlResolver`). The Api maps them to wire DTOs (`Api/Catalog/CatalogResponses.cs`, where money becomes a string). Enums serialize as strings (`JsonStringEnumConverter` registered globally), so `stockStatus` is `"Unknown" \| "InStock" \| "Low" \| "Out"`, matching the TS union exactly (§5.4). | Rule 7 lets controllers map responses. Wire formatting is an Api concern. |
| D27 | **`IProductReader`** (Catalog's cross-module contract, §6.1) is **not** added in 1a. It arrives with its first consumer (Cart, Slice 3). | No consumer means dead code. Catalog's own browse port is `ICatalogReadStore`, which is internal to the module. |
| D28 | **IDs** are `uuid`, generated in code as `Guid.CreateVersion7(clock.UtcNow)` (never the parameterless overload, which reads the system clock). They are passed into domain factories. | Time-ordered keys index well. Rule 9 stays clean. |
| D29 | **No caching in 1a** (no Redis, no `Cache-Control`). This is Slice 7 (§10.2). | Slice order. Correct before fast. |

**Packages added (complete list):** `@tanstack/react-query` (D17) and `expo-image` (D16). No NuGet. No dev dependencies.

---

## 3. Backend — files and contracts

Every module lives in a `Catalog/` folder in each project (§6.2). Namespaces follow the folders.

### 3.1 Domain — `backend/src/Ecommerce.Domain/`

| File | Contents |
|---|---|
| `Common/DomainException.cs` | `public sealed class DomainException(string message) : Exception(message)` |
| `Catalog/Slug.cs` | `public static class Slug { public const int MaxLength = 100; public static bool IsValid(string? value); }` (regex in D13, generated regex) |
| `Catalog/Category.cs` | `Id`, `Slug`, `Name`, `ParentId?`. `static Create(Guid id, string slug, string name, Guid? parentId)` validates the slug and requires a name of 1–100 characters. A parent cannot also be a child: **enforced in the seeder and the 1b admin service** (needs a lookup). The domain checks only `parentId != id`. |
| `Catalog/Product.cs` | `Id`, `Slug`, `Name` (1–200), `Description` (0–4000), `CategoryId`, `IsActive` (false on create), `CreatedAt`, `UpdatedAt`, `IReadOnlyList<ProductVariant> Variants`, `IReadOnlyList<ProductImage> Images` (backing fields). `static Create(Guid id, string slug, string name, string description, Guid categoryId, DateTimeOffset now)`. `AddVariant(Guid id, string sku, string name, string? size, string? colour, decimal price, string currency, decimal? compareAtPrice, int weightGrams, DateTimeOffset now)` enforces: SKU 1–64 characters `^[A-Z0-9-]+$` and unique within the product, price ≥ 0, compareAt null or > price, currency `^[A-Z]{3}$` and equal to the existing variants' currency, weight ≥ 0. `AddImage(Guid id, string storageKey, string altText, int sortOrder, DateTimeOffset now)`: key 1–512 characters, alt 0–300. `Publish(DateTimeOffset now)` requires ≥ 1 active variant. `Unpublish(DateTimeOffset now)`. Every mutation sets `UpdatedAt = now`. |
| `Catalog/ProductVariant.cs` | `Id`, `ProductId`, `Sku`, `Name`, `Size?`, `Colour?`, `Price`, `Currency`, `CompareAtPrice?`, `WeightGrams`, `IsActive` (true on create). `Deactivate()` (used by tests and the seeder for the "inactive variant" case). |
| `Catalog/ProductImage.cs` | `Id`, `ProductId`, `StorageKey`, `AltText`, `SortOrder` |

Private parameterless constructors for EF are allowed. There are no public setters.

### 3.2 Application — `backend/src/Ecommerce.Application/`

| File | Contents |
|---|---|
| `Inventory/StockStatus.cs` | `public enum StockStatus { Unknown, InStock, Low, Out }` |
| `Inventory/IInventoryReader.cs` | D3 signature. XML doc: "Display-only (§8.1). Checkout never uses this." |
| `Catalog/ProductSort.cs` | `public enum ProductSort { Relevance, PriceAsc, PriceDesc, Newest }` plus wire mapping `ProductSortNames` (`"relevance"`, `"price-asc"`, `"price-desc"`, `"newest"`). |
| `Catalog/ProductSearchCriteria.cs` | `sealed record ProductSearchCriteria(string? Search, string? CategorySlug, ProductSort Sort, int Page, int PageSize)`. `static CriteriaResult Create(string? search, string? category, string? sort, int? page, int? pageSize)` returns the criteria **or** a dictionary of field → error messages (D7, D8, D9 rules). Constants: `DefaultPageSize = 20`, `MaxPageSize = 100`, `MaxSearchLength = 100`. |
| `Catalog/ICatalogReadStore.cs` | Module-internal port: `Task<ProductPageRows> SearchAsync(ProductSearchCriteria c, CancellationToken ct)`, `Task<ProductDetailRow?> GetVisibleBySlugAsync(string slug, CancellationToken ct)`, `Task<IReadOnlyList<CategoryRow>> ListCategoriesAsync(CancellationToken ct)` |
| `Catalog/CatalogRows.cs` | Rows returned by the store: `ProductListRow(Guid Id, string Slug, string Name, CategoryRef Category, decimal Price, decimal? CompareAtPrice, string Currency, string? PrimaryImageKey, string? PrimaryImageAlt, IReadOnlyList<Guid> ActiveVariantIds)`, `ProductPageRows(IReadOnlyList<ProductListRow> Items, int TotalCount)`, `ProductDetailRow(...)` with ordered `VariantRow`s and `ImageRow`s, `CategoryRow(string Slug, string Name, string? ParentSlug)`, `CategoryRef(string Slug, string Name)` |
| `Catalog/IImageUrlResolver.cs` | `string? Resolve(string storageKey)` |
| `Catalog/CatalogResults.cs` | Results returned to the Api: `ProductListPage(IReadOnlyList<ProductListItem> Items, int Page, int PageSize, int TotalCount, int TotalPages)`, `ProductListItem(..., ImageView? PrimaryImage, StockStatus StockStatus)`, `ProductDetail(..., IReadOnlyList<ImageView> Images, IReadOnlyList<VariantView> Variants)`, `VariantView(Guid Id, string Sku, string Name, string? Size, string? Colour, decimal Price, decimal? CompareAtPrice, string Currency, StockStatus StockStatus)`, `ImageView(string? Url, string AltText)` |
| `Catalog/StockStatusRollup.cs` | `static StockStatus Rollup(IEnumerable<StockStatus>)` (D5) |
| `Catalog/CatalogBrowseService.cs` | `SearchAsync(ProductSearchCriteria, ct) → ProductListPage`. It calls `ICatalogReadStore.SearchAsync`, makes **one** `IInventoryReader` call with every `ActiveVariantIds` on the page (skipped when the page is empty), rolls up per product, resolves images, and computes `TotalPages`. `GetBySlugAsync(string slug, ct) → ProductDetail?`: if `!Slug.IsValid` it returns null with no store call. Otherwise it calls the store, makes one inventory call, and maps. `GetCategoriesAsync(ct)`. |
| `Catalog/CatalogApplicationServices.cs` (or extend an existing DI entry) | `AddCatalogApplication(this IServiceCollection)` registers `CatalogBrowseService` (Scoped). If Application has no `Microsoft.Extensions.DependencyInjection.Abstractions` reference, register the service from `Infrastructure/DependencyInjection.cs` instead. **Do not add a package for this.** |

### 3.3 Infrastructure — `backend/src/Ecommerce.Infrastructure/`

| File | Contents |
|---|---|
| `Catalog/Persistence/CategoryConfiguration.cs` | `categories`: `id uuid pk`, `slug varchar(100) unique` (`ux_categories_slug`), `name varchar(100)`, `parent_id uuid null fk → categories(id) restrict`, index `ix_categories_parent_id` |
| `Catalog/Persistence/ProductConfiguration.cs` | `products`: `id`, `slug varchar(100)` unique (`ux_products_slug`), `name varchar(200)`, `description varchar(4000)`, `category_id uuid not null fk restrict`, `is_active bool default false`, `created_at timestamptz`, `updated_at timestamptz`. Shadow `search_vector` (D9) via `HasComputedColumnSql(..., stored: true)` plus GIN `ix_products_search_vector`. Index `ix_products_category_id_is_active (category_id, is_active)` (§7.2). Partial index `ix_products_active_newest (created_at DESC, id DESC) WHERE is_active`. Navigations use backing fields. |
| `Catalog/Persistence/ProductVariantConfiguration.cs` | `product_variants`: `id`, `product_id fk restrict`, `sku varchar(64) unique (ux_product_variants_sku)`, `name varchar(200)`, `size varchar(50) null`, `colour varchar(50) null`, `price numeric(19,4)`, `currency char(3)`, `compare_at_price numeric(19,4) null`, `weight_grams int`, `is_active bool`. CHECKs: `ck_product_variants_price_nonneg (price >= 0)`, `ck_product_variants_compare_at (compare_at_price IS NULL OR compare_at_price > price)`, `ck_product_variants_currency (currency ~ '^[A-Z]{3}$')`, `ck_product_variants_weight (weight_grams >= 0)`. Index `ix_product_variants_product_id` (§7.2). |
| `Catalog/Persistence/ProductImageConfiguration.cs` | `product_images`: `id`, `product_id fk restrict`, `storage_key varchar(512)`, `alt_text varchar(300)`, `sort_order int`, index `ix_product_images_product_id_sort_order` |
| `Catalog/CatalogReadStore.cs` | `ICatalogReadStore` over `db.Set<…>()`, using `AsNoTracking`. List: (1) resolve the category slug to `{id} ∪ children ids` (one query; empty means an empty result with no further query); (2) base predicate D6 + filters; (3) `CountAsync`; (4) ordered page projection to `ProductListRow` (min active price via a correlated subquery, first image by `sort_order, id`, active variant IDs). **At most 3 SQL round-trips for the list. No per-row queries.** Detail: one query with active variants ordered `price, sku` and images ordered `sort_order, id`. Uses `EF.Functions.WebSearchToTsQuery("english", …)`, `.Matches`, `.Rank` on `EF.Property<NpgsqlTsVector>(p, "SearchVector")`. |
| `Catalog/CatalogImageOptions.cs` + `Catalog/ConfiguredImageUrlResolver.cs` | Options `Catalog:Images:BaseUrl`, validated on start (empty is OK; otherwise an absolute http/https URI). The resolver follows D15. Singleton. |
| `Catalog/DevSeed/CatalogDevSeedOptions.cs` + `CatalogDevSeeder.cs` + `CatalogDevSeedData.cs` | D20. The data file is a static in-code list. |
| `Inventory/UnknownInventoryReader.cs` | D3 stub. Comment: `// Slice 2 replaces this registration with the inventory_stock reader.` |
| `DependencyInjection.cs` | Add registrations: `ICatalogReadStore → CatalogReadStore` (Scoped), `IImageUrlResolver` (Singleton), `IInventoryReader → UnknownInventoryReader` (Singleton), the options, `CatalogDevSeeder` (Scoped), and `CatalogBrowseService` if it is not registered from Application. |
| `Persistence/Migrations/<ts>_AddCatalog.cs` | Generated by `dotnet tool run dotnet-ef migrations add AddCatalog --project backend/src/Ecommerce.Infrastructure`. **Review the generated SQL:** it must create only (tables, indexes, checks, FKs), with no alters or drops of existing objects. `Down` drops only these four tables. Commit the Designer and snapshot changes. |

### 3.4 Api — `backend/src/Ecommerce.Api/`

| File | Contents |
|---|---|
| `Catalog/ProductsController.cs` | `[ApiController] [Route("api/v1/products")]`. `GET ""` with `[FromQuery] string? search, string? category, string? sort, int? page, int? pageSize`. It calls `ProductSearchCriteria.Create`; on errors it returns `ValidationProblem(ModelStateDictionary)` (400 problem+json). Otherwise it calls the service and maps the result. `GET "{slug}"` returns the service result, or `Problem(statusCode: 404, title: "Product not found")`. **No other logic.** |
| `Catalog/CategoriesController.cs` | `GET api/v1/categories` → `{ items }` |
| `Catalog/CatalogResponses.cs` | Wire DTOs plus `MoneyResponse(string Amount, string Currency)` and the mapping extensions (D14, D26). |
| `Catalog/CatalogDevSeedHostedService.cs` | D20 gate and fail-fast. Uses `IServiceScopeFactory` to resolve `CatalogDevSeeder`. Registered only when `Catalog:DevSeed:Enabled` is true. The fail-fast check runs in `Program.cs` composition, before `Build()`, through an options validator or explicit check, so a misconfigured non-Development host never starts. |
| `Program.cs` | `AddControllers().AddJsonOptions(JsonStringEnumConverter)`, `AddCors` (D21), `app.UseCors()` (after the correlation middleware, before endpoints), `app.MapControllers()`. The existing ProblemDetails customization must also apply to `[ApiController]` 400s and 404s. **Prove it with a test.** If `DefaultProblemDetailsFactory` does not apply `CustomizeProblemDetails`, add the correlation ID via `InvalidModelStateResponseFactory` or a custom factory. Don't assume either way. |
| `appsettings.json` | Add `"Catalog": { "Images": { "BaseUrl": "" }, "DevSeed": { "Enabled": false } }` and `"Cors": { "AllowedOrigins": [] }` |
| `appsettings.Development.json` | `Catalog:DevSeed:Enabled: true`, `Cors:AllowedOrigins: ["http://localhost:8081"]` |

### 3.5 Wire shapes (exact)

```jsonc
// GET /api/v1/products?search=shirt&category=tops&sort=price-asc&page=1&pageSize=20
{ "items": [ { "id": "…", "slug": "blue-shirt", "name": "Blue Shirt",
    "category": { "slug": "tops", "name": "Tops" },
    "price": { "amount": "24.50", "currency": "USD" }, "compareAtPrice": null,
    "primaryImage": { "url": null, "altText": "Blue shirt, front" },   // or null when no images
    "stockStatus": "Unknown" } ],
  "page": 1, "pageSize": 20, "totalCount": 1, "totalPages": 1 }

// GET /api/v1/products/blue-shirt
{ "id": "…", "slug": "blue-shirt", "name": "Blue Shirt", "description": "…",
  "category": { "slug": "tops", "name": "Tops" },
  "images": [ { "url": null, "altText": "…" } ],
  "variants": [ { "id": "…", "sku": "BS-M", "name": "Medium", "size": "M", "colour": "Blue",
      "price": { "amount": "24.50", "currency": "USD" }, "compareAtPrice": null, "stockStatus": "Unknown" } ] }

// GET /api/v1/categories
{ "items": [ { "slug": "clothing", "name": "Clothing", "parentSlug": null },
             { "slug": "tops", "name": "Tops", "parentSlug": "clothing" } ] }

// 400 (application/problem+json)
{ "type": "…", "title": "One or more validation errors occurred.", "status": 400,
  "errors": { "pageSize": ["pageSize must be between 1 and 100."] }, "traceId": "…", "correlationId": "…" }
```

---

## 4. Frontend — files and contracts (`frontend/apps/store/`)

Naming follows §5.4 exactly. `app/` holds routes only. Screens stay thin: each renders one `features/catalog/components/*View`.

| File | Contract |
|---|---|
| `lib/config.ts` | `export function getApiBaseUrl(): string \| undefined` (D22; trims the trailing `/`) |
| `lib/correlation-id.ts` | `export function newCorrelationId(): string` (32 lowercase hex) |
| `lib/api-client.ts` | `export type ApiErrorKind = 'network' \| 'timeout' \| 'not-found' \| 'validation' \| 'client' \| 'server' \| 'config'`; `export class ApiError extends Error { kind; status?; title?; correlationId?; fieldErrors? }`; `export function apiGet<T>(path: string, options?: { params?: Record<string, string \| number \| undefined>; signal?: AbortSignal }): Promise<T>`. It omits undefined and empty params, adds `X-Correlation-ID` and `Accept: application/json`, applies the 15s timeout, parses `application/problem+json`, maps status to kind (404 → not-found, 400/422 → validation, other 4xx → client, 5xx → server), treats a fetch rejection as network, and handles a non-JSON body without crashing. Every error carries the response correlation ID when present. **No `console.*` calls.** `export function userMessage(error: unknown): string` returns plain-language copy (e.g. network: "Can't reach the store. Check your connection and try again."; server: "Something went wrong on our side. Please try again."; not-found: "We couldn't find that."; config: "The app isn't configured to reach the store."). |
| `lib/query-client.ts` | `export function createQueryClient(): QueryClient` (D24); `export function shouldRetry(failureCount: number, error: unknown): boolean` |
| `lib/query-keys.ts` | `export const queryKeys = { products: { all: ['products'] as const, list: (p: ProductListParams) => [...], detail: (slug: string) => [...] }, categories: { all: ['categories'] as const } }`. The list key uses the **normalized** params object, so equal filters produce an equal key. |
| `lib/money.ts` | `export function formatMoney(money: Money, locale = 'en-US'): string`. A non-numeric amount falls back to `` `${amount} ${currency}` ``. |
| `features/catalog/types.ts` | `Money`, `StockStatus = 'Unknown' \| 'InStock' \| 'Low' \| 'Out'`, `ProductSort = 'relevance' \| 'price-asc' \| 'price-desc' \| 'newest'`, `CategoryRef`, `ProductImage`, `ProductListItem`, `ProductListPage`, `ProductVariant`, `ProductDetail`, `Category`, `ProductListParams` (`search?`, `category?`, `sort?`, `page`, `pageSize`) |
| `features/catalog/list-params.ts` | `export const PRODUCT_PAGE_SIZE = 20`; `export function parseListParams(raw: Record<string, string \| string[] \| undefined>): ProductListParams`: drops an invalid page (it becomes 1), drops an unknown sort, drops `relevance` without search, trims search, and caps it at 100 characters. `export function nextListParams(current, change): ProductListParams`: a search, category, or sort change resets page to 1, and clearing search drops relevance. |
| `features/catalog/api.ts` | `fetchProducts(params, signal)`, `fetchProduct(slug, signal)`, `fetchCategories(signal)` using `apiGet` and `encodeURIComponent(slug)` |
| `features/catalog/hooks/useProductsQuery.ts` | `useQuery({ queryKey: queryKeys.products.list(p), queryFn, placeholderData: keepPreviousData })` |
| `features/catalog/hooks/useProductQuery.ts` | by slug |
| `features/catalog/hooks/useCategoriesQuery.ts` | categories |
| `features/catalog/hooks/useDebouncedValue.ts` | `useDebouncedValue<T>(value: T, delayMs: number): T` |
| `features/catalog/components/StockBadge.tsx` | `Unknown` → renders `null`; `InStock` → "In stock"; `Low` → "Low stock"; `Out` → "Out of stock". Text is always present, never colour-only. |
| `features/catalog/components/PriceText.tsx` | Price, plus the compare-at value with strikethrough and an accessible label "Was …" when present |
| `features/catalog/components/ProductImageView.tsx` | `expo-image` with an explicit `width`/`height` or `aspectRatio` box. A `null` URL renders a placeholder `View` with `accessibilityLabel = altText` (or the product name when the alt text is empty). |
| `features/catalog/components/ProductCard.tsx` | Link to `/products/{slug}`: image, name, price, category name, `StockBadge`. Accessible role `link`, named by the product name. |
| `features/catalog/components/SearchBar.tsx` | Controlled `TextInput` (`accessibilityLabel="Search products"`), a clear button, and a debounced `onSearch` |
| `features/catalog/components/CategoryFilter.tsx` | "All" plus category chips (Pressable, `accessibilityRole="button"`, `accessibilityState.selected`, min 44pt) |
| `features/catalog/components/SortPicker.tsx` | Chips for the sorts. `relevance` only when a search is present. |
| `features/catalog/components/Pagination.tsx` | "Previous" / "Next" buttons and "Page x of y". Previous is disabled on page 1, Next on the last page. Renders nothing when `totalPages ≤ 1`. |
| `features/catalog/components/VariantSelector.tsx` | One chip per variant (label: `name`, or size/colour), selected state, `onSelect(variantId)` |
| `features/catalog/components/ProductListView.tsx` | Reads and writes URL params (D23) and renders search, filter, sort, grid (`FlatList`), pagination, loading, empty ("No products match your search." plus "Clear filters"), and error (`ErrorState` with Retry → `refetch`). A page past the end shows an empty state with "Go to first page". |
| `features/catalog/components/ProductDetailView.tsx` | Props: `slug`. Shows one large main image plus small thumbnails (tap to switch the main image), name (sets `Stack.Screen` title), category, description, `VariantSelector` (defaults to the first variant), price and `StockBadge` for the **selected** variant. A 404 shows "Product not found" plus a link "Browse products" to `/products` (no retry). Other errors show `ErrorState` with Retry. No add-to-cart button (D4). |
| `features/catalog/components/HomeView.tsx` | `SearchBar` submit → `router.push({ pathname: '/products', params: { search } })`, category links → `/products?category=slug`, "Browse all products" link (D19) |
| `components/ErrorState.tsx` | `message`, `onRetry?`: an alert-role message and a "Try again" button when `onRetry` is given |
| `components/EmptyState.tsx` | `message`, optional `action` (label + onPress) |
| `components/LoadingState.tsx` | `ActivityIndicator` with `accessibilityLabel="Loading"` (static, no test needed) |
| `app/_layout.tsx` | Wrap `<Stack />` in `QueryClientProvider` with a client created once (`useState(() => createQueryClient())`) |
| `app/index.tsx`, `app/products/index.tsx`, `app/products/[slug].tsx` | Thin: render the corresponding View. Keep the names `HomeScreen`, `ProductListScreen`, `ProductDetailScreen`. **Keep the accessible headers "Home", "Products", and the product name (or "Product" while loading or not found)**, because the route test asserts them. Home **drops** the Slice 0 stub links. The route test covers every route by URL, not through Home links. |
| `.env.example` | D22 |

Remove nothing else. `ScreenStub`/`StubLink` stay; other routes still use them.

---

## 5. Migration

- A single additive migration, `AddCatalog`. It only creates objects (four tables, FKs, unique indexes, GIN, checks, partial index). It is backward-compatible (§10.5): the Slice 0 image ignores the new tables. Rollback = previous image.
- `Down` drops only the four new tables. It is never run against shared data, and agents never run it.
- After generating, Marco runs `dotnet tool run dotnet-ef migrations has-pending-model-changes --project backend/src/Ecommerce.Infrastructure` (must report no changes) and `dotnet tool run dotnet-ef migrations script --idempotent …`, and pastes the CREATE statements for `search_vector` and the GIN index into the report.

---

## 6. Tests

Test locations follow the existing projects. **TF** = test-first: write it, run it, show it failing for the right reason (paste the failure), then implement.

### 6.1 Domain — `backend/tests/Ecommerce.Domain.Tests/Catalog/`

| ID | Test | TF |
|---|---|---|
| D-1 | `Slug.IsValid`: accepts `a`, `blue-shirt`, `x1-2`; rejects empty, uppercase, leading or trailing hyphen, `--`, spaces, `_`, 101 characters | |
| D-2 | `Product.Create` is inactive with `CreatedAt == UpdatedAt == now`; an invalid slug or empty name throws `DomainException` | |
| D-3 | `AddVariant` rejects: negative price, compareAt ≤ price, bad currency, a second variant with a different currency, duplicate SKU, negative weight. Accepts compareAt null. | TF |
| D-4 | `Publish` with no active variant throws. With one it sets `IsActive` and `UpdatedAt`. `Unpublish` clears it. | TF |
| D-5 | `Category.Create` rejects an invalid slug and `parentId == id` | |

### 6.2 Application — `backend/tests/Ecommerce.Application.Tests/Catalog/` (fakes only)

| ID | Test | TF |
|---|---|---|
| A-1 | `ProductSearchCriteria.Create`: defaults (page 1, size 20, sort newest; relevance when search is present); errors for page 0, pageSize 0 and 101, unknown sort, relevance without search, search > 100 characters; whitespace search → null | TF |
| A-2 | `StockStatusRollup` (D5) truth table, including all-Unknown and mixed Unknown/Out | |
| A-3 | `CatalogBrowseService.SearchAsync` calls `IInventoryReader` **exactly once** with the union of the page's variant IDs. On an empty page it is not called. A missing ID maps to Unknown. `TotalPages` math is correct (0, exact, remainder). | TF |
| A-4 | `GetBySlugAsync` with a malformed slug returns null and **never calls the store**. A valid slug maps per-variant status. | |
| A-5 | Existing `ApplicationArchitectureTests` and `ClockRuleTests` must keep passing (no EF or Npgsql in Application, no system clock). | |

### 6.3 Integration — `backend/tests/Ecommerce.Api.IntegrationTests/Catalog/` (Testcontainers PG17, real HTTP)

Add `Catalog/CatalogFixture.cs`, a builder that inserts categories, products, variants, and images through **domain factories + `EcommerceDbContext`** into a fresh migrated database per test class, with explicit `createdAt` values. Add `FakeClock.cs` if needed. Use `ApiFactory` plus `ConfigureTestServices` to swap `IInventoryReader` for a fake where noted.

| ID | Test | TF |
|---|---|---|
| I-1 | List returns only visible products: excludes an inactive product and an active product whose only variant is inactive. `totalCount` counts visible products only. | **TF** |
| I-2 | Detail: an inactive product returns **404** problem+json. An active product with no active variants returns 404. An unknown slug returns 404. A malformed (uppercase) slug returns 404. Each 404 carries `traceId` and `correlationId`. | **TF** |
| I-3 | Detail of a visible product returns only active variants, ordered `price, sku`. Images are ordered by `sort_order`. Money is a string (`"24.50"`) with its currency. | |
| I-4 | `pageSize=101`, `pageSize=0`, `page=0`, `page=abc`, `sort=bogus`, `sort=relevance` without search, and a 101-character search each return **400** problem+json with the offending field in `errors`, plus `traceId` and `correlationId`. | **TF** |
| I-5 | Full-text: `search=shirts` matches a product named "Blue Shirt" (stemming). Matches description text. A no-match returns `items: []`, `totalCount: 0`. Hostile input (`"a & b | !c :* ('`) returns 200, never 500. | |
| I-6 | Relevance: a product with the term in its name ranks above one with the term only in its description. | |
| I-7 | Category filter by a parent slug includes the children's products. A child slug returns only that child. An unknown slug returns 200 empty. | |
| I-8 | Sorts: price-asc and price-desc use the **minimum active** variant price (a cheaper inactive variant is ignored). `newest` uses `created_at desc`. | |
| I-9 | Pagination stability: 25 products with **identical** price and `created_at`, pageSize 10. Pages 1–3 together contain all 25 exactly once for each of `newest`, `price-asc`, `price-desc`. Page 4 returns empty items with `totalCount` 25 and `totalPages` 3. | **TF** |
| I-10 | Stock: with the default registration every `stockStatus` is `"Unknown"`. With a fake `IInventoryReader`, per-variant statuses appear on detail, the list shows the D5 rollup, and the fake records **one call per request**. | |
| I-11 | Images: with `Catalog:Images:BaseUrl=https://img.test.invalid/base/`, the URL is `https://img.test.invalid/base/dev/a%20b.jpg` for key `dev/a b.jpg`. With an empty base URL, `url` is `null`. (`.invalid` is the RFC 2606 reserved TLD, not an invented domain.) | |
| I-12 | `GET /api/v1/categories` returns every category with `parentSlug`, ordered by name. | |
| I-13 | CORS: a request with `Origin` in `Cors:AllowedOrigins` gets `Access-Control-Allow-Origin` echoed and `X-Correlation-ID` exposed. A different origin gets no ACAO header. A preflight `OPTIONS` from the allowed origin succeeds for GET. | |
| I-14 | Schema: after migrating, `products.search_vector` exists and is generated; the GIN index exists; `ux_products_slug`, `ux_product_variants_sku`, `ux_categories_slug` exist; an insert violating `ck_product_variants_compare_at` through EF fails with a check violation. Plus a model-metadata test: every table and column name in the EF model matches `^[a-z][a-z0-9_]*$`. | |
| I-15 | Dev seeder: `SeedAsync` twice on a fresh DB produces identical row counts (idempotent), and the seeded inactive products are not returned by the list. Starting the API with `Catalog:DevSeed:Enabled=true` in the `Testing` environment **fails startup** with a message naming the setting. | |
| I-16 | Existing `MigrationTests` keep passing. Add an assertion that `_AddCatalog` is applied. | |

### 6.4 Frontend — Jest (`npm test -- --ci`); `fetch` mocked with `jest.spyOn(globalThis, 'fetch')` only

| ID | File | Must prove | TF |
|---|---|---|---|
| F2a | `lib/api-client.test.ts` | Sends `X-Correlation-ID` matching `^[a-f0-9]{32}$`, unique per call. Builds `base + path + query` and omits undefined and empty params. A problem+json 404 becomes `kind 'not-found'`, 400 becomes `'validation'` with `fieldErrors`, 500 becomes `'server'`, a fetch rejection becomes `'network'`, an abort after 15s (fake timers) becomes `'timeout'`, a missing base URL becomes `'config'` with no fetch call, and a non-JSON 502 body is handled. `userMessage` returns plain language for each kind. `console.*` is never called (spied). | **TF** |
| Q-1 | `lib/query-client.test.ts` | `shouldRetry`: false for not-found, validation, client, and config; true for network, server, and timeout while `failureCount < 2`, false at 2 | **TF** |
| Q-2 | `lib/query-keys.test.ts` | List keys are equal for equal normalized params and differ when any param differs. The detail key includes the slug. Every product key starts with `queryKeys.products.all` (prefix invalidation). | |
| M-1 | `lib/money.test.ts` | `{amount:"24.50",currency:"USD"}` → `"$24.50"`; `"1234.5"` → `"$1,234.50"`; a bad amount falls back | |
| C-1 | `lib/config.test.ts`, `lib/correlation-id.test.ts` | Trailing slash trimmed; undefined when unset. ID format. | |
| L-1 | `features/catalog/list-params.test.ts` | Parse and normalize rules; reset to page 1 on filter, sort, or search change; relevance dropped without search | **TF** |
| P-1 | `features/catalog/api.test.ts` | Correct paths and query strings; the slug is URI-encoded | |
| H-1 | `features/catalog/hooks/*.test.tsx` | Each query hook returns data from mocked fetch. A 404 surfaces an `ApiError` without retrying (fetch called once). The debounce hook updates only after 300ms (fake timers). | |
| V-1 | `features/catalog/components/StockBadge.test.tsx`, `PriceText.test.tsx`, `ProductImageView.test.tsx`, `ProductCard.test.tsx`, `Pagination.test.tsx`, `VariantSelector.test.tsx`, `SortPicker.test.tsx`, `CategoryFilter.test.tsx`, `SearchBar.test.tsx`, `components/ErrorState.test.tsx`, `components/EmptyState.test.tsx` | The behavior in §4 for each, queried by role or label: Unknown renders no badge; Previous/Next disabled states; selection state; relevance hidden without search; Retry calls `onRetry`; a null image URL renders the labelled placeholder; the card links to `/products/{slug}`. | |
| V-2 | `features/catalog/components/ProductListView.test.tsx` (via `renderRouter`) | Loading, then items. **The fetch URL carries `search`, `category`, `sort`, and `page`** (server-side, never client filtering). Typing a search (after debounce) updates the URL and resets page to 1. Choosing a category or sort updates the URL. Next and Previous change `page`. An empty result shows the empty state and "Clear filters" resets. A 500 shows `ErrorState`, and Retry triggers a second fetch. | |
| V-3 | `features/catalog/components/ProductDetailView.test.tsx` | Renders name, description, category, images, and variants. Selecting a second variant changes the displayed price and stock badge. A 404 shows "Product not found" with a link to `/products` and no Retry button. A 500 shows Retry. **No "Add to cart" control.** | |
| V-4 | `features/catalog/components/HomeView.test.tsx` | Search submit navigates to `/products?search=…`; a category link navigates to `/products?category=…` | |
| F1 | `tests/routes.test.tsx` (**update, do not weaken**) | Add a `built` flag to the table. Stub routes keep asserting "Not built yet." For `/`, `/products`, and `/products/blue-shirt`, mock fetch (empty list, categories, a "Blue Shirt" detail) and assert the header ("Home", "Products", "Blue Shirt") and the **absence** of "Not built yet.". The file-to-table equality check and the not-found tests stay unchanged. | |

TanStack Query in tests: build a fresh `QueryClient` per test (`retry: false` unless the test is about retries, `gcTime: Infinity`) through a helper in `tests/render-with-query.tsx`. That file is a non-test helper and is not under `app/`. The routes test uses the real `_layout` provider. Each `renderRouter` mounts a fresh `RootLayout`, which creates a fresh client, and fetch is mocked per test. If cross-test cache leakage appears, fix it with isolation. **Never add retries to hide it** (quality bar §5).

---

## 7. Build order for Marco

1. Run `AGENT_STATUS.md` → BUSY.
2. Domain TF tests (D-3, D-4) → red → domain entities → green. Then D-1, D-2, D-5.
3. Application TF tests (A-1, A-3) → red → criteria, rollup, service → green. Then A-2, A-4.
4. Infrastructure configs → `migrations add AddCatalog` → review the SQL → drift check.
5. Integration TF tests (I-1, I-2, I-4, I-9). First add the controllers and a read store with **no** visibility filter, no validation, and no tie-breaker. The TF tests must then fail **on their own assertions** (an inactive product is returned; 200 instead of 400; duplicated or missing rows across pages). A 404 from a missing route does not count as a red test. Next, implement the real filters, validation, and ordering → green. Then I-3 and I-5 through I-16.
6. `npx expo install @tanstack/react-query expo-image`. Pin exact versions in `package.json` (match the Slice 0 convention) and run `npx expo install --check`.
7. Frontend TF tests (F2a, Q-1, L-1) → red → lib and list-params → green. Then the hooks, components, views, screens, and the routes test update.
8. Docs and README (§9).
9. Run every command in §10 and paste the real output.

---

## 8. Risks

| # | Risk | Mitigation |
|---|---|---|
| R1 | EF/Npgsql translation of the min-active-price sort plus first-image projection could produce client evaluation or N+1. | EF throws on client evaluation in the final projection; keep it that way. Marco enables `LogTo` once and pastes the list SQL into the report. Review checks for ≤ 3 round-trips. |
| R2 | A `HasComputedColumnSql` shadow property with `NpgsqlTsVector` could create drift on each `migrations add`. | The drift check must report no changes after the migration. If the Npgsql fluent `HasGeneratedTsVectorColumn` is cleaner on a shadow property, Marco may use it, provided Domain still has no Npgsql reference. |
| R3 | `[ApiController]` automatic 400 and `NotFound` might not carry `correlationId`. | I-2 and I-4 assert it. Fix in `Program.cs` composition if needed. |
| R4 | `websearch_to_tsquery` on stop-word-only input ("the") yields an empty query, which returns no rows plus a Postgres NOTICE. | Acceptable: an empty result is correct. No 500. I-5's hostile input covers it. |
| R5 | No prefix matching: "shi" does not find "shirt". | Accepted v1 behavior (§13.2 search tradeoff). Revisit with `:*` prefix queries if search quality is measured as a problem. Not a 1a bug. |
| R6 | `expo-image` or TanStack Query in Jest (jest-expo) might need transforms or mocks, or might keep handles open (gc timers). | `gcTime: Infinity` in the test client, unmount after each test. If `expo-image` needs a jest mock, use jest-expo's built-in module mocks. No new mocking package. |
| R7 | `EXPO_PUBLIC_*` inlining could behave differently in Jest and Metro. | `getApiBaseUrl` reads `process.env.EXPO_PUBLIC_API_BASE_URL` at call time. Tests set and restore it. The web export builds without it set (runtime config error, which is expected). |
| R8 | The routes test now depends on mocked fetch for three routes, so other routes could leak cache. | Per-test fetch mock. Each `renderRouter` mounts a fresh `RootLayout`, so `useState(createQueryClient)` gives a fresh client per render. |
| R9 | `numColumns` changes on `FlatList` at runtime need a `key` change. | Key the list by its column count. |
| R10 | The seeder's hosted service could race with tests. | It is registered only when enabled, and tests run in `Testing` with it disabled. The seeder logic is tested directly (I-15). |
| R11 | Offset pagination degrades on deep pages. | Fine at the launch scale (§10.4). Keyset pagination is a Slice 7 slow-query item if measured. |

---

## 9. Doc and README updates

**Done by Rob in this plan:**
- Architecture §12.3: Slice 1 split into 1a/1b (D1), plus deferral notes (D19).
- §9: catalog list contract notes (D7, D8, D11, D12, D13, D14).
- §11.3: F2 split note (D25).
- §13.2: new rows (D3, D10, D14, D15/D20, D21).

**Marco:**
- `README.md` "Local development": `dotnet tool run dotnet-ef database update`, the dev seeder flag, copying `frontend/apps/store/.env.example` to `.env.local`, and the LAN IP note for Expo Go.
- If an implementation detail forces a deviation from this plan, stop and report it. Don't silently diverge.

---

## 10. Commands (Marco pastes real output; Rob re-runs them in review)

```
dotnet build Ecommerce.slnx
dotnet test Ecommerce.slnx                     # Docker running
dotnet tool run dotnet-ef migrations has-pending-model-changes --project backend/src/Ecommerce.Infrastructure
cd frontend/apps/store && npx expo install --check
cd frontend/apps/store && npx tsc --noEmit
cd frontend/apps/store && npm test -- --ci
cd frontend/apps/store && npx expo export --platform web
git grep -nE 'sk_(live|test)_|whsec_|PRIVATE KEY'      # secret scan, read-only
```

Also run a manual smoke test and paste the output:
1. `docker compose up -d`, then `dotnet tool run dotnet-ef database update --project backend/src/Ecommerce.Infrastructure`.
2. `dotnet run --project backend/src/Ecommerce.Api`. The seeder runs.
3. `curl "http://localhost:5080/api/v1/products?search=shirt&sort=price-asc&pageSize=5"` and `curl -i http://localhost:5080/api/v1/products/<an-inactive-seed-slug>` (expect 404).

---

## 11. Review gates Rob will check

- Domain and Application have no EF or Npgsql reference.
- Catalog never touches stock tables; stock only via `IInventoryReader`.
- Controllers contain only validate → service → map.
- No `DateTime.UtcNow` or parameterless `Guid.CreateVersion7()` in Domain or Application.
- The list path makes ≤ 3 SQL round-trips and one inventory call.
- The migration only creates objects.
- No price math on the client (formatting only).
- The TF tests were shown red first.
- No test is weakened: the routes test still checks every stub route.
- Only the two named npm packages are added; no NuGet is added.
- No invented domain, bucket, or origin: empty placeholders, `localhost:8081` in Development only, and `.invalid` in tests.
- Nothing from Slice 1b or Slice 2+ appears.
