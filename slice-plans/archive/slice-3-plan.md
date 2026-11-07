# Slice 3 — Cart · Plan (Rob)

Mode: Autonomous (owner override: Slices 2 → 3 → 4 → 6 back to back, §13.2) · Verdict: READY · 2026-11-07

**Read:** arch §2, §3.4, §5 (all: §5.1–5.5), §6.2, §7.1 (`carts`, `cart_items`, `idempotency_keys`), §7.4 (`Store:Currency`), §8.1, §9 (endpoint table + **Cart contract (Slice 3)**), §11.2 tests 21, 22, §11.3 (F3, F9 + rules), §12.3 row `3`; §13.2 grep `Slice 3` and `Guest device-cart`. App context §4.3, §4.4, §5. Quality bar §1, §2, §4. Patterns: `Modules/Orders` (layout, `[Authorize]` + `sub`, `MoneyResponse`), `Modules/Inventory` (store locks, tests), `Persistence/Audit/AuditLogEntry.cs`, `Program.cs` (strict JSON), `Dockerfile`, `Ecommerce.slnx`; frontend `features/catalog`, `features/auth/{session.ts,hooks,components/LoginForm.tsx,RegisterForm.tsx}`, `lib/{api-client,query-keys,money}.ts`, `app/_layout.tsx`, `tests/routes.test.tsx`.

## 0. Run context and rules for Marco

- **Baseline:** `slice-plans/slice-3-baseline.txt` holds `git hash-object` for all 198 dirty/untracked paths before Slice 3. In review I re-hash it, and only new or changed paths count as Slice 3 work. Rob's doc edits (07, 09, 12, 13.2) are part of the delta. If you edit a file that was already dirty (`app/_layout.tsx`, `LoginForm.tsx`, `RegisterForm.tsx`, `ProductCard*.tsx`, `ProductDetail*.tsx`, `package.json`, `Dockerfile`, `Ecommerce.slnx`, `appsettings.json` …), quote your hunks for it in your report.
- **Git is read-only.** Use only `git -c safe.directory='*'` status/diff/log/show. Never stage, remove, restore or check out.
- **New csproj files** (`Ecommerce.Cart`, `Ecommerce.Cart.Contracts`, `Ecommerce.Cart.Tests`): add them to `Ecommerce.slnx`. Add the two `src` ones to the `Dockerfile` restore `COPY` list. Run `bash scripts/verify.sh docker` once, plus the full `verify.sh`.
- **Report hygiene:** no literals that trip the `secrets` scan (no key-, token- or password-shaped strings, no `Bearer …`). Describe them in words.
- Smoke runs only on :5090 against `ecommerce_smoke`. Never use the owner's ports.

## 1. Scope

**In (backend):**
- New module `Cart` (+ empty `Cart.Contracts`): `carts`/`cart_items` migration.
- `GET /cart`, `POST /cart/items`, `PUT /cart/items/{variantId}`, `DELETE /cart/items/{variantId}`, `POST /cart/merge` per §8.1/§9.
- Platform `idempotency_keys` table + `IIdempotencyStore`.
- `IProductReader.GetVariantSnapshotsAsync`, `IInventoryReader.GetAvailabilityAsync`.
- `Cart:MaxQuantityPerLine` (default 10) and `Store:Currency` (`USD`) with `ValidateOnStart`.

**In (frontend):**
- Device guest cart: Zustand `persist` + AsyncStorage.
- `/cart` screen for the server cart and the guest cart.
- Add-to-cart on product detail.
- Guest merge on sign-in, with a global result notice.
- Shop card "Select options" button.
- One truthful line on sign-in/register when the device has cart lines.

**Out:** checkout/reservations and `ICartReader` (Slice 4), guest-line validation endpoint, `ICurrentUser` (D11), Redis and key retention (Slice 7), header cart count, any `GET /products` change.

## 2. Decisions

