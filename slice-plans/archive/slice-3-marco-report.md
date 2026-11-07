# Slice 3 - Cart - Marco report

Concerns:
1. **My smoke API (PID 51052, :5090, DB `ecommerce_smoke`) is still running.** The permission layer refused my kill. It locks the Release DLLs, so I ran the gate with `VERIFY_CONFIGURATION=Debug` (not killing to free a lock). Please stop that one process, then re-run the Release gate.
2. Replay stores the application-level merge result as JSON and re-maps it, so the HTTP bytes are identical (smoke: 462 = 462 bytes).
3. `MergeResultNotice` sits in the Stack header under `TopNav`, so it shows on every screen except `/admin`. Retry is passed through the result store.
4. A rejected add/set can leave an empty `carts` row (cart lines unchanged). GET never creates one.
5. `ApiError.available` and `apiSend` DELETE + `headers` were added (needed for the 409 text and the key).
6. The admin product preview gets `hideAddToCart` (it shares `ProductDetailContent`).

Bugs found: concurrency test passed with the cart lock removed (creating the cart serialised it). Fixed the test, see M1.
Bugs fixed: that test. Bugs open: None

Packages (`npx expo install`): zustand 5.1.0, @react-native-async-storage/async-storage 2.2.0, expo-crypto 58.0.7. No others.

## Red outputs (test-first)
- **T22 / T21** (module had a migration but no endpoints): `Failed: 28, Passed: 0`; `Expected: OK / Actual: NotFound`, `Expected: Unauthorized / Actual: NotFound`. Green: 28/28.
- **F9 store** (stub threw): `Error: not implemented`, 6 failed. **F9 merge hook** (stub did nothing): `Failed: 5, Passed: 1`. Green after implementing: 17/17 in `features/cart`.

## Mutation checks (each restored)
1. Cart row `FOR UPDATE` removed: RED, `Concurrent_adds_of_the_same_variant...` (first version of the test stayed green, so I made it add to an existing cart).
2. Idempotency lookup removed: RED x2 (same key twice; parallel same key).
3. Hash comparison removed: RED, different body or user is 409.
4. "Never lower" branch broken: RED x2 (server line kept; no-stock line).
5. `[Authorize]` to `[AllowAnonymous]`: RED x2 (401 tests).
6. Edit no longer nulls `mergeKey`: RED, store test.
7. Clear lines on failure: RED x3 (hook tests).

## Smoke (:5090, `ecommerce_smoke`, migration `AddCart` applied)
add 2 -> subtotal 19.98; PUT 5 -> 49.95; PUT 99 -> 400; DELETE -> 204, cart empty; anonymous GET -> 401. Merge key smoke-key-1 twice: both `Kept` 3, subtotal 29.97, bodies byte-identical (462 bytes), quantity stayed 3.

## Changed tests (deliberate)
- `ProductDetailView.test` "has no add-to-cart control in this slice" replaced by a presence test (app context 4.3).
- `ProductCard.test`: both old assertions kept; one new Select-options case.
- `CatalogSchemaTests` table list +`cart_items`, `carts`, `idempotency_keys`. `tests/routes.test` cart row: stub to built.
- Mechanical: three `IInventoryReader` fakes gained `GetAvailabilityAsync`. `SolutionLayout.ExpectedModules` +`"Cart"`.
- Added a Login/Register "device cart" notice test each (appended).

## Hunks in files already dirty before the slice
- `Dockerfile`: +2 COPY lines (`Ecommerce.Cart.Contracts.csproj`, `Ecommerce.Cart.csproj`). `Ecommerce.slnx`: +Cart folder (2 projects), +`Ecommerce.Cart.Tests`.
- `appsettings.json`: `"Cart": { "MaxQuantityPerLine": 10 },` and `"Store": { "Currency": "USD" },` before `"Inventory"`.
- `Ecommerce.Api.csproj`: +Cart project reference. `ModuleComposition.cs`: +`using Ecommerce.Cart;`, `typeof(CartModule).Assembly`, `.AddCartModule(configuration)`.
- `EcommerceDbContext.cs`: +`ApplyConfiguration(new Idempotency.IdempotencyKeyEntryConfiguration())`. `PersistenceServiceCollectionExtensions.cs`: +`AddScoped<IIdempotencyStore, Idempotency.EfIdempotencyStore>()`.
- `package.json`: 3 dependencies above + jest `setupFiles: tests/jest-setup.js` (AsyncStorage mock, expo-crypto mock).
- `lib/api-client.ts`: `headers?` option, `DELETE` method, `available` on `ApiError`. `lib/query-keys.ts`: +`cart`.
- `app/_layout.tsx`: mounts `GuestCartMergeEffect`; header = `TopNav` + `MergeResultNotice`. `app/cart.tsx`: renders `CartView`.
- `ProductCard.tsx`: link restructured into two sibling links. `ProductDetailContent.tsx`: +`AddToCartControl`, `hideAddToCart` prop. `AdminProductDetailView.tsx`: passes `hideAddToCart`.
- `LoginForm.tsx`, `RegisterForm.tsx`: +`<GuestCartNotice />` only.

## Changed (new)
Backend: `Cart` and `Cart.Contracts` projects (domain, services, store, controller, options), `IIdempotencyStore` + EF store, `IProductReader.GetVariantSnapshotsAsync`, `IInventoryReader.GetAvailabilityAsync`, migration `AddCart`, tests (`Cart.Tests` 19, integration `Cart/` 4 files). Frontend: `features/cart/*` (types, api, two stores, 3 hooks, 6 components + tests), `lib/idempotency-key.ts`, `tests/{cart-fixtures.ts,jest-setup.js}`.

## verify.sh
- Full (Debug config, see concern 1): `VERIFY PASS (secrets hygiene build test drift expo-check tsc jest export)`; build 0 warnings, jest 374.
- Docker: `VERIFY PASS (docker)`.
- Counts: Inventory 19, Cart 19, Catalog 166, Orders 157, Identity 73, Architecture 25, Integration 261.

Quality-bar §2: all boxes checked except: Release-config gate not re-run (concern 1).
