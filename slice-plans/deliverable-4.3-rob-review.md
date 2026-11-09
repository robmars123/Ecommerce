# Deliverable 4.3 — Review thread

Checkout API: `POST /api/v1/checkout` with an inline address, server-computed totals and an `Idempotency-Key` (Orders · Slice 4).

## Round 1 — Rob → Marco (for Nathaniel)
Verdict: BLOCKED · verify.sh: `VERIFY FAIL: test` twice in a row (build 0 errors / 0 warnings; jest 149 suites / 982 tests; drift clean; Api.IntegrationTests `Failed: 1, Passed: 381` both times, a different test each time, see finding 2)

Scope: the committed code on `dev`, read fresh: `Application/CheckoutService.cs`, `Api/CheckoutController.cs:34-97`, `Api/CheckoutRequests.cs`, `Domain/OrderTotals.cs`, `Application/ShippingMethods.cs`, `Persistence/EfUnitOfWork.cs:28-55` and `Idempotency/IdempotencyKeyEntry.cs:38-60` (the transaction and key store checkout relies on), plus the tests in `Ecommerce.Api.IntegrationTests/Checkout/CheckoutTests.cs`. The working-tree diff in `CheckoutTests.cs` is the 4.2 fix and is not re-reviewed. The design is right: the key row is flushed first, so a concurrent duplicate waits on the key rather than the stock lock. A rejection rolls the key back, the provider is called only after commit, and a replay heals a missing intent. One idempotency guard has no test, though, and the gate is not green.

### Invariants → code → tests

| Invariant | Where it holds | Tests that prove it |
|---|---|---|
| Server owns money (§3.4 rule 1): prices re-read from Catalog, totals per §7.4, intent = `grand_total` | `CheckoutService.cs:117-160`, `OrderTotals.Compute`; strict binding (`Program.cs:38`) | T5 ×2, T23 tax rounding (10.05 × 10 % → 1.01, intent 1606), T23 chosen method (4.2 fix) |
| Client money or status field → 400, nothing created | `UnmappedMemberHandling.Disallow` + DTO with no money members | T5 body test, T23 400 test (0 orders, 0 reservations, 0 creates) |
| Price snapshot (test 6) | `OrderItem.Create(... snapshots[..].Price ...)` | `T6_a_later_admin_price_change…` |
| One key → at most one order (§3.4 rule 2, §8.2 inv. 1), sequential and concurrent | early flush `:107-108`; `pk_idempotency_keys` catch → `ReplayAsync` `:77-81` | T2 twice in a row, T2 eight concurrent (stock = qty, one key row) |
| Same key + other body or other user → 409 | `Matches` `:192-196` over `HashOf` `:199-204` | T2 409 test: **other method and other user only. No address change** (finding 1) |
| Crash or provider outage after commit → same key heals, no second order | `CompleteAsync` `:172-190` (`EnsureIntentAsync`) | 503 test then heal, Rule6 test (no gateway call inside a transaction) |
| Rejection leaves no key, so the same key can be retried after the cart is fixed | `Rejected` thrown inside the transaction, `ChangeTracker.Clear()` | cart-empty and lines-rejected tests assert 0 `idempotency_keys` |
| Address rules and rule 10 (400 before any lock) | `CheckoutRequestValidator`, `CheckoutService.cs:57-65` | Address rules test (4 fields), T23 400 test |
| Address and method name snapshotted (test 23) | `Order.Place(..., method.Name, command.Address, ...)` | T23 snapshot test (3 of 7 address fields, NOTE 4) |
| Auth: Customer, own user id from the token | class `[Authorize]`, `UserId()` from `sub` | 401 test. 403 does not apply: no resource id in the request, and another user's key → 409 (T2) |
| Thin controller · no `var` · IClock · boundaries (§6.2) | Controller validates, trims and maps. csproj → SharedKernel, Persistence, `*.Contracts`. Only `OrdersModule` is public | ArchitectureTests 25/25 |

### Mutation checks (each guard broken, run, restored from a scratch copy; `cmp` matches the original; `git diff --stat -- backend/src` empty afterwards)

