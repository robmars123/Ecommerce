# Deliverable 4.2 — Review thread

Shipping methods API: `GET /api/v1/shipping-methods` and the flat-rate shipping it feeds into checkout totals (Orders · Slice 4).

## Round 1 — Rob → Marco (for Nathaniel)
Verdict: BLOCKED · verify.sh: `VERIFY PASS (secrets hygiene build test drift expo-check tsc jest export)` (build 0 errors / 0 warnings; jest 149 suites / 982 tests; drift clean)

Scope: the committed code on `dev` at `463c858`, read fresh. Files: `Application/ShippingMethods.cs`, `Api/CheckoutController.cs:22-32`, `Api/OrderResponses.cs:94-96`, `Infrastructure/CheckoutOptions.cs`, `OrdersModule.cs:29-47`, `Application/CheckoutService.cs:57-64, 160-165`, and the tests in `Ecommerce.Api.IntegrationTests/Checkout/CheckoutTests.cs` and `Ecommerce.Orders.Tests/Domain/OrderTotalsTests.cs`. I widened to `Ecommerce.Api.IntegrationTests` because the endpoint tests live there. The design is right and small: config only, validated on start, served by a thin action, and the price is never read from the client. Two money guards have no test, though. When I broke each of them, every test stayed green. That is why this is BLOCKED.

### Invariants → code → tests

| Invariant | Where it holds | Tests that prove it |
|---|---|---|
| Server owns money (§3.4 rule 1): shipping comes from config, never from the client | `CheckoutService.cs:57,160` looks the method up by code and uses `method.Amount`. The request DTO has no amount field, and strict binding rejects one | `T5_a_body_carrying_a_price…` (`shippingTotal`), `T23_an_unknown_method…client_money_fields…` (`shippingAmount`, `total`), 400 and nothing created |
| §7.4 config rules: amount ≥ 0 and at most 2 dp, codes unique and `^[a-z0-9-]+$`, name 1–100 | `CheckoutOptions.cs:64-87`, `ValidateOnStart` | **None** (finding 1) |
| `shipping_total` = the chosen method's amount; intent = `grand_total` (test 23) | `CheckoutService.cs:160`, `OrderTotals.Compute` | T5 and T23 use one method (5.00) only. Charging the chosen method is not pinned (finding 2) |
| §9 contract `{ items: [{ code, name, price, estimatedDays }] }` | `CheckoutController.cs:29-32` | `Shipping_methods_lists_the_configured_flat_rates` |
| Auth: Customer (any signed-in user, Slice 3 D1) | class-level `[Authorize]` | `The_three_endpoints_answer_401_to_an_anonymous_caller` |
| Unknown method or country → 400 before any lock | `CheckoutService.cs:57-64` | T23 (`errors.shippingMethodCode`, `errors.shippingAddress.country`, 0 orders, 0 reservations, 0 intents) |
| Thin controller · no `var` · IClock · boundaries (§6.2) | The action is a mapping only. No `var` and no `DateTime.UtcNow` in these files. The csproj references SharedKernel, Persistence and `*.Contracts` only. All types are `internal` | ArchitectureTests 25/25 in verify |

403 does not apply: every signed-in user may read the list, and there is no owner. 400 does not apply either: the GET takes no body, route value or query. I accept that the endpoint has no 403 or 400 test.

### Mutation checks (each guard broken, run, then restored from a scratch copy; `cmp` matches the originals; `git diff --stat -- backend` is empty afterwards)

| # | Guard broken | Result |
|---|---|---|
| M1 | `CheckoutOptions.cs:79`: removed `m.Amount < 0m` | **GREEN**: Orders.Tests `Failed: 0, Passed: 201`. No integration config uses a bad amount |
| M1b | `CheckoutOptions.cs:79`: removed the 2-dp check | **GREEN**: `Failed: 0, Passed: 201` |
| M2 | `CheckoutOptions.cs:71`: duplicate-code guard disabled (`&& false`) | **GREEN**: `Failed: 0, Passed: 201` |
| M3 | `CheckoutService.cs:160`: `method.Amount` → `shipping.All[0].Amount` (charge the first method whatever was chosen) | **GREEN**: Checkout integration `Failed: 0, Passed: 40`, three runs in a row |
| M4 | `CheckoutController.cs:29`: `[AllowAnonymous]` on the action | RED: `The_three_endpoints_answer_401…`. `Failed: 1, Passed: 39` |
| M5 | `CheckoutController.cs:32`: price `m.Amount` → `0m` | RED: `Shipping_methods_lists_the_configured_flat_rates`. `Failed: 1, Passed: 39` |

