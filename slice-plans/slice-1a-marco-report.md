# Slice 1a (Catalog browse) - Marco's build report

Author: Marco - Date: 2026-11-03 - Plan: `slice-plans/slice-1a-plan.md` (READY, D1-D29) - Fix round: 0

Nothing was committed. All changes are in the working tree.

## Concerns (for Rob)

1. **I-9 (pagination stability) is only red at a small page size.** With the plan's `pageSize=10` over 25 rows, Postgres returned a consistent order even without the tie-breaker, so the test passed on the naive store. I added a second case, `pageSize=3`, and that one fails without the tie-breaker (`Expected: 25 / Actual: 22` distinct slugs across pages). The test now runs 3 sorts x 2 page sizes. The real ORDER BY ends in `p.id` (see the SQL below).
2. **I-1 went red as a 500, not as a list assertion.** The naive store (no visibility filter) crashed on the "no active variant" product (null cheapest variant), so `List_returns_only_visible_products...` showed `Expected: OK / Actual: InternalServerError`. The leak is the same root cause (no filter), but the red reason is a crash, not a wrong list. The I-2 and I-4 reds were clean assertion failures.
3. **Category filter is a subquery in the same statement** (D11/plan 3.3 said "resolve slug to ids in one query, then filter"). I folded it into the predicate: `EXISTS (category where id = p.category_id AND (slug = X OR parent has slug X))`. Same semantics (the category and its direct children). The list is now 2 SQL round-trips (count + page, the page query carries the variant ids). An unknown slug returns 200 empty via the count = 0 early exit.
4. **List page query shape (perf, Slice 7).** EF translated "cheapest variant" and "first image" to `ROW_NUMBER() OVER (PARTITION BY product_id ...)` derived tables joined on the page rows. Postgres may compute the window over all active variants and all images before joining. Fine for the dev dataset. At real catalog size consider a lateral join or a denormalized "from price" column. Detail returns two collections (variants x images) in one statement, so rows multiply (small numbers). I did not change it; flagging for Rob and Slice 7.
5. **`npx expo install --check` is not clean, but not because of this slice.** It reports four Slice 0 packages behind the SDK patch: `expo-linking@58.0.15` (expected `~58.0.16`), `expo-router@58.0.29` (`~58.0.30`), `react-native-safe-area-context@5.10.1` (`~5.11.1`), `jest-expo@58.0.13` (`~58.0.14`). I did not upgrade them (out of plan). `expo-image` and `@tanstack/react-query` are not in the list.
6. **`expo install expo-image` edited `app.json`** (added `"expo-image"` to `plugins`). That is the tool's behaviour; I kept it. Rob may remove it if the plugin is not wanted (it is optional for web/Expo Go).
7. **Version pins.** `expo install` wrote `~58.0.18` and `^5.105.3`. I pinned both exact (`58.0.18`, `5.105.3`) to match Slice 0.
8. **CORS preflight for a disallowed method** (I-13): the framework answers with `Access-Control-Allow-Methods: GET` instead of omitting the header, so a browser refuses the POST. My test asserts that behaviour (`advertises only GET`) rather than "header absent".
9. **Seeder hosted service logs and continues** on any non-cancellation exception (dev only, so the API still starts). The fail-fast for a non-Development host is in `Program.cs` and is tested (I-15).
10. **Plan R3 resolved with no code change:** `[ApiController]` automatic 400 and `Problem(404)` both carry `traceId` and `correlationId` (the existing `CustomizeProblemDetails` applies). Asserted in I-2 and I-4 and seen in the curl output.
11. **Bug found by tests while building:** static initialisation order in `CatalogDevSeedData` (`Products = Build()` ran before the `VariantCodes` arrays were set, so `TypeInitializationException`). Caught by the I-15 test, fixed by declaring the arrays first. Never shipped in a green state.
12. **Local dev DB changed (as requested):** `AddCatalog` applied to the local Compose `ecommerce` database and the Development seeder inserted its data there (via the API, through domain entities and EF; no SQL by hand).
13. **Test helper locations:** `tests/fetch-mock.ts`, `tests/fixtures.ts`, `tests/render-with-query.tsx` (non-test helpers, outside `app/`). Hook tests live in one file, `features/catalog/hooks/hooks.test.tsx`, instead of one file per hook.

