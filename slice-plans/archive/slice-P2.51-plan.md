# Slice P2.51 — Item thumbnail and colour on order items (customer order detail)

Mode: Autonomous · Risk tier: **Low** (CLAUDE.md §5) — two additive, read-only, display-only fields on each item of `GET /orders/{id}` (and the admin detail that embeds it), read from Catalog through `IProductReader` after the ownership check. **No migration, no order data touched**, no money, no stock, no state transition, no auth change. Mutations: **none** (Low tier). Rob reads the diff; Marco's tests + full `verify.sh` are the gate.
Scope: tracker row P2.51 only (`docs/admin-dashboard-feature-tracker.md`). Design: `docs/design-refs/northline-order-detail.md` item 5 (Items card) and CSS `.item-row`. SKU and unit price ("each") already render (P2.50 state) — not touched. P2.52–P2.53 out of scope. All files: **Marco**.

## 1. Read

- `docs/architecture/09-api.md` line 69 (`GET /orders/{id}` — Rob already added `image`/`colour`).
- `docs/architecture/13.2-decision-log.md` — grep `Order item display` (row added by this plan) and `Catalog snapshot for Cart`.
- `docs/architecture/05-frontend.md` §5.5 (tokens; `style` only for runtime values).
- `docs/architecture/11-testing.md` §11.3.
- Backend code: `Catalog.Contracts/IProductReader.cs` (`VariantSnapshot`, `CartImage`), `Catalog/Infrastructure/ProductReader.cs` (`GetVariantSnapshotsAsync`, `SnapshotRow`), `Orders/Application/OrderQueryService.cs` (`BuildAsync`), `Orders/Application/OrderViews.cs` (`OrderDetailView`), `Orders/Api/OrderResponses.cs` (`OrderItemResponse`, `OrderDetailResponse`, both mappings).
- Backend tests: `Ecommerce.Api.IntegrationTests/Orders/OrdersReadApiTests.cs` (`OrdersEnv`, T29 detail test, P2_50 tests), `Orders/OrderSeed.cs` (`SeedItem`, item insert), `Inventory/InventoryEnv.cs` `AddProductAsync` (how to build a Catalog product/variant via the domain), `Orders/OrdersFulfillmentApiTests.cs` lines 20–30 (admin user + token), `Ecommerce.Cart.Tests/Application/CartMergeServiceTests.cs` line 62 (`Snapshot` helper).
- Frontend: `features/orders/types.ts`, `features/orders/components/OrderDetailView.tsx` (`ItemRow`), `features/catalog/components/ProductImageView.tsx`, `features/cart/types.ts` (`CartImage`), `lib/design-tokens.js` (`touch`), `tests/order-fixtures.ts`, `tests/render-with-query.tsx`, `features/orders/components/OrderDetailView.status-note.test.tsx` (setup to copy).

No migration, no new endpoint, no new package. §12.3 has no row for P2 tracker rows; the tracker row is the acceptance criterion.

## 2. Decisions