| # | Guard broken | Result |
|---|---|---|
| M1 | `CheckoutService.cs:202`: hash = method code only (address ignored) | **GREEN**: Checkout `Failed: 0, Passed: 42` |
| M2 | `:195`: dropped `seen.UserId == userId` | RED: `T2_the_same_key_with_another_body_or_another_user…` |
| M3 | `:108`: removed the early key flush | RED: `T2_the_same_key_sent_concurrently…` |
| M4 | `:80`: race loser → `Conflict` instead of replay | RED: `T2_the_same_key_sent_concurrently…` |
| M5 | `OrderTotals.cs`: `AwayFromZero` → `ToEven` | RED: Orders.Tests `Failed: 2`; `T23_with_a_tax_rate…` |
| M6 | `Program.cs:38`: `Disallow` → `Skip` | RED: T5 body test and T23 400 test |
| M7 | `ShippingMethods.ShipsTo` always true | RED: `T23_an_unknown_method_a_foreign_country…` |
| M8 | `CheckoutController.cs:34`: `[AllowAnonymous]` on `checkout` | RED: `The_three_endpoints_answer_401…` |
| M9 | `CheckoutService.cs:132`: PriceChanged check disabled | RED: `A_changed_price_and_an_unavailable_item…` |
| M10 | `:86`: a replay returns the order without `EnsureIntentAsync` | RED: T2 ×2, 503-then-heal, Rule6 (`Failed: 4`) |

### 1. Changing only the address under the same key is never tested — BLOCKING   (`CheckoutService.cs:199-204`, `CheckoutTests.cs:88`)
**What I saw:** the "another body" half of the T2 409 test changes the shipping method only. With M1 the hash ignores all seven address fields, and every checkout test still passes.
**Why it matters:** this is the exact retry path §8.2 is built for. Say the customer submits, the response is lost, they notice a typo in the postal code, fix it, and the app resends with the same key (the key is persisted until success). The server should answer 409 so the client mints a new key. With the address out of the hash, the server replays 200 with the first order, and the parcel goes to the wrong address with no error anywhere. §9 says "same key with another body → 409", and the address is most of the body.
**What I want:** in `CheckoutTests.cs`, a test (a `[Theory]` over the field is fine) that places an order with key K, then resends K with the same method and exactly one address field changed: `fullName`, `line1`, `line2`, `city`, `region`, `postalCode`, and `country` (configure a second ship-to country for that case). Each resend → 409, still 1 order, 1 payment, 1 intent. Production code needs no change unless you take NOTE 3.
**How I'll check:** I re-apply M1, and also a variant that drops only `a.Line2` and `a.Region`. Both must go red.
**Lesson:** an idempotency hash is a guard on every field it covers. Test each field, or a refactor can drop one without anyone noticing.

### 2. verify.sh is red: two runs, two different integration failures — BLOCKING   (`tests/Ecommerce.Api.IntegrationTests`)
**What I saw:** first run: `OrderEmailTests.An_override_a_full_refund_and_a_reservation_expiry…` failed with `SocketException: Only one usage of each socket address`. Second run: `ReservationExpiryTests.Only_the_due_orders_are_expired…` failed with `PostgresException 53300: sorry, too many clients already`. A third run of the integration suite on its own passed 382/382. Neither test is checkout code, but both are resource exhaustion, and the 4.2 fix just added two more `CheckoutEnv` hosts. Each host has its own pool (`PostgresFixture.cs:28`, `MaxPoolSize = 8`, idle lifetime 2 s with pruning, so connections churn).
**Why it matters:** the gate decides Done. When it fails 2 runs out of 3, a real regression looks just like noise, and nobody can claim PASS honestly.
**What I want:** find the cause and fix it in the test infrastructure. For example, bound the total connections (hosts in parallel × pool size) below the container's `max_connections`, or stop the pruning churn that uses up ephemeral ports. Don't fix it by retrying, skipping or deleting tests. In your reply, quote the cause you found and three consecutive full `verify.sh` runs ending in `VERIFY PASS`.
**How I'll check:** I run `verify.sh` twice. Both must pass.
**Lesson:** a flaky gate is a broken gate. Treat it as a bug, not as bad luck.

### 3. The request hash is ambiguous across fields — NOTE   (`CheckoutService.cs:202`)
The fields are joined with `\n`, and the validator allows `\n` inside a field. So `fullName "A\nB", line1 "C"` and `fullName "A", line1 "B\nC"` hash the same. Only the same user can do this, to their own key, so it's not blocking. If you touch the hash for finding 1, prefix each field with its length (or hash a serialized record), and add one test for this case.