### 1. The shipping config validator has no tests — BLOCKING   (`Infrastructure/CheckoutOptions.cs:64-87`)
**What I saw:** `CheckoutOptionsValidator` holds the §7.4 rules for shipping methods: amount ≥ 0 with at most two decimals, unique codes, the code pattern, name length and days ≥ 0. It also holds the rules for currency, TTL and tax rate. No test calls it. M1, M1b and M2 each removed a rule, and all 201 Orders tests still passed. The carrier validator next to it does have tests (`ShippingCarriersTests.cs:30-70`), so this one is the gap.
**Why it matters:** these checks are the only thing between an owner's config typo and the customer's total. Suppose a refactor drops the `< 0m` check and the owner enters `"Amount": "-5.00"`. The app starts, `GET /shipping-methods` shows −5.00, and every order is 5.00 under its true `grand_total`, all the way to the PaymentIntent. Suppose instead the 2-dp check goes and the amount is `4.999`. Then `shipping_total` breaks the §7.4 "stored at 2 decimal places" rule, and the minor-unit conversion can charge a cent that the order never shows. If the duplicate check goes, `Find` silently picks the first of two `standard` rows, so the menu shows two prices for one code.
**What I want:** add `Ecommerce.Orders.Tests/Infrastructure/CheckoutOptionsValidatorTests.cs` in the same shape as `ShippingCarriersTests`, with these cases. A good config passes. Empty method and country lists pass. Each of these fails: amount −0.01, amount 4.999, a duplicate code, the codes `Standard`, `a b` and `""`, a name of 0 and of 101 characters, `EstimatedDays` −1, country `us`, currency `usd`, `Tax:RatePercent` −1 and 100.01, TTL 0 and 121.
**How I'll check:** I re-apply M1, M1b and M2, and each one must turn a test red.
**Lesson:** a config validator that guards money is a money guard. It gets a test and a mutation check like any other.

### 2. Nothing proves the order is charged for the method the customer chose — BLOCKING   (`Application/CheckoutService.cs:160`)
**What I saw:** every checkout test that looks at totals runs with one configured method (`standard`, 5.00). The only test that configures `express` (`T2_the_same_key_with_another_body…`) asserts a 409 and never places an express order. With M3, every order is charged the first configured rate, and all 40 checkout tests pass.
**Why it matters:** test 23 requires `shipping_total` to match §7.4, which means the chosen method's amount. Suppose a refactor picks the method from a default, or indexes the list by mistake. A customer who picks Express at 12.00 is charged 5.00, or the other way round. The Stripe amount then differs from the price on the menu, and no test notices.
**What I want:** add one integration test in `CheckoutTests.cs`, for example `T23_the_chosen_method_sets_the_shipping_total_and_the_intent`. Configure `standard` 5.00 and `express` 12.00. Add one 10.00 item and check out with `CheckoutEnv.Body("express")`. Assert `shippingTotal` 12.00, `grandTotal` 22.00, the intent `AmountMinor` 2200, `shipping_method_code` `express` with the name `Express` on the order, and `SELECT amount FROM payments` = 22.00.
**How I'll check:** M3 must turn it red.
**Lesson:** a test with one option cannot prove that the choice matters. Configure at least two values whenever the code selects one.

### 3. The list test does not pin order or more than one item — NOTE   (`CheckoutTests.cs:240`)
`Shipping_methods_lists_the_configured_flat_rates` uses `Assert.Single`. If the test from finding 2 shares its two-method config, assert that both items come back in config order. The F10 checkout screen shows them in that order. This is non-blocking.

### 4. One cold-run flake while testing mutations — NOTE
My first Checkout integration run under M3 (with a build) reported `Failed: 4, Passed: 36`. The next three runs of the same code, and verify.sh, all passed. I did not keep that output, so I cannot name the tests or the cause, and it is not a finding against 4.2. If it shows up again in your runs, keep the failing test names.

### Decisions
- None new. Shipping and tax follow the existing decision-log row "Shipping methods and tax (v1)". Ruling: the 401 test is enough auth coverage for `GET /shipping-methods`. 403 and 400 do not apply (see the end of the table).

**What was good:** shipping amounts are fixed in one place, validated on start, and turned into immutable `ShippingMethod` records. The action only maps them, and the `MoneyResponse` currency comes from the same `Store:Currency` the totals use. The unknown-method and country checks run before the idempotency key or any lock, and T23 proves that nothing is created.

### Can 4.2 move to Done?
No. Findings 1 and 2 must be fixed and re-checked with the mutations first. `docs/ecommerce-app-context.md:72` stays **Review open**.

## Round 1 — Marco → Rob

**1. Fixed.** Added `Ecommerce.Orders.Tests/Infrastructure/CheckoutOptionsValidatorTests.cs` (test only, no production change). It has every case you listed: valid config passes, empty lists pass; fails for amount -0.01 and 4.999, duplicate code, codes `Standard`, `a b` and `""`, name length 0 and 101, `EstimatedDays` -1, country `us`, currency `usd`, tax -1 and 100.01, TTL 0 and 121. Orders.Tests: 201 -> 218, all pass.

**2. Fixed.** `CheckoutTests.cs` `T23_the_chosen_method_sets_the_shipping_total_and_the_intent`: `standard` 5.00 plus `express` 12.00, one 10.00 item, checkout with `express`. It asserts shippingTotal 12.00, grandTotal 22.00, intent AmountMinor 2200, `payments.amount` 22.00, and order detail `shippingMethod` code `express` / name `Express`.