## Bugs found

- `backend/src/Ecommerce.Infrastructure/Catalog/DevSeed/CatalogDevSeedData.cs` - static init order bug - caught by `CatalogDevSeedTests.Seeding_twice_is_idempotent_and_hidden_products_are_not_listed`.
- `frontend/apps/store/components/ErrorState.tsx` - `accessibilityRole="alert"` on a non-accessible `View` was invisible to role queries (and an `accessible` wrapper would have hidden the Retry button from screen readers) - caught by `ErrorState.test.tsx`; the role now sits on the message `Text`.
- `Catalog/CatalogCorsTests` - my first assertion about the preflight response was wrong (see Concern 8); corrected the test to the real, correct framework behaviour (not weakened: it now asserts `Allow-Methods == GET`).

## Bugs fixed

All of the above, each by a test that is now passing.

## Bugs open

None.

## Test-first evidence (shown red for the right reason, then green)

- **D-3, D-4 (domain).** Naive `AddVariant` (no validation) and `Publish` (no check): `Failed! - Failed: 16, Passed: 5, Skipped: 0, Total: 21`, every failure `Assert.Throws() Failure: No exception was thrown / Expected: typeof(Ecommerce.Domain.Common.DomainException)`. After implementing: `Passed! - Failed: 0, Passed: 21`.
- **A-1 (criteria).** Naive `Create` (no validation, sort always Newest): `Expected: PriceDesc / Actual: Newest`, `Assert.False() Failure / Expected: False / Actual: True` for page=0, pageSize=0/101/-5, unknown sort, relevance without search, search > 100, collected errors; `Assert.Null() Failure: Value is not null / Actual: ""` for whitespace search. After: green.
- **A-3 (service).** Naive service (one inventory call per product) failed `Search_calls_inventory_exactly_once_with_the_union_of_the_pages_variant_ids`. After: `Passed! - Failed: 0, Passed: 48` (Application tests).
- **I-1, I-2, I-4 (integration).** Naive store (no visibility filter, no tie-breakers) and naive controller (validation bypassed): `Failed! - Failed: 14, Passed: 11, Skipped: 0, Total: 25`. Samples: `Expected: BadRequest / Actual: OK` (pageSize 0/101/-1, page 0/-3, sort=bogus, sort=relevance, 101-char search), `Expected: NotFound / Actual: OK` (inactive-shirt, no-active-variant), `Expected: OK / Actual: InternalServerError` (list, see Concern 2), `Expected: "application/problem+json" / Actual: "application/json"`.
- **I-9 (pagination).** Naive ORDER BY without tie-breaker: pageSize=10 passed (Concern 1); pageSize=3 failed for newest, price-asc, price-desc with `Expected: 25 / Actual: 22`. After restoring the real store: 28 catalog tests passed.
- **F2a, Q-1, L-1 (frontend).** Skeleton modules (raw fetch, `shouldRetry` always true, `parseListParams` returning defaults): `Test Suites: 3 failed, 3 total / Tests: 39 failed, 13 passed, 52 total`. Samples: `Expected: "http://api.test.invalid:5080/api/v1/products?search=blue%20shirt&page=2&pageSize=20" / Received: "/api/v1/products"`, `Expected constructor: ApiError / Received constructor: TypeError`, `Expected: false / Received: true` (shouldRetry), `Expected: 30000 / Received: undefined`, `Expected: "a" / Received: undefined` (parse). After: `Tests: 52 passed, 52 total`.

## Changed

