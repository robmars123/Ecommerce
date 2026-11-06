# Slice 1a — Catalog Browse: Rob's Code Review

Mode: Review · Author: Rob · Date: 2026-11-03 · Fix round reviewed: 0 (Marco's first build)

```
Mode: Review
Verdict: BLOCKED
```

## Blocking failures

| # | file:line | Issue | Fix |
|---|---|---|---|
| B1 | `frontend/apps/store/package.json:16,18,19,21,22,27,37` | `npx expo install --check` exits 1, and CI runs it (`.github/workflows/ci.yml:64`), so the frontend job would go red. When I re-ran it today it listed **7** packages, not the 4 in Marco's report, because Expo publishes new SDK 58 patches over time: `@expo/metro-runtime 58.0.16→~58.0.17`, `expo 58.0.22→~58.0.23`, `expo-constants 58.0.14→~58.0.15`, `expo-linking 58.0.15→~58.0.16`, `expo-router 58.0.29→~58.0.30`, `react-native-safe-area-context 5.10.1→~5.11.1`, `jest-expo 58.0.13→~58.0.14`. "Pre-existing" doesn't excuse it: the slice can't pass while a CI gate fails. | From `frontend/apps/store`, run `npx expo install --fix`. Re-pin every changed entry to the exact installed version (the Slice 0 convention), and commit `package-lock.json` to the working tree. Then re-run `expo install --check` (must print no outdated list and exit 0), `tsc`, `npm test -- --ci`, and `expo export`. This is **not a new package**: it is the same packages at their SDK-recommended patch versions. Recorded in §13.2. |
| B2 | `backend/src/Ecommerce.Domain/Catalog/Slug.cs:12`, `backend/src/Ecommerce.Domain/Catalog/Product.cs:119`, `Product.cs:122` | **Bug: the domain validation accepts a trailing newline.** In .NET, `$` also matches just before a final `\n`. I checked this with the .NET regex engine: `IsMatch("blue-shirt\n", "^[a-z0-9]+(?:-[a-z0-9]+)*$")` is True, and so is `"USD\n"` against `^[A-Z]{3}$` and `"SKU-1\n"` against `^[A-Z0-9-]+$`. So `Product.Create` and `Category.Create` accept the slug `"blue-shirt\n"` (breaking D13), and `AddVariant` accepts the SKU `"SKU-1\n"`, which is then stored. Only the DB CHECK, which uses POSIX `$`, catches the currency case. No endpoint writes in 1a, but 1b's admin create relies on these rules. | Use `\z` instead of `$` in all three patterns. Add cases to D-1 (`"blue-shirt\n"` is rejected) and D-3 (SKU `"SKU-1\n"` and currency `"USD\n"` are rejected). Show them red first. |
| B3 | `backend/src/Ecommerce.Infrastructure/Catalog/DevSeed/CatalogDevSeeder.cs:63` | **Bug: the seeder updates existing rows, which breaks D20 ("never updates").** `if (seed.Publish && !product.IsActive) product.Publish(...)` also runs for products that **already existed**. Once 1b lets an admin unpublish a seeded product, every Development startup re-publishes it and bumps `updated_at`. | Publish only products created in this run (track an `isNew` flag from the `TryGetValue` branch). Add a test to `CatalogDevSeedTests`: seed, unpublish one seeded product through the domain and save, seed again, and assert the product is still inactive with an unchanged `updated_at`. |
| B4 | `backend/tests/Ecommerce.Api.IntegrationTests/Catalog/CatalogPaginationTests.cs:9-15` | **Missing test:** I-9 covers `newest`, `price-asc`, and `price-desc`, but not `relevance`. Relevance is the default sort whenever a search is present, and it has its own tie-breaker chain (`CatalogReadStore.cs:71`, `rank, created_at, id`). Nothing proves that chain stays stable when ranks tie. | Add a relevance case: 25 products with an identical name and description containing a common term (so ranks are equal), identical `created_at`, `search=<term>`, and pageSize 3 and 10. Every product must appear exactly once. Show it red by removing `.ThenByDescending(x => x.Id)` temporarily. |

## Bugs found

- **B2**: a trailing newline gets past the slug, SKU, and currency validation (`$` vs `\z`). I confirmed it by running the patterns.
- **B3**: the dev seeder re-publishes existing products.

## Rulings on Marco's concerns

| # | Concern | Ruling |
|---|---|---|
| 1 | I-9 only went red at pageSize 3 | **Accepted.** Adding pageSize 3 makes the test stronger, and it is the case that proved the bug. Postgres returning a stable order at pageSize 10 is luck, not a guarantee. Keep both sizes. Relevance coverage is missing: see B4. |
| 2 | I-1 went red as a 500 | **Accepted, with a note.** The plan wanted the test to fail on its own assertion. A 500 is not that. However, I-2 showed the same visibility leak failing on a clean assertion (`Expected NotFound / Actual OK` for the inactive product and the no-active-variant product), and the I-1 assertions are simple. No re-run required. |
| 3 | Category filter is an `EXISTS` subquery, not a separate lookup | **Accepted.** It has the same semantics (the category plus its direct children), uses fewer round-trips (2 instead of 3), and I-7 covers it. It is internal to the read store. |
| 4 | `ROW_NUMBER()` window derived tables | **Accepted for 1a. Not blocking.** It is correct and not N+1. The risk is that the window computes over every active variant and image before the page join. I recorded it as a Slice 7 slow-query item in §13.2. One small cleanup is optional: `MinPrice` and `Cheapest.Price` compute the same value twice (`CatalogReadStore.cs:55` and `:58`), so the sort could use the cheapest row. |
| 5 | `expo install --check` not clean | **Blocking: B1.** CI runs it, so it is a gate whichever slice introduced the drift. The fix aligns existing packages to SDK patches, which is not a new package. |
| 6 | `expo-image` plugin added to `app.json` | **Keep it.** `expo install` adds it, and it ships inside the package (`node_modules/expo-image/app.plugin.js`). `npx expo config --type prebuild` resolves it cleanly, so EAS prebuild will run it. It adds no package and no native folder. |
| 7 | Exact pins | **Accepted.** This matches the Slice 0 convention. |
| 8 | CORS preflight advertises `Allow-Methods: GET` | **Accepted.** That is the correct framework behaviour: the browser refuses the POST because it is not in the list. The test asserts what actually protects us. |
| 9 | The seeder hosted service logs and continues | **Accepted.** It is Development only, the non-Development fail-fast sits in `Program.cs:42-52` and is tested (I-15), and the seeding logic is tested directly. Crashing local startup over seed data would hurt the developer loop with no safety gain. The error is logged with the exception at Error level. B3 is a separate issue. |
| 10 | R3: ProblemDetails on `[ApiController]` 400/404 | **Confirmed.** I-2 and I-4 assert `traceId` and `correlationId`, and they pass on my re-run. |
| 11–13 | Static init bug, dev DB migrated and seeded through the API, test-helper locations | **Accepted.** The seed ran through the domain and EF, with no SQL. The helpers are outside `app/`. Grouping the hook tests in one file is fine. |

## Gate checks (plan §11)

- **Domain/Application have no EF or Npgsql:** pass. Architecture tests are green, and the shadow `NpgsqlTsVector` property lives only in Infrastructure.
- **No cross-module DbSet or join:** pass. `CatalogReadStore` reads only `categories`, `products`, `product_variants`, and `product_images`. Stock comes only from `IInventoryReader`, which `UnknownInventoryReader` provides.
- **Thin controllers:** pass. `ProductsController` does validate, then call the service, then map. `CategoriesController` calls the service and maps.
- **`IClock`:** pass. My grep finds no `DateTime(Offset).UtcNow/Now` and no parameterless `CreateVersion7()` in `backend/src`. The seeder uses `Guid.CreateVersion7(clock.UtcNow)`.
- **Round-trips (R1):** pass. The list makes 2 SQL statements (count, then page), with no per-row queries, and it skips the page query when the page is past the end. The overflow guard on `(long)(Page-1)*PageSize` is at `CatalogReadStore.cs:38`. The detail makes 1 statement, and the inventory reader is called once per request (tested in A-3 and I-10).
- **Money:** pass. The server formats a `decimal` into `"0.00##"` invariant strings (`CatalogResponses.cs:11`). `lib/money.ts` only formats and falls back on bad input. There is no client arithmetic.
- **Migration:** pass. `AddCatalog` only creates objects (4 tables, FKs, checks, unique, GIN, and partial indexes). `Down` drops only those 4 tables. The drift check is clean.
- **TF evidence:** pass, with the notes on concerns 1 and 2. Red output is pasted for D-3, D-4, A-1, A-3, I-1, I-2, I-4, I-9, F2a, Q-1, and L-1.
- **Routes test not weakened:** pass. Every stub route still asserts "Not built yet." The file-table equality and not-found tests are unchanged.
- **Packages:** pass. Only `@tanstack/react-query` and `expo-image` were added, and no NuGet packages.
- **No invented domains:** pass. `BaseUrl` and production CORS are empty, `localhost:8081` appears in Development only, `.env.example` uses the launchSettings port, and tests use `.invalid`.
- **No 1b or Slice 2+ work:** pass. There is no auth, admin, cart, Redis, or inventory table.
- **App context §6 (Kubernetes/orchestrators out of scope):** no conflict. 1a adds no orchestration artifacts. The only container files are the Slice 0 `Dockerfile` and the local `compose.yaml`.

## Non-blocking notes

- **Domain input lengths:** `Product.AddVariant` does not validate the variant `name` (required, at most 200), `size`, or `colour` (at most 50). Over-long values reach the DB as a `DbUpdateException` instead of a `DomainException`. There is no write path in 1a, so this isn't blocking yet, but **1b must add these checks before admin create ships** (I'll put it in the 1b plan).
- **`api-client.ts:87`:** if the caller's signal is already aborted before the call, the listener never fires, so the request still goes out. TanStack passes fresh signals, so this is harmless today. A one-line `if (options.signal?.aborted)` guard would close it.
- **`api-client.ts:107`:** a timeout that fires while the body is being read surfaces as `server` ("unreadable response") instead of `timeout`. The user still sees a plain message and gets a retry.
- **`ProductListView.tsx:96-97`:** Next and Previous use `data.page` from placeholder data while the next page loads, so a fast double-tap lands on page+1 rather than page+2. This is harmless.
- **Count vs page snapshots:** the two statements run under READ COMMITTED, so a concurrent publish can skew `totalCount` by one for that one response. That is acceptable for display, and I recorded it in §13.2.