| # | Decision | Reason | Rejected |
|---|---|---|---|
| D1 | Cart module = `Ecommerce.Cart` + `Ecommerce.Cart.Contracts` (empty until Slice 4). References: SharedKernel, Persistence, Cart.Contracts, Catalog.Contracts, Inventory.Contracts. Endpoints `[Authorize]` (any signed-in user owns exactly their cart via `sub`) | §6.1/§6.2; Orders precedent; no route carries an id | Customer-only policy (blocks admins testing the store) |
| D2 | Every write: `INSERT INTO carts … ON CONFLICT (user_id) DO NOTHING`, then `SELECT … FOR UPDATE` the caller's cart row, inside `IUnitOfWork.InTransactionAsync`. Lock order `carts → cart_items → idempotency_keys` | No lost update or double merge | Optimistic retry on lines |
| D3 | Stock/price reads go through `IProductReader.GetVariantSnapshotsAsync` and `IInventoryReader.GetAvailabilityAsync`. These are display-grade, take no locks, and make no external calls | Rule 8; the cart reserves nothing (§8.1, §8.2) | Inventory locks in cart |
| D4 | Add/set validation order: body bounds → (PUT) line exists else 404 → variant snapshot exists, active and in `Store:Currency`, else 400 → resulting qty 1…cap, else 400 → resulting qty ≤ available, else 409 `{available}`. Any rejection changes nothing. The written line's `display_unit_price` is refreshed | §8.1, test 21 | — |
| D5 | Cart read: every line re-priced from Catalog. Issues: `PriceChanged` (current ≠ display), `ExceedsStock` (qty > available), `Unavailable` (unknown, inactive or other currency). `subtotal` = Σ `lineTotal` of lines without `Unavailable`, in `Store:Currency`. GET never creates or writes a row | §8.1, §9 (updated) | Refreshing prices on read (would hide `PriceChanged`) |
| D6 | Merge: 1–100 distinct lines, qty 1–1000, `Idempotency-Key` 1–100 chars. Per input line: unknown/inactive/other currency → `Removed/Unavailable`. Otherwise `target = min(server + guest, cap, available)`. If `target ≥ server + guest` → `Kept`. If `server < target` → `Capped` (reason = binding bound: `OutOfStock` if available < cap else `LimitReached`). Else → `Removed` with that reason, server line untouched. **Never lowers an existing server line.** Results in input order | §8.1, app ctx §4.4, test 22 | Dropping duplicates silently |
| D7 | Idempotency: in the merge transaction, after the cart lock, `FindAsync("cart-merge", key)`. If found: a different user or a different request hash (SHA-256 of the normalised body) → 409, else return the stored `response_json` verbatim. If not found: apply, then `Add` the row (user, hash, response, `expires_at = now + 24 h`) in the same transaction. A cross-user race on one key ends in a unique violation → 409 | Rule 2 style; §9 "same key → same response" | Storing only a result reference (the cart changes later) |
| D8 | `IIdempotencyStore` in SharedKernel (`FindAsync`, `Add`; `Add` only stages, like `IAuditLog`), implemented in Persistence with entity `IdempotencyKeyEntry` (table `idempotency_keys`, PK `(scope, key)`, scope CHECK in the §7.1 list) | Slice 4 reuses it for `checkout`, `retry-payment` and `stripe-event` | Per-module tables |
| D9 | `IProductReader.GetVariantSnapshotsAsync(ids)` → `VariantSnapshot(VariantId, ProductSlug, ProductName, VariantName, CartImage? Image, decimal Price, string Currency, bool IsActive)`, `CartImage(Url, AltText)` resolved by Catalog's `IImageUrlResolver` from the primary image. Unknown IDs omitted | Only Catalog can resolve image URLs; §9 now says `image` | Raw `imageKey` |
| D10 | `IInventoryReader.GetAvailabilityAsync(ids)` → `StockAvailability(int Available, StockStatus Status)` per requested ID. Available is clamped ≥ 0, a missing row gives 0/`Out`, and it reuses `StockLevel.Classify`. Update the interface doc comment ("callers never see quantities" is no longer true) | 409 `available` and merge caps need the number | A second reader interface |
| D11 | No `ICurrentUser`/`IUserReader`; the controller reads `sub` like Orders. §12.3 carry-in updated | No consumer | — |
| D12 | `Cart:MaxQuantityPerLine` int 1–100, default 10. `Store:Currency` `^[A-Z]{3}$`, `appsettings.json` `"USD"`. Both bound by Cart, `ValidateOnStart` | §7.4, §8.1 | Hard-coded |
| D13 | **Guest store** `features/cart/guest-cart-store.ts`: Zustand `persist` (`createJSONStorage(() => AsyncStorage)`, name `guest-cart`, `version: 1`). State `{ lines: GuestLine[], mergeKey: string \| null }`. `GuestLine = { variantId, productSlug, quantity, snapshot: { productName, variantName, price: Money, image } }`. Actions: `add` (sums, 1–99, ≤ 100 lines), `setQuantity` (0 removes), `remove`, `clearAfterMerge`, `ensureMergeKey`. **Every line edit sets `mergeKey = null`** | §8.1, §13.2, F9. A key reused with a changed body would get a 409 | — |
| D14 | **Merge runner** `features/cart/hooks/useGuestCartMerge.ts`, mounted once in `app/_layout.tsx` (via `features/cart/components/GuestCartMergeEffect.tsx`). When signed in, hydrated, lines > 0 and idle: `ensureMergeKey()` → `POST /cart/merge`. On 200: write the returned cart into the cart query cache, put the results in a non-persisted result store, `clearAfterMerge()`. On failure: keep lines and key, expose `error` + `retry` (same key). On 409: drop the key so a retry gets a new one | F9, §13.2 row "Guest merge on the client" | Merging only inside the login mutation |
| D15 | **Merge result** `MergeResultNotice` (global, under the header in `_layout`). Every result line with outcome and plain reason; "View cart", "Dismiss"; on failure the error with "Try again" | "Never drop silently"; visible wherever login returns to | Showing it only on `/cart` |
| D16 | **Copy:** one shared `GuestCartNotice` line, "Items in this device's cart will be added to your account.", rendered in `LoginForm` and `RegisterForm` **only** when the device cart has lines. No other copy change | Truthful now that merge exists; minimal | Restoring the old broader claims |
| D17 | `/cart` (`features/cart/components/CartView.tsx`): signed in → server cart (`useCartQuery`) with quantity stepper/remove (`PUT`, `DELETE`). Each server reply replaces the cache (`setQueryData`); there are no optimistic writes. Issues in words; server `subtotal`. Signed out → guest lines (snapshot price labelled an estimate, same stepper/remove). **The client does no price arithmetic for the server cart.** Guest line totals are not shown | F3, user requirement "client never does price maths" | Client-computed totals |
| D18 | **Add-to-cart** on detail (`features/cart/components/AddToCartControl.tsx` inside `ProductDetailContent`): stepper (1–10) + "Add to cart" for the selected variant, disabled when `Out`. Signed in → `POST /cart/items`, show 409 `available` / 400 errors, success → "Added to cart" + `/cart` link. Signed out → `guestCart.add(snapshot from detail)` | App ctx §4.3; server result wins | Optimistic add |
| D19 | **Shop card:** restructure so no button sits inside the card link. Outer `View` holding (a) the existing product link (image + text) and (b) a sibling `Link asChild` + `Pressable` `accessibilityRole="link"`, label "Select options for {name}", same href | §13.2 "Shop card Select options"; no nested interactive elements | Add-to-cart on the card |
| D20 | **Packages** (rule 16). `npx expo install` (SDK-pinned): **`zustand`** (guest cart state; stack lock §2), **`@react-native-async-storage/async-storage`** (its persistence; `localStorage` on web), **`expo-crypto`** (`randomUUID` for keys, D21). Each replaces nothing: no client store, storage or UUID source exists. No other package | §13.2 rows | `expo-secure-store` (§13.2) |
| D21 | `lib/idempotency-key.ts` exports `newIdempotencyKey()` (expo-crypto). `features/cart/api.ts` sends the key as the `Idempotency-Key` header through `api-client` | One place, F4 reuse in Slice 4 | — |