| Decision | Reason | Alternative rejected |
|---|---|---|
| **Read from Catalog at request time**, not a snapshot on the order line: `OrderQueryService.BuildAsync` calls `IProductReader.GetVariantSnapshotsAsync(item variant IDs)` (rule 8 — Catalog contract, no join, no `DbSet`) | Both fields are display-only, never money. Image URLs must not be stored: Catalog resolves them from `Catalog:Images:BaseUrl` at read time (a stored URL would bake in a host). A colour snapshot column would leave every existing order without colour forever (a backfill would need a cross-module SQL join — banned) and would change `CheckoutService`, a High-tier path. The money/legal snapshot (`sku`, `name` = "Product - Variant", `unitPrice`) is untouched, so test 6 still holds | `colour_snapshot`/`image_key_snapshot` columns on `order_items` written at checkout (migration + checkout change + empty for existing orders) |
| **Trade-off accepted:** if an admin later edits a variant's colour or replaces the product images, the order shows the current value. The name snapshot (which normally contains the variant name) is still the record of what was bought | SKU is immutable (`ProductVariant`), so the variant is the same item; colour edits are corrections | — |
| **No migration.** No table or column touched | Fields come from Catalog's existing `product_variants.colour` and `product_images` | — |
| **Contract:** `VariantSnapshot` gains a last positional `string? Colour` (filled from `v.Colour` in `ProductReader`'s `SnapshotRow`). No new reader method | One existing query already returns the primary image; Cart and checkout ignore the new field | A new `GetVariantDisplaysAsync` method (second query path for the same rows) |
| **Order view:** `OrderDetailView` gains `IReadOnlyDictionary<Guid, VariantSnapshot> Variants` (keyed by variant ID). Mapping reads `view.Variants.GetValueOrDefault(i.VariantId)` | No extra Orders type; the lookup happens only after `GetAsync`'s ownership check (403/404 never query Catalog) | A new `ItemDisplay` record (no consumer beyond the mapping) |
| **Wire shape:** new `OrderDetailItemResponse(Guid VariantId, string Sku, string Name, MoneyResponse UnitPrice, int Quantity, MoneyResponse LineTotal, CartImage? Image, string? Colour)` used only by `OrderDetailResponse.Items`. `OrderItemResponse` and the checkout response are **unchanged** | Checkout has no Catalog display lookup; adding always-null fields to its contract would mislead clients. `CartImage` (`{ url, altText }`) is the existing Catalog contract type for a resolved image — reused, not twinned | Adding nullable fields to `OrderItemResponse` (checkout would send `image: null` always) |
| **Fallbacks:** variant unknown to Catalog → `image: null, colour: null`; product with no image, or `Catalog:Images:BaseUrl` empty → `image: null`; variant with no colour → `colour: null`. Never an error, the order still returns 200 | Catalog omits unknown IDs and returns a null image by design (decision "Catalog snapshot for Cart") | Failing the read or hiding the item |
| **Image placeholder / storage:** none new. URLs come from Catalog's `IImageUrlResolver`; committed `BaseUrl` stays `""` (images null). Tests use `https://img.example.test` (reserved test domain), set in `OrdersEnv` | No real bucket or domain | — |
| **Client fallback:** `ProductImageView` with `url = item.image?.url ?? null` already renders a neutral `bg-surface-raised` box labelled with `altText` or the item name. Colour line only when `colour` is non-null | One existing component; fixed box, no layout shift | A new thumbnail component |
| **Thumbnail size:** `width={touch}` (44, from `@/lib/design-tokens`), `aspectRatio` 1 | Token, not a pixel literal (§5.5); the mock-up gives no thumbnail size | `width={56}` literal |
| **Row a11y label** becomes `` `${item.name}${item.colour ? `, ${item.colour}` : ''} × ${item.quantity}, ${amount}` `` — unchanged when colour is null | Row is one screen-reader stop (P2-style); colour must be in it | — |
| Frontend `OrderItem` (shared with checkout) is **unchanged**; new `OrderDetailItem = OrderItem & { image: CartImage \| null; colour: string \| null }`; `OrderDetail.items: OrderDetailItem[]` | Mirrors the backend split; `useReorder(items: OrderItem[])` accepts it unchanged | — |

Docs (done by Rob in this step): `09-api.md` line 69 lists item `image`/`colour`; decision-log row "Order item display fields (P2.51, Rob)".

## 3. Files (all Marco)

| File | Change |
|---|---|
| `backend/src/Modules/Catalog/Ecommerce.Catalog.Contracts/IProductReader.cs` | `VariantSnapshot` last param `string? Colour`; update the summary to mention colour |
| `backend/src/Modules/Catalog/Ecommerce.Catalog/Infrastructure/ProductReader.cs` | `SnapshotRow` gains `string? Colour` (select `v.Colour`); pass it to `VariantSnapshot` |
| `backend/src/Modules/Orders/Ecommerce.Orders/Application/OrderViews.cs` | `OrderDetailView` last param `IReadOnlyDictionary<Guid, VariantSnapshot> Variants` (`using Ecommerce.Catalog.Contracts;`) |
| `backend/src/Modules/Orders/Ecommerce.Orders/Application/OrderQueryService.cs` | inject `IProductReader products`; in `BuildAsync` call `GetVariantSnapshotsAsync(order.Items.Select(i => i.VariantId).Distinct().ToList(), ct)` → `ToDictionary(s => s.VariantId)`; pass to `OrderDetailView` |
| `backend/src/Modules/Orders/Ecommerce.Orders/Api/OrderResponses.cs` | new `OrderDetailItemResponse`; `OrderDetailResponse.Items` typed `IReadOnlyList<OrderDetailItemResponse>`; `ToResponse(OrderDetailView)` fills `Image`/`Colour` from `view.Variants` (null when absent). Checkout mapping untouched |
| `backend/tests/Ecommerce.Cart.Tests/Application/CartMergeServiceTests.cs` | `Snapshot` helper: append `null` colour arg (compile fix only; listed as changed test) |
| `backend/tests/Ecommerce.Api.IntegrationTests/Orders/OrderSeed.cs` | `SeedItem` gains optional last `Guid? VariantId = null`; insert uses `item.VariantId ?? Guid.NewGuid()` (existing calls unchanged) |
| `backend/tests/Ecommerce.Api.IntegrationTests/Orders/OrdersReadApiTests.cs` | `OrdersEnv` settings add `["Catalog:Images:BaseUrl"] = "https://img.example.test"`; private helper `CatalogVariantAsync(env, sku, colour, imageKey?)` building Category/Product/Variant/Image via the domain like `InventoryEnv.AddProductAsync` (`product.AddVariant(..., size: null, colour, ...)`, `product.AddImage(...)`, `Publish`); **append** I1–I5 |
| `frontend/apps/store/features/orders/types.ts` | `OrderDetailItem` type; `OrderDetail.items: OrderDetailItem[]` |
| `frontend/apps/store/tests/order-fixtures.ts` | default item gains `image: null, colour: null` (only change) |
| `frontend/apps/store/features/orders/components/OrderDetailView.tsx` | `ItemRow` takes `OrderDetailItem`; thumbnail + colour line + label (below) |
| `frontend/apps/store/features/orders/components/OrderDetailView.items.test.tsx` | **new** — F1–F3 |
| `docs/admin-dashboard-feature-tracker.md` | P2.51 `Todo` → `Done` after verify passes |

**UI contract** (`ItemRow`, tokens only):
- First child of the row: `<ProductImageView url={item.image?.url ?? null} altText={item.image?.altText ?? ''} fallbackLabel={item.name} width={touch} />` (import from `@/features/catalog/components/ProductImageView`).
- In the name/SKU column, after the SKU: `{item.colour ? <Text className="text-meta text-muted">{`Colour: ${item.colour}`}</Text> : null}`.
- Row `accessibilityLabel` as in §2. Nothing else in the row changes.

## 4. Build order

1. Contract + `ProductReader` colour; fix `CartMergeServiceTests` helper; build.
2. Write I1–I5 → run → quote red (I1/I3/I5 fail: no `image`/`colour` in JSON).
3. `OrderViews`, `OrderQueryService`, `OrderResponses` → I1–I5 green; T29, P2_50 and checkout tests still green.
4. Frontend types + fixture; write F1–F3 → quote red; `ItemRow` → green.
5. Full `bash scripts/verify.sh`; tracker row → Done; report.

## 5. Required tests

Integration (`OrdersReadApiTests.cs`, all against `GET /api/v1/orders/{id}` unless stated):

| # | Test | Asserts |
|---|---|---|
| I1 (test-first) | `P2_51_an_item_returns_its_catalog_colour_and_primary_image_url` | Variant in Catalog with colour `Black` and image key `tees/black front.jpg`, alt `Black tee, front`; seeded line uses that `VariantId` with snapshot name `Black tee, M` while the Catalog product name differs → `items[0].colour == "Black"`, `items[0].image == { url: "https://img.example.test/tees/black%20front.jpg", altText: "Black tee, front" }`, `items[0].name == "Black tee, M"` (snapshot wins) |
| I2 | `P2_51_missing_colour_image_or_catalog_variant_gives_nulls_and_still_200` | One order, two lines: a Catalog variant with no colour and no image, and the default seed line (variant ID unknown to Catalog) → 200, both items `image: null`, `colour: null`, sku/name/unitPrice/lineTotal unchanged |
| I3 | `P2_51_items_keep_their_order_and_each_gets_its_own_variant_data` | Two Catalog variants (`Black`/`Red`, different images) on one order → each item's colour/image matches its own `variantId` (no cross-line bleed) |
| I4 | `P2_51_another_customers_order_is_403_without_item_data` | Customer B reads A's order with a Catalog image → 403 and the body contains neither `img.example.test` nor the colour |
| I5 (test-first) | `P2_51_admin_order_detail_embeds_the_item_colour_and_image` | Admin token, `GET /api/v1/admin/orders/{id}` → `order.items[0].colour`/`image.url` as in I1 |

Existing checkout tests must stay green unchanged (proves `OrderItemResponse`/checkout contract untouched).

Jest (`OrderDetailView.items.test.tsx`, `renderRouterWithQuery({ 'orders/[id]': OrderDetailScreen }, { initialUrl: '/orders/o-1' })`, setup copied from `status-note.test.tsx`):

| # | Test | Asserts |
|---|---|---|
| F1 (test-first) | shows the thumbnail and colour | item `image: { url: 'https://img.test.invalid/tee.jpg', altText: 'Black tee, front' }`, `colour: 'Black'` → element labelled `Black tee, front` exists with image source uri; text `Colour: Black`; row label `Black tee, M, Black × 2, $40.00` (use the formatted amount the existing tests use) |
| F2 | placeholder and no colour line when both are null | default fixture → an `image`-role placeholder labelled `Black tee, M`; no `Colour:` text; row label unchanged from before |
| F3 | each row shows its own colour | two items, one with `colour: 'Red'`, one null → exactly one `Colour: Red` text |

Existing `OrderDetailView*.test.tsx`, admin order tests and checkout tests stay unchanged and green (fixture defaults are null).

## 6. Risks

- **Nested accessible in the row:** `ProductImageView` is `accessible` inside the `accessible` row. If RNTL can't find it by role, query by label (`getByLabelText`) — do not remove the row's grouping.
- **Positional record change** to `VariantSnapshot`: any construction site missed fails the build (good). Only `ProductReader` and the Cart test helper construct it today.
- **Extra Catalog query per detail read** (two statements, by primary key / product ID): acceptable; not on the list endpoint.
- **Admin frontend** consumes `OrderDetail`; types widen only, admin UI unchanged — `tsc` must stay clean.
- `OrdersEnv` now sets an image base URL for every order read test; no existing test asserts images, so none change.