### Packages
- **NuGet: none added** (central `Directory.Packages.props` unchanged).
- **npm (`frontend/apps/store/package.json`):** `@tanstack/react-query` **5.105.3** (new; brings `@tanstack/query-core` 5.105.3 transitively) and `expo-image` **58.0.18** (new). No dev dependencies. `package-lock.json` updated.

### Backend (`backend/src`)
- Domain: `Common/DomainException.cs`, `Catalog/{Slug,Category,Product,ProductVariant,ProductImage}.cs`.
- Application: `Inventory/{StockStatus,IInventoryReader}.cs`; `Catalog/{ProductSort,ProductSearchCriteria,CatalogRows,CatalogResults,ICatalogReadStore,IImageUrlResolver,StockStatusRollup,CatalogBrowseService}.cs`. (No DI file: Application has no DI abstractions reference, so `CatalogBrowseService` is registered from Infrastructure.)
- Infrastructure: `Catalog/Persistence/{Category,Product,ProductVariant,ProductImage}Configuration.cs`; `Catalog/{CatalogReadStore,CatalogImageOptions,ConfiguredImageUrlResolver}.cs`; `Catalog/DevSeed/{CatalogDevSeedOptions,CatalogDevSeedData,CatalogDevSeeder}.cs`; `Inventory/UnknownInventoryReader.cs`; `DependencyInjection.cs` (edited); `Persistence/Migrations/20261103185341_AddCatalog.cs` + `.Designer.cs` + `EcommerceDbContextModelSnapshot.cs`.
- Api: `Catalog/{ProductsController,CategoriesController,CatalogResponses,CatalogDevSeedHostedService}.cs`; `Program.cs` (controllers + string enums, CORS via options, dev-seed gate/fail-fast, `UseCors`, `MapControllers`); `appsettings.json`, `appsettings.Development.json`.

### Backend tests (`backend/tests`)
- Domain.Tests: `Catalog/{SlugTests,ProductCreateTests,CategoryTests,ProductVariantRulesTests,ProductPublishTests}.cs` (41 tests total in project).
- Application.Tests: `Catalog/{ProductSearchCriteriaTests,StockStatusRollupTests,CatalogBrowseServiceTests}.cs` (48 total).
- Api.IntegrationTests: `Catalog/{CatalogTestEnv,CatalogVisibilityTests,CatalogValidationTests,CatalogPaginationTests,CatalogDetailTests,CatalogSearchTests,CatalogStockAndImageTests,CatalogCorsTests,CatalogSchemaTests,CatalogDevSeedTests}.cs`; `MigrationTests.cs` (asserts `_AddCatalog` applied) (77 total). Fixtures use domain factories and `EcommerceDbContext`, never the seeder or SQL.

### Frontend (`frontend/apps/store`)
- `lib/`: `config`, `correlation-id`, `api-client`, `query-client`, `query-keys`, `money` (+ a `.test.ts` for each).
- `features/catalog/`: `types`, `list-params`, `api`, `hooks/{useProductsQuery,useProductQuery,useCategoriesQuery,useDebouncedValue}` (+ `hooks.test.tsx`), `components/{StockBadge,PriceText,ProductImageView,ProductCard,SearchBar,CategoryFilter,SortPicker,Pagination,VariantSelector,ProductListView,ProductDetailView,HomeView}` (+ a co-located test for each).
- `components/{ErrorState,EmptyState,LoadingState}.tsx` (+ tests for the first two; `LoadingState` is static, per plan).
- `app/_layout.tsx` (QueryClientProvider), `app/index.tsx`, `app/products/index.tsx`, `app/products/[slug].tsx` (thin; headers kept in the Views).
- `tests/routes.test.tsx` updated with a `built` flag (stub routes still assert "Not built yet."; built routes mock fetch and assert the header and no stub text; file-table equality and not-found tests untouched). New helpers: `tests/{fetch-mock.ts,fixtures.ts,render-with-query.tsx}`.
- `.env.example`, `app.json` (expo-image plugin, see Concern 6), `package.json`, `package-lock.json`.
- `README.md` (status, dev seed, `.env.local`, LAN IP note).