## 3. Files, contracts, migration

**Backend — new**
- `Modules/Cart/Ecommerce.Cart.Contracts/` (csproj only, SharedKernel ref).
- `Modules/Cart/Ecommerce.Cart/`:
  - `CartModule.cs`
  - `Domain/{Cart.cs, CartItem.cs}`: `Cart.Add(variantId, qty, price, currency, now)`, `Set(...)`, `Remove(variantId)`, `QuantityOf(variantId)`; guards: qty > 0 (§7.1 CHECK).
  - `Application/{CartPorts.cs (ICartStore: EnsureAndLockAsync(userId), GetAsync(userId) no-tracking), CartService.cs (Get/Add/Set/Remove), CartMergeService.cs, CartViewBuilder.cs (D5), CartResults.cs}`.
  - `Infrastructure/{CartOptions.cs, CartStore.cs, Persistence/{CartDbModel, CartConfiguration, CartItemConfiguration}.cs}`.
  - `Api/{CartController.cs, CartRequests.cs (+ FluentValidation validators), CartResponses.cs (copy Orders' MoneyResponse shape)}`. The controller stays thin. It reads `Idempotency-Key` from the header and passes it to the service.
- `Ecommerce.SharedKernel/Persistence/IIdempotencyStore.cs` (`IdempotencyEntry` record: Scope, Key, UserId?, RequestHash, ResultRef?, ResponseJson?, CreatedAt, ExpiresAt).
- `Ecommerce.Persistence/Idempotency/IdempotencyKeyEntry.cs` (entity + config + `EfIdempotencyStore`), registered beside `EfAuditLog`.
- `Modules/Catalog/Ecommerce.Catalog.Contracts/IProductReader.cs`: add D9. `Catalog/Infrastructure/ProductReader.cs` implements it (own tables, `AsNoTracking`, primary image = lowest `sort_order`).
- `Modules/Inventory/Ecommerce.Inventory.Contracts/IInventoryReader.cs`: add D10. `InventoryStockReader` implements it.
- Host: `ModuleComposition` adds `AddCartModule`, and `ModuleAssemblies` adds Cart. `appsettings.json`: `"Cart": { "MaxQuantityPerLine": 10 }`, `"Store": { "Currency": "USD" }`.
- Tests: `tests/Ecommerce.Cart.Tests` (new project: Domain + Application with fakes), `tests/Ecommerce.Api.IntegrationTests/Cart/*`. `ArchitectureTests/SolutionLayout.ExpectedModules` adds `"Cart"` (stricter, so justified). Make no other architecture-test edit.