### 4. The snapshot test pins 3 of 7 address fields — NOTE   (`CheckoutTests.cs:189`)
Test 23 says "address … snapshotted". Send `line2` and `region` in the body and assert all seven fields in the detail. Then a jsonb mapping that drops `city`, `region` or `postalCode` turns a test red.

### 5. The cart is not emptied after checkout or Paid — NOTE (question for Nathaniel)
No doc says when the server cart is cleared, and checkout leaves it full. This is not a 4.3 defect. Nathaniel should decide it (on Paid, via the outbox, is the natural place) before 4.5 ships the screen.

### Decisions
- None new. Rulings: (a) a line in another currency → 400 stands. It is Slice 4 plan D13, and the test `A_cart_line_priced_in_another_currency_is_a_400` pins it. (b) The key's primary key is `(scope, key)`, not per user, so another user's key → 409 (§9). (c) 403 does not apply to `POST /checkout` (no resource id). The 401 test and the other-user 409 cover auth.

**What was good:** the early key flush, with replay on `pk_idempotency_keys`, makes eight concurrent duplicates return one identical body, and the stock = quantity setup proves none of them re-reserved. M3, M4 and M10 all go red. `ChangeTracker.Clear()` plus `AsNoTracking` on the key read means the replay after a unique violation can't see the failed attempt's staged row.

### Can 4.3 move to Done?
No. Findings 1 and 2 must be fixed and re-checked first. `docs/ecommerce-app-context.md:73` stays **Review open**.

## Round 1 — Marco → Rob

**1. Fixed.** `CheckoutTests.cs`: `T2_the_same_key_with_one_changed_address_field_is_a_409_and_creates_nothing` is a `[Theory]` over fullName, line1, line2, city, region, postalCode and country. The first order carries all 7 fields (line2 and region set). The resend changes exactly one field. Each resend must be 409, with 1 order, 1 payment and 1 intent. Country uses a second ship-to country (`Shipping:Countries:1=CA`). Real code: 7/7 green.
- M1 (hash = method code only): red, `Failed: 7, Passed: 0` (all seven fields).
- Variant dropping only `a.Line2` and `a.Region`: red, `Failed: 2, Passed: 5` (`field: "line2"`, `field: "region"`).
- I restored `CheckoutService.cs` from a scratch copy, `cmp` matched, and `git diff --stat -- backend/src` was empty at that point. (It now shows the item-3 change, below.)

**2. Fixed. Cause: real, not a concurrent run.** I can't prove what else was running during your runs, so I measured the suite on its own. xunit v3 runs one test class per CPU in parallel (24 here), and each host's pool holds up to 8 connections. Worst case is 24 x 8 = 192 against Postgres's 100. Sampling `pg_stat_activity` during one solo run showed a peak of 83 of 100, even with a slow sampler. Port churn on the Docker proxy adds the `SocketException`. Fix: `PostgresFixture.cs`, `[assembly: CollectionBehavior(MaxParallelThreads = 8)]`, so at most 64 connections. The pool settings are unchanged, no test was touched, and the suite duration is the same (34 s). Three consecutive full runs of `bash scripts/verify.sh 2>&1 | tail -n 30`, each ending `VERIFY PASS (secrets hygiene build test drift expo-check tsc jest export)`: run 1 PASS, run 2 PASS, run 3 PASS. Build 0 errors / 0 warnings; jest 149 suites / 982 tests. The integration suite alone passed 389/389 before the final run.

**3. Fixed (small).** `CheckoutService.cs` `HashOf` now length-prefixes each field (`{len}:{value}`) instead of joining with `\n`. New test `T2_fields_that_differ_only_by_where_a_newline_falls_are_another_body_and_a_409` failed before the change (`Assert.Equal() Failure`) and passes now. A stored hash from before this change would 409 on replay, which is harmless pre-release.

**4. Fixed.** The T23 snapshot test now sends line2 and region and asserts all 7 address fields in the order detail. It was already green on the real code. I did not run a jsonb mutation on it.

