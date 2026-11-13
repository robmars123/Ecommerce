# Slice P2.51 — Marco report

Concerns: None. (Prettier initially failed the new F-test file in `verify.sh format`; fixed with `prettier --write`, re-run green.)
Bugs found: None
Bugs fixed: None
Bugs open: None
Tests changed: `backend/tests/Ecommerce.Cart.Tests/Application/CartMergeServiceTests.cs` `Snapshot` helper — appended `null` colour arg (compile fix for the new `VariantSnapshot` parameter; no assertion touched). `OrderSeed.SeedItem` gained an optional last `VariantId` (existing calls unchanged); `OrdersEnv` now sets `Catalog:Images:BaseUrl = https://img.example.test`; `tests/order-fixtures.ts` default item gains `image: null, colour: null`. No test weakened or deleted.

## Red output (test-first)
I1/I2/I3/I5 before the Orders change (I4 passes because 403 never had item data, as expected):
```
Failed P2_51_items_keep_their_order_and_each_gets_its_own_variant_data  KeyNotFoundException : The given key was not present in the dictionary.
Failed P2_51_an_item_returns_its_catalog_colour_and_primary_image_url   KeyNotFoundException
Failed P2_51_missing_colour_image_or_catalog_variant_gives_nulls_and_still_200  KeyNotFoundException
Failed P2_51_admin_order_detail_embeds_the_item_colour_and_image         KeyNotFoundException
Failed!  - Failed: 4, Passed: 1, Total: 5
```
(missing `colour`/`image` JSON property). Green after change: all Orders integration tests `Passed! 101/101`.

F1–F3 before `ItemRow` change: `Unable to find an element with accessibility label: Black tee, front` / `role: image, name: Black tee, M` / `text: /Colour:/`; `Tests: 6 failed, 6 total` (web+native projects). Green after: `features/orders` 30 suites, 196 tests passed.

## Changed
Backend: `Catalog.Contracts/IProductReader.cs` (`VariantSnapshot.Colour`), `Catalog/Infrastructure/ProductReader.cs`, `Orders/Application/OrderViews.cs` (`Variants`), `Orders/Application/OrderQueryService.cs` (inject `IProductReader`, lookup after ownership check), `Orders/Api/OrderResponses.cs` (`OrderDetailItemResponse`; checkout `OrderItemResponse` untouched).
Tests: `OrdersReadApiTests.cs` (I1–I5, `CatalogVariantAsync` helper), `OrderSeed.cs`, `CartMergeServiceTests.cs`.
Frontend: `features/orders/types.ts` (`OrderDetailItem`), `tests/order-fixtures.ts`, `OrderDetailView.tsx` (`ItemRow`: `ProductImageView` width `touch`, colour line, row label), new `OrderDetailView.items.test.tsx` (F1–F3).
Docs: `docs/admin-dashboard-feature-tracker.md` P2.51 Todo -> Done. No migration, no new package, no git writes.

verify.sh: `VERIFY PASS (secrets hygiene format build test drift expo-check tsc jest export)` — build 0 warnings, jest 262 suites / 1989 tests, drift clean.

Quality-bar §2: all boxes met for a Low-tier read-only slice. Mutations N/A (Low tier, no money/security guard added; the ownership check is untouched and I4 covers no item data on 403). Migration N/A (no schema change). Money untouched (snapshot sku/name/unitPrice/lineTotal unchanged, asserted in I2).