## Commands (real output)

```
dotnet build Ecommerce.slnx
    0 Warning(s)
    0 Error(s)

dotnet test Ecommerce.slnx
Passed!  - Failed: 0, Passed: 41, Skipped: 0, Total: 41, Duration: 33 ms - Ecommerce.Domain.Tests.dll (net10.0)
Passed!  - Failed: 0, Passed: 48, Skipped: 0, Total: 48, Duration: 56 ms - Ecommerce.Application.Tests.dll (net10.0)
Passed!  - Failed: 0, Passed: 77, Skipped: 0, Total: 77, Duration: 3 s - Ecommerce.Api.IntegrationTests.dll (net10.0)

dotnet tool run dotnet-ef migrations has-pending-model-changes --project backend/src/Ecommerce.Infrastructure
No changes have been made to the model since the last migration.

dotnet tool run dotnet-ef migrations script --idempotent ... (relevant lines; only CREATEs, no ALTER/DROP)
    search_vector tsvector GENERATED ALWAYS AS (setweight(to_tsvector('english', coalesce(name, '')), 'A') || setweight(to_tsvector('english', coalesce(description, '')), 'B')) STORED,
    CREATE INDEX ix_products_active_newest ON products (created_at DESC, id DESC) WHERE is_active;
    CREATE INDEX ix_products_search_vector ON products USING GIN (search_vector);

cd frontend/apps/store && npx expo install --check
  expo-linking@58.0.15 - expected version: ~58.0.16
  expo-router@58.0.29 - expected version: ~58.0.30
  react-native-safe-area-context@5.10.1 - expected version: ~5.11.1
  jest-expo@58.0.13 - expected version: ~58.0.14
Found outdated dependencies          <-- NOT clean; Slice 0 packages, see Concern 5

npx tsc --noEmit                       -> no output, exit 0
npm test -- --ci                       -> Test Suites: 26 passed, 26 total / Tests: 174 passed, 174 total (3 extra full runs: 174 passed each, no warnings)
npx expo export --platform web --output-dir <scratchpad>/expo-dist  -> "Exported: ...\scratchpad\expo-dist" (success)
secret scan (Grep for sk_live/sk_test/whsec_/PRIVATE KEY outside docs, build output): one hit, .github/workflows/ci.yml (pre-existing secret-scan step, false positive)
Dev DB: dotnet-ef database update --connection ... -> "Applying migration '20261103185341_AddCatalog'. Done."
```

Totals: backend 166 tests, frontend 174 tests, all passing.

### List SQL (R1; run with EF command logging, `search=shirt&category=clothing&sort=price-asc&pageSize=5`) - 2 round-trips, no per-row queries
```
-- 1. count
SELECT count(*)::int FROM products AS p
WHERE p.is_active AND EXISTS (SELECT 1 FROM product_variants AS p0 WHERE p.id = p0.product_id AND p0.is_active)
  AND EXISTS (SELECT 1 FROM categories AS c WHERE c.id = p.category_id AND (c.slug = @cat OR EXISTS (
        SELECT 1 FROM categories AS c0 WHERE c0.id = c.parent_id AND c0.slug = @cat)))
  AND p.search_vector @@ websearch_to_tsquery('english', @search)
-- 2. page (shortened): subquery s = filtered products joined to categories with min(active price) and ts_rank,
--    ORDER BY (SELECT min(p1.price) ... is_active), p.id  LIMIT @p7 OFFSET @p   <- unique tie-breaker
--    LEFT JOIN cheapest active variant  (ROW_NUMBER() OVER (PARTITION BY product_id ORDER BY price, sku) <= 1)
--    LEFT JOIN first image              (ROW_NUMBER() OVER (PARTITION BY product_id ORDER BY sort_order, id) <= 1)
--    LEFT JOIN active variant ids
--    ORDER BY s.c1, s.id, s.id0, p7.id, p9.id
```

