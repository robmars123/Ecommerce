# Slice P2.51 — Review thread

## Round 1 — Rob → Marco
Verdict: PASS · verify.sh: `VERIFY PASS (secrets hygiene format build test drift expo-check tsc jest export)` — build 0 warnings, ArchitectureTests 25/25, jest 262 suites / 1989 tests, drift clean.

This is a small additive read slice and it matches the plan. Orders gets display data from Catalog only through `IProductReader`, the checkout contract is untouched, and every null case falls back to `null` without an error. Tier is Low, so I read the diff and ran no mutations.

### Checks I made (all confirmed in the code, not taken from the report)
- **Boundary (rule 8):** `OrderQueryService` injects `IProductReader` (from `Catalog.Contracts`, which the Orders csproj already referenced). Orders has no `DbSet` or join on Catalog. The only cross-module type in Orders is `VariantSnapshot`/`CartImage` from Contracts. ArchitectureTests pass 25/25 and none of them changed.
- **Ownership first:** `GetAsync` returns `NotFound`/`Forbidden` before it calls `BuildAsync`, and `BuildAsync` is the only place that calls `GetVariantSnapshotsAsync` (`OrderQueryService.cs:83`). So a 403 or 404 never queries Catalog. I4 seeds B's order with a Catalog image and colour, reads it with A's token, and asserts 403 with no `img.example.test` and no `Black` in the body.
- **Per-line data:** the mapping looks up `view.Variants` by each item's own `VariantId` (`OrderResponses.cs:83`), using `TryGetValue` and passing `v?.Image, v?.Colour`. I3 checks each line against its own `variantId`, so data can't bleed across lines. The `Distinct()` before the lookup keeps `ToDictionary` safe when the same variant appears twice.
- **Nulls:** an unknown variant is missing from the dictionary, so both fields are null. A variant with no image gets a null `Image` from Catalog, and one with no colour gets a null `Colour`. I2 covers the bare variant and the unknown variant in one order, still 200, and money (`20.00`/`40.00`) is unchanged. On the client, the `ProductImageView` placeholder uses `fallbackLabel={item.name}` and the colour line renders only when `colour` is truthy (F2 and F3).
- **No real domains:** tests use `https://img.example.test` (backend) and `https://img.test.invalid` (Jest). Committed `appsettings.json` `Catalog:Images:BaseUrl` is still `""` (line 18).
- **Changed test helpers (4), none weakened:**
  - `CartMergeServiceTests.Snapshot` only appends a `null` colour arg. No assertion changed.
  - `OrderSeed.SeedItem` gains an optional last `VariantId` that defaults to the old `Guid.NewGuid()`, so existing callers get identical data.
  - `OrdersEnv` adds an image base URL. No existing test asserts on images, and T29/P2_50 still pass.
  - `order-fixtures.ts` only adds `image: null, colour: null`.
- **Checkout contract:** `OrderItemResponse` and the frontend `OrderItem` are unchanged. The new `OrderDetailItemResponse`/`OrderDetailItem` are used only by the detail views, as the plan decided.
- **Tokens:** the thumbnail uses `width={touch}` (no pixel literal), and the text uses existing classes.

### 1. F1 doesn't check the image source — NOTE   (`OrderDetailView.items.test.tsx:12`)
**What I saw:** F1 finds the thumbnail by `Black tee, front` but never checks its `source.uri`. The plan asked for both.
**Why it matters:** if a regression rendered the placeholder box with the alt text as its label, F1 would still pass. Today the backend tests and the component itself cover this path, so it isn't blocking.
**What I want:** add a `source` prop assertion the next time you touch the file (logged in backlog).
**Lesson:** when a test claims to show an image, assert the thing that makes it an image, not only its label.

**What was good:** you used `TryGetValue` plus nullable propagation in one place, which avoids a separate "display" type, and every `null` path stays in the mapping. I2 also asserts that money is unchanged next to the new nulls, which proves this display slice doesn't touch the money snapshot.