## Decisions made

- **`expo install --check` stays a blocking CI gate.** When Expo publishes patches, align with `--fix` and re-pin, which does not count as a new package. Reason: a native/SDK mismatch is invisible to Jest and the web export but breaks EAS and Expo Go. Recorded in §13.2.
- **The ROW_NUMBER list query is accepted for 1a and goes on the Slice 7 slow-query list** (lateral join or a denormalized "from price" column if measurements call for it). Recorded in §13.2.
- **The `expo-image` config plugin stays in `app.json`.** It ships with the package and resolves in prebuild.
- **The seeder may swallow errors in Development only.** The fail-fast outside Development is the safety property, and it is tested.

## Risks still open

- The Slice 7 performance of the window-function list query has not been measured.
- `expo install --check` can drift again between slices without any code change. Each slice must re-check it.

## Commands (my re-run, real output)

```
dotnet build Ecommerce.slnx
  Build succeeded. 0 Warning(s) 0 Error(s)

dotnet test Ecommerce.slnx        (Docker up; compose ecommerce-postgres-1 on 127.0.0.1:5434)
  Passed!  - Failed: 0, Passed: 41, Total: 41 - Ecommerce.Domain.Tests.dll
  Passed!  - Failed: 0, Passed: 48, Total: 48 - Ecommerce.Application.Tests.dll
  Passed!  - Failed: 0, Passed: 77, Total: 77 - Ecommerce.Api.IntegrationTests.dll

dotnet tool run dotnet-ef migrations has-pending-model-changes --project backend/src/Ecommerce.Infrastructure
  No changes have been made to the model since the last migration.

cd frontend/apps/store && npx expo install --check            <-- FAILS (B1)
  @expo/metro-runtime@58.0.16 - expected version: ~58.0.17
  expo@58.0.22 - expected version: ~58.0.23
  expo-constants@58.0.14 - expected version: ~58.0.15
  expo-linking@58.0.15 - expected version: ~58.0.16
  expo-router@58.0.29 - expected version: ~58.0.30
  react-native-safe-area-context@5.10.1 - expected version: ~5.11.1
  jest-expo@58.0.13 - expected version: ~58.0.14
  Found outdated dependencies      EXIT=1

npx tsc --noEmit                                   TSC_EXIT=0
npm test -- --ci                                   Test Suites: 26 passed, 26 total / Tests: 174 passed, 174 total
npx expo export --platform web --output-dir <scratchpad>/expo-dist   Exported (exit 0)
npx expo config --type prebuild                    plugins: ['expo-router', 'expo-image'] resolved, no error

Secret scan (CI pattern, tracked + untracked, excl. node_modules)   no matches (exit 1)
Hygiene: test files under app/                                       none
Hygiene: focused tests (.only/fit/fdescribe)                         none (exit 1)
Clock grep (DateTime(Offset).UtcNow/Now, CreateVersion7()) in backend/src   none

.NET regex probe (B2):  "blue-shirt\n" ~ ^...$ True | "USD\n" ~ ^[A-Z]{3}$ True | "SKU-1\n" ~ ^[A-Z0-9-]+$ True | "blue-shirt\n" ~ ...\z False
```