**3. Fixed (cheap).** Added `Shipping_methods_come_back_in_configuration_order` (two methods; order `standard, express`; second price 12.00). I left `Shipping_methods_lists_the_configured_flat_rates` unchanged.

**Mutations** (each applied, run, restored from a scratch copy):
| # | Result |
|---|---|
| M1 drop `m.Amount < 0m` | RED: `An_amount_that_is_negative_or_has_more_than_two_decimals_fails(amount: "-0.01")`, Failed 1, Passed 217 |
| M1b drop 2-dp check | RED: `...(amount: "4.999")`, Failed 1, Passed 217 |
| M2 duplicate guard `&& false` | RED: `A_duplicate_method_code_fails`, Failed 1, Passed 217 |
| M3 `method.Amount` -> `shipping.All[0].Amount` | RED: `T23_the_chosen_method_sets_the_shipping_total_and_the_intent`, Failed 1, Passed 20 |

Restored; `git diff --stat -- backend/src` is empty.

**verify.sh:** `VERIFY PASS (secrets hygiene build test drift expo-check tsc jest export)`. Build 0 errors / 0 warnings; jest 149 suites / 982 tests. Checkout integration class: 22 tests (21 before the finding 3 test), all pass.

**Concerns:** None. No production bug was revealed. I did not see the cold-run flake from finding 4.

## Round 2 — Rob → Marco
Verdict: PASS · verify.sh: `VERIFY PASS (secrets hygiene build test drift expo-check tsc jest export)` (build 0 errors / 0 warnings; ArchitectureTests 25/25; jest 149 suites / 982 tests; drift clean)

I checked only the three items from Round 1 and the new test code. The diff under `backend/` is test-only: `Ecommerce.Orders.Tests/Infrastructure/CheckoutOptionsValidatorTests.cs` (new) and +43 lines in `CheckoutTests.cs`. `git diff --stat -- backend/src` is empty. The `TopNav` / `useCartCount` frontend changes in the working tree are not part of 4.2, and I did not review them here.

### Mutation re-checks (each guard broken, run, restored from a scratch copy, `cmp` matches the original, `git diff --stat -- backend/src` empty after each)

| # | Guard broken | Result |
|---|---|---|
| M1 | `CheckoutOptions.cs:79`: dropped `m.Amount < 0m` | RED: `An_amount_that_is_negative_or_has_more_than_two_decimals_fails(amount: "-0.01")`. `Failed: 1, Passed: 217` |
| M1b | `CheckoutOptions.cs:79`: dropped the 2-dp check | RED: `…(amount: "4.999")`. `Failed: 1, Passed: 217` |
| M2 | `CheckoutOptions.cs:71`: `!seen.Add(m.Code) && false` | RED: `A_duplicate_method_code_fails`. `Failed: 1, Passed: 217` |
| M3 | `CheckoutService.cs:160`: `method.Amount` → `shipping.All[0].Amount` | RED: `T23_the_chosen_method_sets_the_shipping_total_and_the_intent`. `Failed: 1, Passed: 20` |
| M6 (mine) | `CheckoutController.cs:32`: `shipping.All.Reverse().Select(…)` | RED: `Shipping_methods_come_back_in_configuration_order`. `Failed: 1, Passed: 20` |

### Your replies

**1. Confirmed.** Every case I listed is present: valid passes; empty lists pass; −0.01, 4.999, duplicate code, `Standard` / `a b` / `""`, name 0 and 101, days −1, `us`, `usd`, tax −1 and 100.01, TTL 0 and 121 all fail. Each failing case starts from `Valid()` (which is asserted to pass) and changes exactly one field. That means a red result can only come from the rule under test, which is why M1, M1b and M2 each turn exactly one test red. Orders.Tests went from 201 to 218, with no production change.

**2. Confirmed.** T23 configures `standard` 5.00 and `express` 12.00, checks out with `express`, and pins all four places the money shows up: response `shippingTotal` 12.00 / `grandTotal` 22.00, intent `AmountMinor` 2200, `payments.amount` 22.00, and code `express` / name `Express` on the order detail. With M3 the test goes red.

**3. Confirmed.** `Shipping_methods_come_back_in_configuration_order` asserts the code sequence `standard, express` and the second price, 12.00. I added M6 (reversing the list) to prove that it pins the order and not only membership, and it goes red. Keeping `…lists_the_configured_flat_rates` unchanged is fine, since it still pins the single-item contract fields.

### 1. The test count in your report is off by one — NOTE
You wrote "Checkout integration class: 22 tests (21 before the finding 3 test)". `CheckoutTests` has 19 `[Fact]`s at `HEAD` and 21 now, and my filtered run reports `Total: 21`. Your work is correct, but please quote counts from the output rather than from memory.

### 2. Cold-run flake (Round 1 note 4) — NOTE
I saw no flake this round: two filtered integration runs and the full verify all passed. I am closing the note.

**What was good:** the single-field-delta shape of the validator tests makes every red result attributable to one rule. T23 asserts the amount at every hop (response, intent, payments row, order detail), so a bug in any one layer cannot hide behind the others.

### Can 4.2 move to Done?
Yes. `docs/ecommerce-app-context.md:72` is now **Done**.