## Smoke test (API started in Development with the seeder on, then stopped)

The log showed `Catalog dev seed complete.` and `/health/ready` returned `Healthy` / 200.

```
GET /api/v1/products?pageSize=2
{"items":[{"id":"01a1a2d4-...","slug":"blue-shirt","name":"Blue Shirt","category":{"slug":"tops","name":"Tops"},"price":{"amount":"9.99","currency":"USD"},"compareAtPrice":{"amount":"14.99","currency":"USD"},"primaryImage":{"url":null,"altText":"Blue Shirt, view 1"},"stockStatus":"Unknown"},{... "slug":"red-shirt" ... "price":{"amount":"13.49","currency":"USD"},"compareAtPrice":null ...}],"page":1,"pageSize":2,"totalCount":24,"totalPages":12}

GET ?search=shirt&sort=price-asc&pageSize=5
{'page': 1, 'pageSize': 5, 'totalCount': 3, 'totalPages': 1}
blue-shirt 9.99 / red-shirt 13.49 / linen-shirt 20.49          (all stockStatus Unknown)

GET ?category=clothing&pageSize=100  -> 12 products, categories ['bottoms','tops'] (parent includes children)
GET ?category=tops&pageSize=100      -> 6 products, ['tops']
GET ?sort=price-desc&pageSize=3      -> picture-frame 90.49, woven-basket 86.99, table-lamp 83.49
GET ?page=1 -> 20 items (total 24, pages 2); ?page=2 -> 4 items; ?page=99 -> 0 items, totalCount 24, totalPages 2

GET ?pageSize=101   -> HTTP/1.1 400, Content-Type: application/problem+json; charset=utf-8, X-Correlation-ID: db19f3...
  {"title":"One or more validation errors occurred.","status":400,"errors":{"pageSize":["pageSize must be between 1 and 100."]},"traceId":"00-6d2c...","correlationId":"db19f376c39b4bb985981efb62399147"}
GET ?page=0         -> 400 {"errors":{"page":["page must be 1 or greater."]}, ... "correlationId":"3323..."}
GET ?sort=relevance -> 400 {"errors":{"sort":["sort=relevance requires a search term."]}, ...}

GET /api/v1/products/blue-shirt -> 200 {"id":"01a1a2d4-...","slug":"blue-shirt","name":"Blue Shirt","description":"Blue Shirt. A dev-seed product used to exercise browse, search and pagination.","category":{"slug":"tops","name":"Tops"},"images":[{"url":null,"altText":"Blue Shirt, view 1"}],"variants":[{"id":"...","sku":"BLUE-SHIRT-S","name":"Small","size":"S","colour":null,"price":{"amount":"9.99","currency":"USD"},"compareAtPrice":{"amount":"14.99","currency":"USD"},"stockStatus":"Unknown"}]}
GET /api/v1/products/no-such-product -> HTTP/1.1 404, application/problem+json
  {"title":"Product not found","status":404,"traceId":"00-602b...","correlationId":"d085e91877734c9493add5391c9d35f5"}
GET /api/v1/products/retired-scarf (inactive seed) -> 404 problem+json
GET /api/v1/products/ghost-hat (active, only inactive variant) -> 404
GET /api/v1/categories -> {"items":[{"slug":"bottoms","name":"Bottoms","parentSlug":"clothing"},{"slug":"clothing","name":"Clothing","parentSlug":null},{"slug":"decor","name":"Decor","parentSlug":"home"},{"slug":"home","name":"Home","parentSlug":null},{"slug":"kitchen","name":"Kitchen","parentSlug":"home"},{"slug":"tops","name":"Tops","parentSlug":"clothing"}]}
GET ?search=a & b | !c :* ('   -> 200
CORS: Origin http://localhost:8081 -> Access-Control-Allow-Origin: http://localhost:8081, Access-Control-Expose-Headers: X-Correlation-ID
      Origin http://evil.invalid -> 200 with no Access-Control-* headers
```
API process was stopped afterwards (port 5080 no longer listening).