**Migration `AddCart`** (additive):
- `carts`: `id uuid PK`, `user_id uuid` unique `ux_carts_user_id`, `updated_at timestamptz`.
- `cart_items`: `id uuid PK`, `cart_id` FK → carts (cascade), `product_variant_id uuid`, `quantity int CHECK > 0`, `display_unit_price numeric(19,4)`, `currency varchar(3)`, `added_at`, `updated_at`; unique `ux_cart_items_cart_variant (cart_id, product_variant_id)`.
- `idempotency_keys`: `scope varchar(30)`, `key varchar(100)`, PK `(scope, key)`, `user_id uuid null`, `request_hash varchar(64)`, `result_ref varchar(100) null`, `response_json jsonb null`, `created_at`, `expires_at`; `CHECK scope IN ('checkout','retry-payment','refund','cart-merge','stripe-event')`.
- No FK to users or variants (cross-module).

**Frontend:** `lib/idempotency-key.ts`(+test); `lib/query-keys.ts` (+`cart`); `features/cart/{types, api(+test), guest-cart-store(+test), merge-result-store(+test)}.ts`, `hooks/{useCartQuery, useCartMutations, useGuestCartMerge(+test)}.ts`, `components/{CartView, CartLineRow, AddToCartControl, MergeResultNotice, GuestCartMergeEffect, GuestCartNotice}.tsx` (+tests); `app/cart.tsx` → `CartView`; `app/_layout.tsx` mounts the effect + notice; `ProductCard`, `ProductDetailContent`, `LoginForm`, `RegisterForm`; `package.json` (3 deps) + Jest mocks (AsyncStorage's own mock, expo-crypto).

## 4. Build order (quote each red)

1. **Test 22 first** (integration: merge rules + idempotency, red), then **Test 21 first** (bounds, red).
2. Migration + schema test (tables, constraint names, `idempotency_keys` scope CHECK).
3. Contracts (D9, D10) with their tests → `IIdempotencyStore` → Cart domain → services → controller → green.
4. Auth tests for every endpoint (401; a customer only ever sees their own cart).
5. Frontend test-first for F9 (store + merge hook), then F3 (CartView, AddToCartControl), then the card and the copy.
6. Mutation checks, full `verify.sh`, `verify.sh docker`, smoke.

## 5. Required tests

**Test 21 (integration).** For each case, the cart is unchanged afterwards (read it back):
- `POST` resulting qty < 1 or > cap, `PUT` > cap or negative → 400.
- A body carrying `unitPrice`/`price` → 400 (strict JSON).
- Resulting qty > available → 409 with `available`.
- Inactive variant / inactive product / unknown variant / other currency → 400.
- `PUT` of an absent line → 404.
- `PUT 0`/`DELETE` remove; `DELETE` absent → 204.
- Response `unitPrice` equals the current Catalog price after a price change (and `PriceChanged` is flagged, `display_unit_price` refreshed on the next write of that line).
- `ExceedsStock` after an adjust lowers stock; `Unavailable` after unpublish.
- `subtotal` excludes `Unavailable` lines.
- 401 for anonymous on all five endpoints.
- User A's writes never appear in user B's `GET /cart`.

**Test 22 (integration).**
- Same variant: server 2 + guest 3, cap 10, available 20 → `Kept` 5.
- Sum over cap → `Capped` 10 `LimitReached`.
- Over available (available 4 < cap) → `Capped` 4 `OutOfStock`.
- Available ≤ server qty → `Removed`, server line unchanged.
- Inactive / unknown / other-currency → `Removed` `Unavailable`.
- Every input line appears exactly once in `results`, in input order.
- The same `Idempotency-Key` twice → byte-identical body, and quantities are not doubled. Also after an edit in between (stored reply).
- Same key + different body → 409. Same key, other user → 409.
- Missing key → 400. Empty `lines`, > 100 lines, duplicate variant, qty 0 or 1001 → 400. Each rejection writes nothing.
- Concurrency: the same key sent twice in parallel → one application, two identical bodies.

**Also required:**
- Domain: `Cart.Add`/`Set`/`Remove` guards.
- `CartMergeService` outcome table: unit tests with fakes covering every D6 branch.
- `GetVariantSnapshotsAsync`: resolved image, inactive flag, unknown omitted.
- `GetAvailabilityAsync`: no row = 0/`Out`; clamps.
- Config: `Cart:MaxQuantityPerLine` 0 or 101, `Store:Currency` `usd`/`US` → host fails to start.
- Concurrent `POST` of the same variant (2 × qty 3, cap 10) → final 6, never 3.

**F9 (Jest, test-first).**
- Lines persist across a store re-creation with AsyncStorage mocked (rehydrate).
- `add` sums, `setQuantity` sets, 0/`remove` delete.
- Every edit nulls `mergeKey`.
- Merge hook: only when signed in with lines; 200 clears lines + key and publishes results; failure keeps both; `retry` reuses the **same** key; 409 drops it; one request in flight.

**F3 (Jest).**
- `CartView` shows the server's `unitPrice`, `lineTotal`, `subtotal` strings as given, and each issue in words.
- After a mutation reply with a new price or a 409 `available`, the old value is gone from the screen.
- `MergeResultNotice` lists every result with outcome and reason, and Dismiss hides it.
- `AddToCartControl`: signed in posts `{ variantId, quantity }` only (no price field) and shows the 409 message; signed out adds to the guest store; disabled for `Out`.

**Other Jest:** card "Select options" → `/products/[slug]`, sibling (not nested) links; `GuestCartNotice` only with device lines; distinct UUIDs; `api.ts` sends the header and no price.

**Mutation checks (report each red):** cart-row `FOR UPDATE` → concurrent POST; idempotency lookup → replay; hash comparison → different-body 409; "never lower" → server line kept; `mergeKey = null` on edit → F9; clear on failure → F9; `[Authorize]` → 401.

**Smoke (:5090, `ecommerce_smoke`):** register, add, `PUT`, `DELETE`; merge twice with one key, quote both identical bodies.

## 6. Changed tests (deliberate, listed)

- `ProductCard.test.tsx:58` "offers no add-to-cart or rating". **Keep** both assertions; they stay true, since the card has "Select options", not add-to-cart. Only add new cases. If the test name changes, give the reason.
- `ProductDetailView.test.tsx:106` "has no add-to-cart control in this slice". **Replace** it with the presence test (owner decision; app ctx §4.3). The old assertion is now intentionally false.
- Fakes of `IProductReader`/`IInventoryReader` gain the new members (mechanical, no assertion change).
- `SolutionLayout.ExpectedModules` + `"Cart"`.
- List any other changed test with its reason.

## 7. Risks

- Guest prices are an estimate until sign-in (no public validation endpoint); the merge result corrects them.
- No cart endpoint takes an id; cross-user isolation is proven by "A's writes absent from B's cart".
- Slice 4 must reuse `IIdempotencyStore` as built or justify a change.
- The 409 `available` shows stock counts to shoppers (by design, §9).

## 8. Open items for Nathaniel

1. **Features 9 and 10** (Out of Stock filter, `/admin/inventory` screen) still have no §12.3 row.
2. **App-context edit (carried from the Slice 5 review, still not pasted).** Agents do not edit that file. Verbatim:

> **Line 55** (§4.1 Browse, item 1). Replace the whole line with:
> ```
> 1. **Shop (first screen):** `/` opens the product list; best sellers, categories and search live there.
> ```
> **Line 163** (§5 Screens). Replace the `/` row with:
> ```
> | `/` | Public | Redirects to `/products` (Shop) |
> ```
> **Line 164** (the `/products` row). Replace it with:
> ```
> | `/products` | Public | Shop (first screen): best sellers, search, filter, sort, pagination |
> ```
> The decision log already has the "Store opens on Shop" row.

3. `verify.sh` hygiene reads the deleted `UnknownInventoryReader.cs` until the Slice 2 deletion is committed.