Tests changed: the T23 snapshot test is strengthened. Nothing was weakened or removed. Nothing in `docs/ecommerce-app-context.md` was edited.

## Round 2 — Rob → Marco (for Nathaniel)
Verdict: PASS · verify.sh: `VERIFY PASS (secrets hygiene build test drift expo-check tsc jest export)` on both of my runs, back to back (build 0 errors / 0 warnings, jest 149 suites / 982 tests, drift clean)

I re-checked only the two blocking items, your `HashOf` change and the tests you changed. I did not re-review the rest of 4.3. Both blockers are closed, and I found no new bug.

### Your replies

**1. Address fields in the hash: Confirmed.** I read `T2_the_same_key_with_one_changed_address_field_is_a_409_and_creates_nothing`. The first order carries all seven fields. Each case changes exactly one, and it asserts 409, 1 order, 1 payment and 1 intent. `country` uses the second ship-to country (`Shipping:Countries:1=CA`), so the 409 really comes from the hash, not from a 400 on the country rule. My mutations, each restored from a scratch copy:
- M1 (hash = `c.ShippingMethodCode` only): RED, `Failed: 8, Passed: 21`. All seven theory cases fail, plus the new newline test.
- Variant with `a.Line2` and `a.Region` dropped: RED, `Failed: 2, Passed: 27`. The failures are `(field: "line2")` and `(field: "region")`, nothing else.

**2. Flaky gate: Confirmed. The cause is supported and the fix is sound.** `PostgresFixture.CreateDatabaseAsync` gives every test its own database, so every test host gets its own pool of up to 8, all on one container with the default `max_connections` of 100. This machine has 24 cores (`nproc` = 24), so the old worst case was 24 × 8 = 192. Your `pg_stat_activity` peak of 83 shows the margin was already thin, and the `53300` I saw fits that. `MaxParallelThreads = 8` limits the live demand to 64. It is a capacity bound, not a retry: no test was skipped, retried or touched, and the pool settings are unchanged. It is one attribute line plus a comment explaining why. My two full runs were both green, at 56 s and 58 s for the test step.

**3. HashOf length prefix: Confirmed.** The diff in `backend/src` is the `HashOf` summary line and its body, nothing else. `{len}:{value}` per field, in a fixed order, can be decoded only one way, so two different bodies can no longer produce the same text. Every field is non-null, because `Line2` and `Region` go through `?? ""`, so `.Length` cannot throw. Mutation M11: I reverted line 202 to the `string.Join('\n', …)` from `HEAD`, and only `T2_fields_that_differ_only_by_where_a_newline_falls_are_another_body_and_a_409` failed (`Failed: 1, Passed: 28`). That is the right test, and only that test. I restored the file from the scratch copy, `cmp` matched, and `git diff -- backend/src` shows only your two changed lines. I accept your note that a hash stored before this change now gives 409 on replay: there is no release yet, and 409 is the safe direction.

**4. T23 snapshot test, reviewed as a changed test: Confirmed, it is stronger.** Before, it placed the order through `PlaceOrderAsync` and checked `fullName`, `line1` and `country`. Now it sends all seven fields, including `line2` and `region`, and the `foreach` checks each one against the detail response. The method code and name checks are unchanged. Nothing was removed. It is a superset of the old test.

### 1. The connection bound is a measured margin, not a guarantee — NOTE   (`PostgresFixture.cs:8`)
64 live connections plus idle pools that linger for 2 s still sits under 100, but nothing enforces that. If someone adds a test with a wider pool, or raises `MaxParallelThreads`, the flake comes back. If it does, treat it as finding 2 again: measure, then bound. Not blocking. It is green twice here and three times for you.

### 2. Untracked `Ecommerce.Orders.Tests/Infrastructure/CheckoutOptionsValidatorTests.cs` — NOTE
It is untracked, but not part of this fix round, so I did not review it here. Nathaniel: commit it with the deliverable that owns it, or have it reviewed there.

**What was good:** each fix comes with a test that fails without it. The theory has exactly one case per hashed field, so a failure names the field that was dropped. That is why the line2/region variant failed on exactly two cases.

### Can 4.3 move to Done?
Yes. `docs/ecommerce-app-context.md:73` is now **Done**. NOTE 5 from Round 1, about when the cart is cleared, is still open for Nathaniel to decide before 4.5.
