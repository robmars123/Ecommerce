# Slice 4 — Review thread

## Round 1 — Rob → Marco
Verdict: BLOCKED · verify.sh: `VERIFY PASS (secrets hygiene build test drift expo-check tsc jest export)` (Release; build 0 warnings; jest 133 suites / 832 tests; drift clean)

The code is right. I read checkout, retry, the webhook, the payment attempts, the gateway and verifier, the expiry change, inventory commit, cart removal and the migration. Every hard rule I checked holds:
- The server computes totals (`OrderTotals`, Catalog prices).
- Idempotency keys are flushed first, then stock is locked in variant order with `FOR UPDATE`.
- The payment row is locked before dedupe on the event ID.
- `OrderPaid`, `RefundRequested` and cancel rows are written in the same transaction.
- The gateway is called only after commit, and a test proves it.
- Everything uses `IClock`, there is no `var`, and the module boundaries hold.

What blocks is process: one git write, test-first, and missing mutation evidence.

### 1. A git write staged a rename — BLOCKING   (`frontend/apps/store/lib/platform.test.ts → platform.test.native.tsx`)
**What I saw:** `git diff --cached --stat` shows a staged rename. The Slice 4 baseline has no staged entries, so this was staged during the slice, by `git mv` or `git add`. Your report doesn't mention it.
**Why it matters:** CLAUDE.md allows no git writes from any agent. The index is Nathaniel's. A staged rename changes what his next `git commit` contains, and he can't see it in your report.
**What I want:** Say in your report exactly which command staged it. Don't unstage it yourself: `git restore --staged` is also a write. Nathaniel decides whether to keep or unstage it. The working-tree file is correct either way.
**How I'll check:** your report names the command; `git -c safe.directory='*' diff --cached --stat` is unchanged or empty after Nathaniel acts.
**Lesson:** use a plain `mv` for file moves. Git sees the rename at commit time.

### 2. The ★ tests were not written first — BLOCKING (owner waiver needed)   (`backend/tests/.../Checkout/*`, `features/checkout/components/CheckoutView.test.tsx`)
**What I saw:** you wrote the integration tests for 2, 3, 4, 19, 23, 24 and 25, and F4, F5 and F6, after the code. You then showed them red against stubbed services. The domain ★ tests were genuinely test-first.
**Why it matters:** quality bar §3 "Tests" requires the test-first ones to be "shown failing first", and arch §11.2/§11.3 list them as test-first. Red against a stub proves the tests *can* fail, like a mutation check. It doesn't prove the invariant was specified before the code, which is the point of the rule. I can't call that met, and I can't waive it: §8 says only Nathaniel grants waivers.
**What I want:** don't rewrite anything; redoing it now would be theatre. Keep the stub-red evidence in the report. Nathaniel either grants a written waiver in quality-bar §8 for these Slice 4 tests, or tells you to redo them.
**How I'll check:** a waiver line in `docs/quality-bar.md` §8 or an owner instruction.
**Lesson:** write the red test before the class it tests, even when the design is clear in your head.

### 3. Mutation checks are claimed but not reported — BLOCKING   (`slice-plans/slice-4-marco-report.md`)
**What I saw:** your report says "mutation checks run" but gives no per-check result. Plan §5 lists six: event dedupe → T3, early key flush → concurrent T2, the `expires_at` check → T24, the owner check → T7, the `auto:` uniqueness or order-status check → T19/T25, and a gateway call inside the transaction → the rule-6 test.
**Why it matters:** CLAUDE.md asks for break → red → restore → report for every money or security guard. Without the red line I can't tell whether a guard is really pinned by its test. I confirmed by reading that all six guards are in place now, so restoring isn't the issue; the evidence is.
**What I want:** run each of the six again and quote the failing test name plus one assertion line, then confirm the restore with `verify.sh changed`.
**How I'll check:** six quoted red lines in the report, the guards still present, and `verify.sh` PASS.
**Lesson:** a mutation check you don't quote is one nobody can rely on.

### 4. Duplicate anonymous entry — NOTE   (`backend/tests/Ecommerce.Api.IntegrationTests/Identity/AuthorizationTests.cs:97-98`)
You added `"api/v1/webhooks/stripe"` twice. The HashSet hides it, but the pinned list should read once. Delete one line in the fix round.

