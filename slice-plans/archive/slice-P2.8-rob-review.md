# Slice P2.8 — Review thread

## Round 1 — Rob → Marco
Verdict: BLOCKED · verify.sh: not run (not a PASS round). Targeted: `AdminSearchApiTests` baseline `Passed: 15, Failed: 0`.

The production code is clean and matches the plan: the controller is thin, the Support guard sits in the service and Catalog is never called for Support, the three reads are awaited one after another, the ILIKE patterns are parameterised with `ESCAPE '\'`, enum status is mapped after materialising, there is no `var`, records are named, contracts are public and implementations internal. The one problem is in the escape test: it proves the SKU escape through a row the domain can never produce, and it does not cover the order search's own copy of the escape at all.

### 1. I9 tests an impossible state and leaves `OrderSearch` escaping unpinned — BLOCKING   (`backend/tests/Ecommerce.Api.IntegrationTests/Admin/AdminSearchApiTests.cs:196-211`)
**What I saw:** You seed `AAB-1`, then raw-SQL `UPDATE` it to `A_B-1`. `Product.AddVariant` only allows `A-Z 0-9 -` (`Product.cs:69-71`), so no SKU with `_` can exist in a real database. Separately, `OrderSearch.Contains` is a **second copy** of the escape helper, and no test looks at the `orders` group under a wildcard query. The only orders assertion is for `q=\z`, which returns nothing whether or not the escape is there.
**Why it matters:** (a) A test fixture that writes a state the domain forbids becomes a trap. If someone later adds a DB check constraint matching the domain rule, I9 fails for a reason unrelated to search. It also hides what the test actually proves. My M3 run shows the red comes from `AXB-1` wrongly matching `a_b` (`Expected "A_B-1", Actual "AXB-1"`). That is a false positive, and it reproduces without the `A_B-1` row. (b) If someone drops `.Replace("_", "\\_")` or `.Replace("%", "\\%")` from `OrderSearch`, every test stays green. A staff user typing `0_` would then get every order whose number contains a `0` followed by any character. The plan said "integration tests pin it" for both call sites, but for orders they don't. That one is my gap in the plan, and it still has to be closed in this slice.
**What I want:**
- Remove the raw-SQL `UPDATE`. Exercise `_` where the domain allows it, in product names: seed products "a_b Tee" and "aXb Tee" → `q=a_b` → products are exactly `["a_b Tee"]`.
- Keep the SKU escape pinned with valid data only: with `AXB-1` seeded, `q=a_b` → `skus` is `[]`. (A SKU can't contain `_`, so the correct answer is "no SKU", and without the escape `AXB-1` leaks in.)
- Keep `q=0%25` → products `["50% Off Tee"]` (not "500 Club Tee").
- Add orders: seed an order such as `ORD-I9-000123`. Then `q=0%25` → `orders` is `[]`, and `q=0_` → `orders` is `[]` (without the escape, both match that order).
- Keep the `q=%5Cz` → 200, all groups empty check.
**How I'll check:** I'll re-run M3 (drop `_` escape in `CatalogSearch`): I9 red. I'll also run one mutation on the same guard in `OrderSearch` (drop `.Replace("_", "\\_")`): I9 red. In your fix report, quote red for both, and `grep -n "UPDATE" AdminSearchApiTests.cs` must return nothing.
**Lesson:** Test escapes through false positives on legal data. Every copy of a guard needs its own test.

### 2. `error!` null-forgiving in the controller — NOTE   (`AdminSearchController.cs:30`)
`TryParse` could declare `[NotNullWhen(false)] out string? error` so the `!` goes away. It's harmless today. Logged to backlog.

### 3. LIKE-escape helper now exists three times (Identity, Orders, Catalog) — NOTE
This was accepted in the plan (two new call sites). If a fourth appears, extract it to SharedKernel with a unit test. Logged to backlog.

### Answers to the coordinator's points
- **I9 deviation:** not acceptable as written. Decision: exercise `_` through product names, and pin the SKU escape through the `AXB-1` false positive (item 1). The `_` escape on SKUs is still meaningful even though SKUs can't contain `_`: the term comes from the user, and an unescaped `_` matches any character.
- **Contract:** matches the §13.2 row (including the Server part) and `features/admin-shell/types.ts` exactly: root `orders/products/skus`, items `{id, orderNumber, status}`, `{id, name}`, `{sku, productId, productName}`. I8 pins the exact name sets and checks that no money or email field appears. `status` is `OrderStatus.ToString()`, and the enum names match the TS `OrderStatus` union.
- **§3.4:** Admin uses only `Orders.Contracts` and `Catalog.Contracts`. The variant–product join is inside Catalog. There is no cross-module DbSet, no `var`, no anonymous types, no `DateTime.UtcNow`, and no external call.
- **Dashboard flake:** confirmed pre-existing and outside this diff (not in `git diff --stat`). It wasn't in the backlog, so I've added it now.

### Mutations (Rob)
- M1 `IsInRole(Admin)` → `true`: `I6_Support_never_gets_products_or_skus... [FAIL]`, Failed 1 / Passed 14. Restored, `cmp` clean.
- M2 policy → `[Authorize]`: `I2_Customer_gets_403_and_staff_get_200 [FAIL]`, Failed 1 / Passed 14. Restored, `cmp` clean.
- M3 drop `_` escape in `CatalogSearch`: `I9 [FAIL]` (`Expected "A_B-1", Actual "AXB-1"`), Failed 1 / Passed 14. Restored, `cmp` clean.

**What was good:** You put the Support guard in the service, so Catalog isn't even called for Support (U4 asserts zero calls), and that is stronger than filtering afterwards. I8 asserts the exact property-name set, which catches contract drift at the boundary instead of in the UI.

## Round 2 — Rob → Marco
Verdict: PASS · verify.sh: VERIFY PASS (secrets hygiene build test drift expo-check tsc jest export); jest 1445 passed; no dashboard flake hit.

### 1. I9 `_` and order-number coverage — Confirmed
I read the test (AdminSearchApiTests.cs:195-215). The raw-SQL UPDATE is gone. `a_b` returns only "a_b Tee" with empty skus (AXB-1 and "aXb Tee" are seeded as decoys). Order ORD-I9-000123 is checked with `0%25` and `0_` and both return empty orders. The `0_` query would match "00" if `_` were unescaped, so it does exercise the guard.

Mutations (scratch copy, break, red, restore, `cmp` clean):
- M3, drop `.Replace("_", ...)` in CatalogSearch: I9 red (`Assert.Equal` collections differ).
- Same drop in OrderSearch: I9 red (`Assert.Empty` failed, collection not empty).
Both files restored and byte-identical to the scratch copies. My first sed attempt did not match, so those runs were green and were discarded. The second pass confirmed the line was removed (grep count 0) before testing.

No production code changed in this round. Other tests in the file are unchanged in intent.
