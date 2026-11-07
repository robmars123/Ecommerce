# Slice 1b — Review thread

## Round 1 — Rob → Marco
Verdict: BLOCKED · verify.sh: `VERIFY PASS (secrets hygiene build test drift expo-check tsc jest export)` (build 0 warnings; tests Catalog 166, Identity 73, Architecture 25, Integration 118, Inventory 3; jest 277) · `VERIFY PASS (docker)`

The security core is sound. I read every auth path. Login takes the row lock before it verifies the password, refresh locks the family, reset locks the user and then the token, and the order is the same everywhere. No token reaches a log or an `ApiError`, cookie mode keeps the token out of the body, and bootstrap is create-only. Boundaries are clean, and you made the architecture tests stricter, not looser. I am blocking for proof, not design: the hard invariants have no red evidence, one D12 rule has no test, bootstrap skips the password policy, and the real-host smoke test was never run.

### 1. No red evidence for the hard security invariants — BLOCKING   (your report, concern 3)
**What I saw:** You only proved the two `FOR UPDATE` locks with a mutation. The reuse revoke, reset single-use, xmin check, per-resource auth, token validation, CSRF header and client single-flight are all covered by tests written after the code. None of those tests has ever been seen failing.
**Why it matters:** A test that has never been red may be green by luck. Example: if II-9's parallel confirm passes because the user lock alone serializes it, then deleting the token lock later would go unnoticed. These are rule 12 / quality-bar §3 "test-first shown failing" items. Since the tests already exist, the only valid substitute is a mutation run.
**What I want:** For each mutation: apply it, run only the named test, quote the failing assertion line, revert it, and confirm the test is green again. Leave nothing mutated in the tree.
- M1 `RefreshService` Reuse case: delete `family.Revoke(...)`. II-4 and II-5 go red.
- M2 `PasswordResetService.ConfirmAsync`: drop `!resetToken.CanRedeem(now)`. The II-9 reuse/expiry checks go red.
- M3 Remove `InvalidateOutstandingAsync(...)`. "a newer request invalidates the older token" goes red.
- M4 Remove `FOR UPDATE` from both `FindByIdForUpdateAsync` and `GetForUpdateByHashAsync`. The II-9 parallel confirm goes red. Run it 3 times (R3).
- M5 `ProductWriteStore.SetExpectedVersion`: delete the `OriginalValue = version` line. Both IA-2 tests go red.
- M6 `AdminProductsController`: change the policy to `AdminOrSupport`, then delete the attribute. The II-10 Support-403 case goes red, then II-11 "undeclared" goes red.
- M7 `ConfigureJwtBearerOptions`: one at a time, `ValidateIssuer = false`, `ValidateAudience = false`, and a different `IssuerSigningKey`. The matching II-12 case goes red each time.
- M8 `RefreshTokenTransport.ReadToken`: always read the cookie. II-8 "cookie without header 401" goes red.
- M9 `session.ts refreshSession`: drop the `??=` single-flight. F2b "3 parallel 401s" or S-1 goes red.
If a mutation stays green, the test is too weak. Fix the test so it fails (don't touch production code), and say so in your reply.
**How I'll check:** I'll read the quoted failure lines. Then I'll run `git diff` on those files to confirm they're restored and re-run `verify.sh`.
**Lesson:** You have only seen a test protect an invariant once you have seen it fail without that invariant.

### 2. The variant-only stale edit is untested — BLOCKING   (`Infrastructure/ProductWriteStore.cs:26`, `AdminProductApiTests.cs:79`)
**What I saw:** `SetExpectedVersion` forces `UpdatedAt.IsModified = true` (D12). Both IA-2 tests change `name`, so the product row is written whether or not that line exists.
**Why it matters:** Suppose two admins load v1 and each changes only a variant price in the same clock tick. Without the line, EF updates `product_variants` and skips the `products` row, so the `WHERE xmin = @v` check never runs. The second price silently overwrites the first. This is a lost update on money, and someone could delete the line as "redundant" with every test still green.
**What I want:** Add an IA-2 test on a frozen clock. Two PUTs from one version each change only a variant price; expect one 200 and one 409 on `errors.version`. Then delete the `IsModified` line and show the new test go red (add this as M10 to finding 1).
**How I'll check:** I'll read the test body for "variant-only, frozen clock" and the quoted red run.
**Lesson:** A guard written for one edge case needs a test that exercises exactly that edge case.

### 3. Bootstrap admin skips the password policy — BLOCKING   (`Bootstrap/AdminBootstrapHostedService.cs:54`)
**What I saw:** `IsConfigured` checks only that the password is non-empty. `AdminPassword = "admin"` creates an Admin with a 5-character password.
**Why it matters:** The most privileged account gets the weakest password the system allows: below arch §10.1's 8–128, which register and reset both enforce. With lockout at 5 tries per 5 minutes, that is about 1,440 online guesses per day against it. Nothing tells the operator.
**What I want:** If the bootstrap password is outside `PasswordPolicy.MinLength..MaxLength`, log an Error naming the config key (no email, no password) and create nothing. Add an II-15 case: a 7-character password gives no user and the Error event, and the CollectingSink shows no password text.
**How I'll check:** I'll run the new II-15 case and read the log assertion.
**Lesson:** Every path that creates a credential enforces the same policy, especially the path for the most privileged account.

### 4. The :5090 smoke test was not run — BLOCKING   (plan §9–10)
**What I saw:** You didn't run the smoke test (report concern 4).
**Why it matters:** Integration tests run in `Testing`. The Development-only code (ephemeral key singleton, bootstrap from env vars on a migrated database, the relative `../../../artifacts/dev-mail` path resolved from ContentRoot) has never run in a real host. That code is what Nathaniel runs first.
**What I want:** Follow plan §9–10 exactly on :5090 (never touch :5080). Bootstrap env vars apply only to that process. Show: admin login 200, create 201 inactive, public detail 404, publish 200, public detail 200, Customer on admin 403, and a reset request that writes one file under `artifacts/dev-mail/`. Cut tokens to 8 characters. Stop only the process you started.
**How I'll check:** I'll read the quoted status lines in your reply.
**Lesson:** A host-configuration branch that no test environment runs is only proven by running the real host.

### 5. Reset confirm goes to login, not `startSession` — Accepted   (your concern 1)
You are right, and the plan contradicted itself: §5's hooks line says `startSession`, while its components line says confirm → `/login`. Confirm returns 204 with no tokens and revokes every family, so there is nothing to start. `PasswordResetConfirmForm` → `/login?notice=password-changed` is correct.

### 6. `MapFallback(404).AllowAnonymous()` — Accepted   (your concern 2, `Program.cs`)
The authorization middleware applies the fallback policy even when no endpoint matched, so unknown routes became 401. That broke two Slice 0 contracts (ProblemDetailsTests, CorrelationIdTests). One terminal endpoint with no handler is the smallest fix. The alternatives (dropping the fallback policy, or a custom result handler) are bigger or reverse §13.2.
Does II-11 still prove "closed by default"? Yes. It fails on any endpoint that declares nothing, and on any anonymous route not on the pinned list. The fallback endpoint is on that list and only sets 404. Paths with a file extension skip `nonfile` and get 401, which fails closed. Losing 405 is acceptable; §9 promises no 405. I recorded this in the §13.2 "Authorization default" row (docs/architecture/13.2-decision-log.md).

### 7. CatalogSchemaTests table list — Accepted   (your concern 4)
The test scans the whole model, so it has to list the Identity tables now. It is still an exact list and it now covers more. That isn't weakening. The Slice 2 carry-in still applies: scope it to Catalog-owned tables when Slice 2 touches it.

### 8. PostgresFixture pool settings — Accepted   (your concern 5)
This is test infrastructure. Each test gets its own database and therefore its own Npgsql pool, so about 118 databases × idle connections exceed the container's 100 (`too many clients`). An app leak would look different: a pool-exhaustion timeout inside one connection string. The app's connection string is unchanged. Side effect: `MaxPoolSize = 8` caps the parallel tests at 8 connections. II-3 stays meaningful (8 > 5 attempts) and II-5 still races.

### 9. Logout racing an in-flight refresh — NOTE   (`features/auth/session.ts:60`)
If a refresh resolves after `endSession`, `startSession` signs the user back in. The family is already revoked, so it ends at the next refresh (≤ 15 min). Optional fix: a session generation counter checked in `runRefresh`.

### 10. Reset-request timing — NOTE
A known email does a lock, writes and a file write. An unknown email returns at once. That is a timing oracle. Accepted for now: Slice 6 moves the send to the outbox, and S7 adds rate limits.

**What was good:** `LockGuard.RequireTransaction` turns a silent no-op `FOR UPDATE` into an exception, which is exactly the D12 trap. The CORS split is also good: credentials are attached only to `api/v1/auth*` and `api/v1/admin*` by endpoint convention, and II-16 pins it.

## Round 1 — Marco → Rob

**1. Fixed (evidence, no production code changed).** I ran each mutation alone, ran only the named test, and reverted. I re-grepped every touched production line afterwards; all are back, and the last full `verify.sh` is green on the restored tree. M4 and M10 first stayed green, so I strengthened tests (see the notes under the table).

| # | Mutation | Test | Red line | Reverted |
|---|---|---|---|---|
| M1 | delete `family.Revoke(Reuse)` | II-4, II-5 | `Expected: Unauthorized, Actual: OK` (II-4); `Expected: Reuse, Actual: null` (II-5) | ✔ |
| M2 | drop `!CanRedeem(now)` | PasswordResetTests | `Expected: BadRequest, Actual: InternalServerError` (4 tests red; Redeem throws) | ✔ |
| M3 | remove `InvalidateOutstandingAsync` | II-9 newer invalidates older | `Expected: BadRequest, Actual: NoContent` | ✔ |
| M4 | no `FOR UPDATE` on user and token (3 runs) | II-9 parallel confirm | `Assert.Single() Failure: The collection contained 2 matching items` ×3 | ✔ |
| M5 | delete `OriginalValue = version` | IA-2 (all) | `Expected: Conflict, Actual: OK`; `...contained 2 matching items` ×2 | ✔ |
| M6a | policy -> `AdminOrSupport` | II-10 | `support GET /api/v1/admin/products: expected Forbidden, got OK` | ✔ |
| M6b | delete the attribute | II-10, II-11 | `customer GET ...: expected Forbidden, got OK`; II-11 `undeclared: api/v1/admin/products...` | ✔ |
| M7a | `ValidateIssuer = false` | II-12 | `wrong issuer: expected 401, got OK` | ✔ |
| M7b | `ValidateAudience = false` | II-12 | `wrong audience: expected 401, got OK` | ✔ |
| M7c | different signing key | II-12 | `Expected: OK, Actual: Unauthorized` (the good token is rejected) | ✔ |
| M8 | `ReadToken` always cookie | II-8 | `Expected: Unauthorized, Actual: OK` | ✔ |
| M9 | drop `??=` in `refreshSession` | S-1 session | `Expected number of calls: 1, Received: 3` | ✔ |
| M10 | delete `UpdatedAt.IsModified = true` | new IA-2 variant-only | `Assert.Single() Failure: The collection contained 2 matching items` | ✔ |

- **M4:** removing the lock on the user alone, or on the token alone, stays green. Each covers the other. Removing both is red 3 of 3 runs, so the test is meaningful.
- **M7c:** it is red because the valid token is rejected. It does not show a forged-key token being accepted. II-12's "other key" case covers that direction.
- **M2:** the red is a 500 because `Redeem` throws on an unredeemable token. The guard is what turns that into a 400.

**2. Fixed (and found the test was blind).** New test `AdminProductApiTests.IA2_two_parallel_edits_that_change_only_a_variant_price_produce_one_200_and_one_409_on_version`. It uses a frozen clock and two PUTs from one version that change only a variant price.
- M10 first stayed GREEN. The test clock had 100 ns ticks, Postgres stores microseconds, so `UpdatedAt` looked "changed" after a round trip and EF wrote the product row anyway.
- I truncated the test clock to whole seconds (`IdentityTestEnv.cs`). The new test is then red under M10 and green normally. Test change only.

**3. Fixed.** `AdminBootstrapHostedService.cs` now rejects a password outside `PasswordPolicy.MinLength..MaxLength`. It logs an Error naming `Identity:Bootstrap:AdminPassword` (no email, no password) and creates nothing. The check runs after the existing-user check, so an existing admin is still a no-op. Test: `II15_bootstrap_with_a_password_shorter_than_8_creates_nothing_and_logs_an_error_naming_the_key` (7 characters; no user; one Error; no password or email text in any event).

**4. Fixed.** Smoke on :5090. Environment `Development`, `--no-launch-profile`, a fresh database `ecommerce_smoke` on the :5434 server, bootstrap env vars set for that process only, and a random bootstrap password. :5080 untouched. Tokens cut to 8 characters.
- admin login **200** (`eyJhbGci...`)
- category **201**
- create **201**, `isActive=false`
- public detail before publish **404**
- publish **200**
- public detail after publish **200**
- Customer on `/api/v1/admin/products` **403** (register **201**)
- reset request **202**, and exactly **1** file under `artifacts/dev-mail/`; it holds the link and the user id, and no email address
- The log shows the ephemeral-key warning and "Admin bootstrap created admin user"; the bootstrap password appears in the log 0 times.

The relative `../../../artifacts/dev-mail` path resolved correctly. I stopped only the process I started (pid 45280, `Ecommerce.Api.exe`); `:5090` now refuses connections. The `ecommerce_smoke` database remains (I do not drop databases).

**5-8.** Accepted. **9-10.** Noted, no change.

verify.sh: `VERIFY PASS (secrets hygiene build test drift expo-check tsc jest export)`. Build is 0 warnings. Tests: Catalog 166, Inventory 3, Architecture 25, Identity 73, Integration 120. Jest is 45 suites, 277 tests.

## Round 2 — Rob → Marco
Verdict: PASS · verify.sh: `VERIFY PASS (secrets hygiene build test drift expo-check tsc jest export)` (build 0 warnings; tests Catalog 166, Identity 73, Architecture 25, Integration 120, Inventory 3; jest 277)

All four blocking items are closed, and I checked each one in the code, not just in your table. Nothing is still mutated, the new tests are sound, and the test-clock change makes the tests more accurate rather than weaker.

### 1. Mutation evidence — Confirmed
I grepped every line you mutated in the restored tree. All of them are back:
- `family.Revoke(RefreshTokenRevocationReason.Reuse` at `RefreshService.cs:49`.
- `InvalidateOutstandingAsync` and `!resetToken.CanRedeem(now)` at `PasswordResetService.cs:40,76`.
- All four `FOR UPDATE` clauses at `Repositories.cs:30,39,67,100`.
- `OriginalValue = version` and `IsModified = true` at `ProductWriteStore.cs:23,26`.
- `[Authorize(Policy = AuthorizationPolicies.Admin)]` on both admin controllers.
- `ValidateIssuer`, `ValidateAudience` and `IssuerSigningKey = keys.Key` at `ConfigureJwtBearerOptions.cs:32-36`.
- The header-gated `ReadToken` at `RefreshTokenTransport.cs:23`.
- `inFlightRefresh ??=` at `session.ts:78`.

The red lines you quoted are the right reds. Each fails on the invariant's own assertion, not on a missing route or a compile error. M2's red is a 500 rather than a 400, which is still the right reason: the guard is the only thing that turns an unredeemable token into the 400 contract.

**M4 (each lock alone stays green): Accepted.** For confirm the two locks are redundant by design. One token belongs to one user, so the user lock alone already serializes every confirm of that token. A second confirm then re-reads a committed `used_at` under READ COMMITTED. No test can prove a redundant lock on its own, and "both removed → 2 × 204, 3 of 3 runs" proves the invariant. The user lock has a separate job: it serializes confirm against login, which writes the same `users` row (failure count, lockout, rehash). That race isn't tested. I'm leaving it as note 11, not blocking: it needs a hasher upgrade or a failed login to land in the same milliseconds as a reset.

**M7c (the valid key is required): Accepted.** You are right that M7c tests the other direction. The forged-key direction is II-12's "other key" case, and it is a controlled differential: `Mint` gives it the same issuer, audience, subject and expiry as the "good" token, which returns 200. The key is the only difference, so the 401 can only come from signature validation. No mutation is needed.

### 2. Variant-only stale edit — Confirmed
I read `IA2_two_parallel_edits_that_change_only_a_variant_price_…` (`AdminProductApiTests.cs:94`). It runs on a frozen clock and both PUTs come from one version. Only the variant `price` differs (10.00 vs 20.00), and the test asserts one 200 plus one 409 with `errors.version`. The result doesn't depend on timing: the loser's `UPDATE products … WHERE xmin = @v` fails in whichever order the two run.

**Test clock truncated to whole seconds (`IdentityTestEnv.cs:98-101`): Confirmed as a legitimate fix, not a weakening.**
- Only the start instant is truncated. Every window test moves the clock by an offset (+4:59 / +5:00 lockout, +15 m access, +30 m reset, 14 d / 30 d refresh), so each boundary stays the same distance from the start.
- With the old 100 ns start, Postgres silently cut `lockout_end` and `expires_at` by up to 0.9 µs on the round trip. The boundary assertions were then comparing a rounded stored value with an unrounded clock. Whole seconds remove that hidden rounding.
- JWT `exp` / `nbf` are whole seconds, and II-12's real-clock `Mint` keeps its 1-minute `nbf` margin.
- `CatalogTestEnv`'s clock and the unit-test fakes are untouched.
- What you uncovered is real. With a sub-µs clock, the round trip made `UpdatedAt` look changed, so EF wrote the product row anyway and the D12 guard was never tested. In production the guard matters exactly in the same-microsecond case, and the truncated clock now models that case.

### 3. Bootstrap password policy — Confirmed
`AdminBootstrapHostedService.cs` checks `PasswordPolicy.MinLength..MaxLength` after the existing-user check, so an existing Admin is still a no-op. On failure it logs one Error whose template names `Identity:Bootstrap:AdminPassword` and its bounds. It never logs the email or the password, and it creates nothing.
In the new II-15 case (`IdentityHostTests.cs:151`):
- The password is 7 characters.
- The test asserts zero users and exactly one matching Error.
- The rendered text contains the key and neither the password nor the email.
- No event anywhere contains the password.
On the old code `Assert.Empty(users)` fails, so the test guards the fix.

### 4. :5090 smoke — Confirmed
Your status lines cover the whole plan §9–10 list. I checked the evidence you left behind:
- `artifacts/dev-mail/` has exactly one file. It is gitignored (`.gitignore:50`) and contains a user id, a timestamp and the link, with no `@` anywhere. That proves the relative path resolved from ContentRoot.
- Using a separate `ecommerce_smoke` database and leaving it in place was right: no drops, and nothing on :5080 touched.

### 5–8 — Accepted (Round 1 rulings stand). 9–10 — Notes, still open, non-blocking.

### 11. Reset confirm vs. concurrent login writes — NOTE (new)
The user lock in `ConfirmAsync` is what stops a login (failure count or rehash) from overwriting the new hash or the cleared lockout on the same row. No test pins it, because II-9 only races two confirms. When Slice 7's auth hardening touches this code, add a test that races a confirm against a wrong-password login.

**What was good:** You strengthened the M10 test when the mutation stayed green, and you didn't rationalise it. You also traced the cause to µs-versus-100 ns precision instead of loosening an assertion. That is how a test earns its green.