## Files reviewed

- **Backend:** `Domain/Catalog/*`, `Domain/Common/DomainException.cs`, `Application/Catalog/*`, `Application/Inventory/*`, `Infrastructure/Catalog/**` (configs, read store, image options and resolver, dev seed), `Infrastructure/Inventory/UnknownInventoryReader.cs`, `Infrastructure/DependencyInjection.cs`, the `AddCatalog` migration, `Api/Catalog/*`, `Api/Program.cs`, `appsettings*.json`, `tests/.../Catalog/CatalogPaginationTests.cs`, and `CatalogTestEnv.cs`.
- **Frontend:** `lib/*`, `features/catalog/{api,list-params}.ts`, `hooks/*`, `components/{ProductListView,ProductDetailView,HomeView,ProductImageView,SearchBar}.tsx`, `app/_layout.tsx`, `app/index.tsx`, `app/products/*`, `tests/routes.test.tsx`, `.env.example`, `package.json`, `app.json`, and `.github/workflows/ci.yml`.

## Files changed by Rob

- `docs/ecommerce-architecture.md` §13.2: two rows added (Expo SDK patch alignment; Catalog list query shape and the Slice 7 item).
- `slice-plans/slice-1a-rob-review.md` (this file).
- `AGENT_STATUS.md` (status only).