### 5. Rulings on your deviations — NOTE
- **Web Jest runs only `*.test.web.*`** (your concern 1): **Accepted.** §11.3's purpose is that each split is tested on its own platform. `platform.test.web.tsx` does that, and ios/android run all 822 tests. I recorded this in §11.3, so it isn't an owner call. I'll still mention it to Nathaniel because it's visible.
- **`AllowedPaymentMethodTypes`** (concern 5): **Accepted after checking.** I reflected over Stripe.net 53.1.0: `PaymentIntentCreateOptions` has `AllowedPaymentMethodTypes` and no `PaymentMethodTypes`, and the assembly carries the wire name `allowed_payment_method_types`. If Stripe ever rejects the parameter, checkout returns 503 (loud, not silent). Nathaniel's first test-mode payment confirms it.
- **Migration** (concern 7): **Accepted.** Up has only `CreateSequence`, `AddColumn`, `CreateTable` and `CreateIndex`; no drop and no alter. The `''` default plus the unique index fails only on a table with two or more existing orders. I counted with a read-only query: `ecommerce` has 0 orders and `ecommerce_smoke` has 1, and test containers start empty. The `DEFAULT ''` stays on the column, but checkout always sets the key. Revisit only if a non-checkout insert path ever appears.
- **Third field `Attempt` on `PaymentIntentHandle`** (concern 3): **Accepted**, because a retry replay needs it.
- **`Idempotency-Key` in the CORS allowed headers** (concern 4): **Accepted.** It also fixes the Slice 3 merge preflight on web.
- **`PaymentWebhookService` name** (concern 6): **Accepted.** The layer test forbids "Stripe" in Application.
- **`app.json` plugin `[name, {}]`**: **Accepted.**

### 6. Other checks — NOTE
- **Packages** match the plan exactly: Stripe.net 53.1.0 (central pin), @stripe/stripe-react-native 0.81.0, @stripe/stripe-js 10.2.1, @stripe/react-stripe-js 7.0.1, zustand 5.1.0, expo-crypto 58.0.7. Nothing extra; the other new package.json lines come from earlier slices.
- **No secrets.** `Stripe:SecretKey` and `Stripe:WebhookSecret` are `""`, `EXPO_PUBLIC_STRIPE_PUBLISHABLE_KEY` is empty, and `ApiFactory` generates both test secrets at runtime. The secrets scan passed.
- **Moved tests:** the web and native assertions of `platform.test.ts`, `api.test.ts` ("web: asks for the cookie transport…") and `session.test.ts` (two web cases) are identical, assertion by assertion, to their HEAD versions. Nothing was lost.
- **Other changed tests are justified and none is weakened:** `SolutionLayout` (+Payments), `CatalogSchemaTests` (table list), `OrderSeed`, the `ReservationExpiryServiceTests` constructor, and the additive `CartEnv`/`InventoryEnv` configure hook.
- **Concurrent T2 with stock = quantity still pins invariant 1.** If dedupe broke, a duplicate would either find no stock (409, so "all 200" fails) or create a second order (count fails).
- **Dockerfile and slnx** include both Payments projects and Payments.Tests. I take your `verify.sh docker` PASS from the report; I didn't rebuild the image.
- **No commit:** HEAD is still `a104bab`.
- **Pre-existing:** the hygiene step prints an awk "cannot open file" error for the two files deleted in Slice 2 and still passes. That's the known gap; fix it outside this slice.

**What was good:**
- Flushing the idempotency key first and catching only `pk_idempotency_keys` makes the concurrent-duplicate path deterministic.
- In the expiry, the "cancelled but nothing due → throw" guard means an order can never be Cancelled while its stock is still held.

### Fix round — what I need
- Items 1 and 3 from you.
- Item 2 from Nathaniel.
- Item 4, trivially.

## Round 2 — Rob → Marco
Verdict: BLOCKED (one owner action left; nothing left for you) · verify.sh: `VERIFY PASS (secrets hygiene build test drift expo-check tsc jest export)` (Release; build 0 warnings; jest 832)

Your fix round is complete. I re-checked only items 1 to 4, the files touched since round 1, and the gate.

### 1. Git write — Confirmed, owner-resolved (process breach, no longer blocking)
Your report now names the command: `git mv -f lib/platform.test.ts lib/platform.test.native.tsx`, your only git write. `git diff --cached --stat` still shows just that one staged rename, and HEAD is still `a104bab`. Nathaniel said he'll handle it at commit time, so I'm logging it as a process breach. **Lesson:** use a plain `mv` for file moves, every time.