## Quality-bar section 2

- [x] **Module boundaries respected.** Catalog reads only its own tables (`categories`, `products`, `product_variants`, `product_images`); stock only via `IInventoryReader`. Controllers do validate -> service -> map only. Domain/Application have no EF or Npgsql reference (existing architecture tests pass).
- [x] **Every money or stock movement has a test; required tests exist and pass.** Slice 1a moves no money or stock. The plan's tests D-1..D-5, A-1..A-5, I-1..I-16 and the frontend set F1, F2a, Q-1, Q-2, M-1, C-1, L-1, P-1, H-1, V-1..V-4 exist and pass (some files grouped, see Concern 13). TF tests were shown red first (evidence above).
- [x] **Frontend co-located tests.** Every exported function/hook in `lib/` and `features/` and every component with behavior has a test next to it (`LoadingState` is static, per plan). No test under `app/`, no `.only`/`fit`/`.skip` (grep found none). F4-F8 do not belong to this slice.
- [x] **`IClock`, not `DateTime.UtcNow`.** None in Domain or Application (`ClockRuleTests` passes); ids use `Guid.CreateVersion7(clock.UtcNow)` and the seeder/service take time from `IClock`. Tests use explicit timestamps.
- [x] **Prices and totals server-side only.** Money is a server-formatted string; the client only formats (`lib/money.ts`) and does no arithmetic.
- [x] **Idempotency and outbox.** N/A with reason: Slice 1a has only read endpoints and an idempotent dev seeder; no external state change, no payment or side effect.
- [x] **Commands run with actual output pasted.** See Commands. One command is not clean: `expo install --check` (pre-existing drift, Concern 5).
- [x] **Open decisions left as placeholders.** `Catalog:Images:BaseUrl` empty; `Cors:AllowedOrigins` empty in committed config, `http://localhost:8081` in Development only; `EXPO_PUBLIC_API_BASE_URL` has no fallback (`.env.example` uses the launchSettings port); tests use `.invalid` hosts.
- [x] **Docs updated.** README updated (status, `database update` flow, dev seed flag, `.env.local`, LAN IP). Architecture sections were updated by Rob in the plan; no code contradicts them (deviation in Concern 3 is internal to the read store).
- [x] **Every change explained in plain language.** See Changed and Concerns.

AGENT_STATUS.md: Marco set to AVAILABLE at the end of this task.

## Fix round 1

- B1: `npx expo install --fix` then re-pinned to exact versions (no ^/~): @expo/metro-runtime 58.0.17, expo 58.0.23, expo-constants 58.0.15, expo-linking 58.0.16, expo-router 58.0.30, react-native-safe-area-context 5.11.1, jest-expo 58.0.14. react-native and @react-native/jest-preset both stay 0.88.1. Lockfile regenerated. `expo install --check` prints "Dependencies are up to date", exit 0.
- B2: added "blue-shirt\n" (SlugTests), "SKU-1\n" and "USD\n" (ProductVariantRulesTests). Red: 3 failed, 41 passed. Switched Slug.cs and Product.cs (x2) to `@"...\z"`. Green: 44 passed.
- B3: added `Reseeding_never_republishes_or_touches_an_existing_product`. Red: `Expected: False / Actual: True`. Seeder now publishes only when `isNew`. Green.
- B4: added `Relevance_pages_cover_all_rows_exactly_once_when_rank_and_created_at_are_identical` (pageSize 10 and 3, 25 equal products, search=cotton). With `.ThenByDescending(x => x.Id)` removed: pageSize 3 red (`Expected: 25 / Actual: 22`); pageSize 10 passed by luck, as in the earlier sorts. Restored; green.
- Non-blocking notes: none addressed (not trivial enough to justify scope).
- Results: dotnet build 0 warnings, 0 errors; dotnet test 44 + 48 + 80 passed; EF drift check clean; tsc exit 0; jest 174 passed; expo export exit 0.