## Re-review after fix round 1

Mode: Review · Author: Rob · Date: 2026-11-03 · Fix round reviewed: 1

```
Mode: Review
Verdict: PASS
```

### Blocking items (B1–B4): all closed

| # | Verified |
|---|---|
| B1 | `package.json` has exact pins with no `^` or `~`: @expo/metro-runtime 58.0.17, expo 58.0.23, expo-constants 58.0.15, expo-linking 58.0.16, expo-router 58.0.30, react-native-safe-area-context 5.11.1, jest-expo 58.0.14. The dependency set is unchanged: the same 14 deps and 8 devDeps, and the only 1a additions are still `@tanstack/react-query` and `expo-image`. The `package-lock.json` root and installed versions match the pins. `react-native` and `@react-native/jest-preset` are both 0.88.1. `expo install --check` prints "Dependencies are up to date" and exits 0. |
| B2 | `Slug.cs:12`, `Product.cs:119`, and `Product.cs:122` use `\z`. The new cases are `SlugTests` `"blue-shirt\n"`, `ProductVariantRulesTests` `"USD\n"` (line 59), and `"SKU-1\n"` (line 85). Marco's red output shows 3 failed, then green. |
| B3 | `CatalogDevSeeder.cs:41,64` now publishes only when `isNew`. For existing products, the seeder only inserts missing variants, which matches D20 (it inserts and never updates). The new test `Reseeding_never_republishes_or_touches_an_existing_product` seeds, unpublishes, and reseeds with the clock moved forward a day. It asserts the product stays inactive and `updated_at` is unchanged. Marco's red output shows `Expected: False / Actual: True`. |
| B4 | `CatalogPaginationTests.cs:38-62` covers relevance with 25 products that have the same text and `created_at` (Epoch), `search=cotton`, and pageSize 10 and 3. It asserts 25 rows seen and 25 distinct. It went red at pageSize 3 (`Expected 25 / Actual 22`) with the id tie-breaker removed. `CatalogReadStore.cs:71` is restored to `Rank desc, CreatedAt desc, Id desc`. The other sort chains and the count/overflow guard are unchanged. |

### Scope check

These files changed after my review: `Slug.cs`, `Product.cs`, `CatalogReadStore.cs` (restored after the red run), `CatalogDevSeeder.cs`, `SlugTests.cs`, `ProductVariantRulesTests.cs`, `CatalogDevSeedTests.cs`, `CatalogPaginationTests.cs`, `package.json`, and `package-lock.json`. Nothing else in backend/, frontend/, or docs/ changed. No tests were weakened.

### Commands (my re-run, real output)

```
dotnet build Ecommerce.slnx            Build succeeded. 0 Warning(s) 0 Error(s)
dotnet test Ecommerce.slnx             Domain 44/44 · Application 48/48 · Api.IntegrationTests 80/80 (Failed: 0)
dotnet-ef has-pending-model-changes    No changes have been made to the model since the last migration.
npx expo install --check               Dependencies are up to date (exit 0)
npx tsc --noEmit                       exit 0
npm test -- --ci                       Test Suites: 26 passed, 26 total · Tests: 174 passed, 174 total
npx expo export --platform web         Exported to scratchpad/expo-dist-r1 (exit 0)
Secret scan (CI pattern, excl. node_modules/bin/obj/dist/.git)    no matches (exit 1)
Test files under app/                  none
Focused tests                          none (exit 1)
Clock grep in backend/src              none (exit 1)
```

### Non-blocking notes (carried forward, unchanged)

- 1b must validate the variant name, size, and colour lengths in the domain before admin create ships.
- `api-client.ts:87` needs a pre-aborted signal guard. `api-client.ts:107` reports a timeout during the body read as a server error. `ProductListView.tsx:96-97` has a double-tap issue on placeholder data. The count and page queries can see different snapshots.

### Decisions made

None new.

### Risks still open

- Slice 7 has not yet measured the window-function list query.
- `expo install --check` can drift again as Expo publishes SDK 58 patches. Re-check it every slice.
