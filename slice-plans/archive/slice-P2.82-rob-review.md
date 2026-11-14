# Slice P2.82 — Review thread

## Round 1 — Rob → Marco
Verdict: PASS · verify.sh: `VERIFY PASS (secrets hygiene format build test drift expo-check tsc jest export)` · build 0 warnings · Api.IntegrationTests 628 passed · jest 2111 passed

The diff is small and does what the row asks: one host convention, applied once to `moduleEndpoints`, the two per-route `.Accepts` removed, and four tests that went red on today's code. I re-ran both mutations and the full gate myself. No blocking findings.

### 1. Your departure from the plan (JSON routes only) — Accepted
**What I saw:** my plan said "every `IAcceptsMetadata`". You found that this also hit the multipart upload route and turned IP6, IP7 and IU5 red, so you limited the convention to routes whose accepts list `application/json`, and scoped CT4 the same way, without editing those tests.
**Why you were right:** IU5 dates from slice 9.6, when upload was still an MVC action with `[Consumes]`. `[Consumes]` gives MVC accepts metadata too, so on MVC a wrong Content-Type on upload already fell through to the anonymous 404 fallback. The migration's goal is "behaviour unchanged", and editing three pinned tests to get a new 415 would have been a behaviour change outside the row. The 404 also runs nothing: no handler, no write (IP7 checks `image_blobs` = 0). My plan missed the upload route; that gap is mine.
**Coverage check for P2.72–P2.79:** I grepped the remaining controllers for `FromForm`, `Consumes`, `IFormFile`, raw-body reads. The only hit is the webhook's `Request.Body` (P2.80, D13: no body parameter, so no accepts metadata and nothing to fix). Every remaining body route (Cart, Orders, Returns, AdminOrders, AdminRefund, Checkout) binds a JSON `[FromBody]` DTO, so it gets `application/json` and is covered.
**Would CT4 catch a future JSON route without the fix?** Yes. CT4 reads every `RouteEndpoint` in the host, not just the group, so a JSON route mapped outside `moduleEndpoints` is listed as missing `*/*`. P1 below proves it (CT4 red when the call is removed). I recorded this as D15 in `minimal-api-migration-plan.md`.
**Lesson:** a migration convention should match the old framework's behaviour, not a cleaner idea of it; when a pinned test disagrees with the plan, the test wins until the owner decides otherwise.

### 2. Mutations — re-run by me, both red as you reported
- **P1** (delete `AcceptAnyContentType(moduleEndpoints);`): `Failed: 12, Passed: 0` (CT1 x4, CT2 x5, CT3, CT4, AE6), 11 x `Actual: NotFound`. Restored from scratch copy, `cmp` OK.
- **P2** (`accepts is { IsOptional: true } && …`): `Failed: 10, Passed: 2` (CT1 x4, CT2 x5, CT4); AE6 and CT3 green, which shows CT1/CT2 cover what AE6 does not. Restored, `cmp` OK.

### 3. Plan §7 checks
- `grep -rn "\.Accepts" backend/src/Modules`: none.
- `MapFallback` unchanged; the convention is applied once, to `moduleEndpoints` only.
- Auth runs first: the anonymous half of CT2 asserts 401 (it was 404 before, the real security half of the row).
- No `var`, no new public type, no dead code. The extra condition is one line, with a comment that names the tests it protects.
- D-D (no body, no Content-Type → 400, MVC gave 415): you observed it in the MVC capture, as planned. I added it to the 13.2 "Minimal API migration" row.

### 4. `SnapshotSql` is clever where it should be plain — NOTE   (`ContentTypeContractTests.cs:17`)
**What I saw:** you pack four counts into one number (`users * 1000000 + categories * 10000 + products * 100 + sum(on_hand)`).
**Why it matters:** a reader has to work out the encoding, and two changes can cancel out (one more product and 100 fewer units give the same number). Nothing in this slice hits that, because the 415s prove no handler ran, so it is not blocking.
**What I want (next touch):** compare the counts one by one, for example with a small named record or one assert per table. Logged in the backlog.

### 5. Upload route keeps MVC's anonymous 404 — NOTE
An anonymous upload with a wrong Content-Type gets 404, not 401. MVC did the same, and nothing runs. If Nathaniel ever wants 415 there, that is a separate row that edits IP6, IP7 and IU5 on purpose. Logged in the backlog.

**What was good:** you stopped at three red pinned tests instead of editing them, and named the gap in your report. Your MVC capture pinned the exact 415 `type` (identical on both stacks), so CT1 checks the full problem shape, not only the status.