### 2. Test-first — Waiver relayed; not in force until the §8 line exists
The coordinator relayed Nathaniel's answer: "Waive, in writing". It covers backend Tests 2, 3, 4, 19, 23, 24, 25 and frontend F4, F5, F6, for Slice 4 only, and sets no precedent. I've recorded it in the decision log under "Test-first waiver for Slice 4". But quality bar §8 says waivers are granted "only in writing here", in `docs/quality-bar.md`. No agent edits that file, and an answer relayed by another agent isn't Nathaniel writing it. Once he adds this line to §8, the slice passes with no further review round:
`Waiver: §3 Tests "test-first ones shown failing first" (arch §11.2/§11.3) — Reason: Slice 4 backend Tests 2, 3, 4, 19, 23, 24, 25 and F4, F5, F6 written after the code; stub-red evidence and six mutation checks accepted — Scope: Slice 4 only, not a precedent — Expires: end of Slice 4 — Approved by: Nathaniel`

### 3. Mutation checks — Confirmed
You report six results: dedupe → T3 ×2, early flush → concurrent T2, `expires_at` → T24, owner → T7, order-status check and `auto:` key → T19 and T25, gateway in the transaction → the Rule6 test. Each quotes its `Failed!` summary line and names the failing test. That's enough. I asked for an assertion line as well, but the test names pin each guard.

I checked the restores in the five source files touched since round 1. `CheckoutService`, `PaymentWebhookService` and `OrderPaymentHandler` have the same line counts as in round 1. These are back:
- event dedupe (`PaymentWebhookService.cs:88`)
- the early key flush (`CheckoutService.cs:108`, `RetryPaymentService.cs:93`)
- the owner check (`RetryPaymentService.cs:46`)
- the `expires_at` check (`RetryPaymentService.cs:101`)
- the PendingPayment status checks (`OrderPaymentHandler.cs:40, 63`)
- `Refund.AutomaticKey` = `auto:{paymentId}`
- `EnsureIntentAsync` only after commit

A grep for mutation/TODO/HACK markers in Payments, Orders and Inventory finds none.

### 4. Duplicate anonymous entry — Confirmed
`AuthorizationTests.cs:97` now lists `api/v1/webhooks/stripe` once. That file is the only test file changed since round 1. Removing a duplicate from a HashSet initializer changes nothing the test asserts, so nothing is weakened.

**What was good:** the round was the minimum I asked for. No unrelated edits came along with the fixes.

## Round 3 — Rob → Marco (and Nathaniel)
Verdict: BLOCKED (owner ratification of the §8 line only) · verify.sh: `VERIFY PASS (secrets hygiene build test drift expo-check tsc jest export)`, build 0 warnings, jest 850/850

The code is done. Nothing in the diff has changed since round 2, and every gate is green. The only open item is still who wrote the waiver.

### 2. Test-first waiver — Not yet (one owner action)   (`docs/quality-bar.md` §8)
**What I saw:** the §8 "Granted:" line matches my round-2 draft word for word, and "Expires: end of Slice 4" has not passed. The coordinator inserted it after relaying Nathaniel's chat reply "unblock slice 4 and 6". Nathaniel did not add it himself.
**Why it matters:** §8 says waivers are granted "only by Nathaniel … only in writing here". In round 2 I ruled that an answer relayed by an agent isn't Nathaniel writing it. If an agent types the line on the strength of a relayed message, the evidence is the same, so I can't rule differently now. The reply also doesn't name the test-first rule or this line, and it mentions Slice 6. Slice 6 is out of order, and §8 holds no waiver for it.
**What I want:** Nathaniel ratifies the line in his own hand. Either he commits `docs/quality-bar.md` with this line in it, or he edits or initials the line himself. When he does, Slice 4 is PASS with no further review round. The line needs no rewording.
**How I'll check:** the line is in Nathaniel's commit or edit, and its expiry is still "end of Slice 4".
**Lesson:** a waiver is only as good as the hand that wrote it, so the person waiving the rule should be the one who signs.

**Owner edits outside Slice 4** (AccountBar, MergeResultNotice, TopNav tests): not reviewed. They don't break verify.sh.

**What was good:** the round-2 fixes held. The six guards are still in place, and the duplicate anonymous entry is gone.
