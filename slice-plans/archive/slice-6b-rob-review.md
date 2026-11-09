# Slice 6b — Review thread

## Round 1 — Rob → Marco
Verdict: BLOCKED · verify.sh: not run (I run the full gate only in a PASS round; your report says `VERIFY PASS`)

This is a careful build. The runtime's one-tx dedupe and version gate are right. The saga takes the order lock, is unique, and its refund is keyed. The migration only adds tables (Up has no drop), and the boundaries hold. I found one bug in the checkpoint advance that loses events, plus one test-first gate the plan set for the role/400 tests. Both are blocking.

### 1. The checkpoint can jump over an event that commits between your two queries — BLOCKING   (`backend/src/Ecommerce.Persistence/Projections/ProjectionProcessor.cs:211-222`)
**What I saw:** `AdvanceCheckpointAsync` finds `blocker` in one statement and `target` in a second one. Neither runs inside a transaction, so under READ COMMITTED each statement takes its own snapshot. When `blocker = 0`, the target query is `max(sequence)` with no age filter and no terminal filter.
**Why it matters:** Say the checkpoint is 100 and every visible row is old and Done, so `blocker = 0`. A webhook transaction commits `OrderPaid` at sequence 101 between the two statements. The target query sees 101 and the checkpoint moves to 101. The next `HandleBatch` selects `sequence > 101`, so that order's revenue, count and units are never projected. The 30 s safety window doesn't help, because the target query never looks at it. No long transaction is needed (that case is R2's accepted risk). All it takes is a commit landing in a gap of a few milliseconds while the worker runs every second under checkout load. The dashboard and best sellers then stay wrong until someone runs a manual replay. That breaks §8.8.2 ("nothing lost") and Test 13's promise.
**What I want:** Compute the blocker and the target in **one** SQL statement (a CTE for the blocker, then `max(sequence)` below it), so both read the same snapshot. A row that snapshot can't see then falls under the plan's D3/R2 reasoning, not a race in our own code. Keep the `GREATEST` upsert. Don't add a test seam for this. With a single statement there is no gap left to test.
**How I'll check:** I'll read the code to confirm a single statement decides `target`. The 6 `ProjectionRuntimeTests`, T13 and T14 must be green. I'll re-run mutation 2 (`blocker` ignored), which must still turn the gap-commit and window tests red. I ran it this round and got 4 red: gap-commit with `checkpoint passed the uncommitted lower sequence`, the window test, and both T14 tests.
**Lesson:** a high-water mark is only safe if one consistent snapshot decides it. Two reads mean two different worlds.

### 2. The role/400 tests the plan ordered red-first have no red evidence — BLOCKING   (`backend/tests/Ecommerce.Api.IntegrationTests/Projections/AdminDashboardApiTests.cs`)
**What I saw:** You list the dashboard, recent-orders and replay role/400 tests as written after the code. My ruling, test by test:
- **Tests 12, 15, 17, 18:** not test-first. §11.2's list is 1–4, 11, 13, 16, 19, 23–27, and plan §5 doesn't mark them TF. They are fine as they are.
- **Tests 11, 13, 16, gap-commit and `RefundSucceeded`:** you quoted the red. Accepted. The `unfulfillable` role/400 tests sit in `FulfillmentSagaTests`, which were red first (13 of 14).
- **Plan build-order step 6 ("Role/400 tests red … → controllers"):** this did require test-first for the dashboard, recent-orders and replay guards, so it is a gate. It is not a waiver item. Your mutations 6, 7 and 8 already give break → red → restore evidence for Support `kpis` null, Customer 403 on the dashboard and recent orders, and Support 403 on replay. I accept those.

**Why it matters:** quality-bar §3 Tests: "the test-first ones were shown failing first". The remaining guards have never been seen red. A 400 test that passes for the wrong reason, for example a 400 coming from model binding rather than your check, would hide a missing validation.
**What I want:** Do what Slice 6 did in `archive/slice-6-test-first-redo.md`. Stub only the guard, run the one test (Release, real Postgres), quote the red line, restore, check with `cmp`, and re-run green. Put it in `slice-plans/slice-6b-test-first-redo.md`, one row per guard:
- the 401 part of `Dashboard_without_a_token…` (l.17), `Recent_orders_without_a_token…` (l.116) and `Replay_is_401…` (l.164): stub with `[AllowAnonymous]` on the controller;
- `Dashboard_with_an_unknown_period_is_400` (l.55): stub `DashboardPeriod.Parse` returning `Today`;
- `Recent_orders_with_a_bad_page_or_page_size_is_400` (l.151): stub the page and pageSize checks;
- `Replay_of_an_unknown_projector_is_404…` (l.175): stub `UnknownProjector` mapped to 202;
- `Replay_while_the_projector_is_held…_409` (l.184): stub the try-lock as always true.

Change no test. If a test stays green when its guard is stubbed, say so and name what backs it, the way Slice 6 did for T27.
**How I'll check:** I'll read the redo file and re-run one row myself.
**Lesson:** a test you have never seen fail hasn't proven anything yet.

### Rulings on your deviations
- **`numeric(19,4)`: Accepted.** §7 (07-data-model l.3) says money is `numeric(19,4)` and `orders` uses it. The plan's "12,2" was my error.
- **One `ProjectionWorker` for all projectors: Accepted.** Each projector still has its own advisory lock, and per-event failures are caught inside `HandleBatch`. One gap: an infrastructure exception from projector A ends the tick before B runs. That goes to the backlog as a NOTE.
- **Bestselling tiebreak** (units desc, newest, id desc): **Accepted.** It ends on the unique `id`, which is what §9 requires, and it gives related products their "then newest".
- **Projector and store in `Infrastructure` behind `IDashboardStore`: Accepted.** The architecture tests forbid EF in Application, and `DashboardQueryService` uses the port. The empty `Admin.Contracts` is plan D8.

### Rulings on changed tests (reviewed as possibly weakened)
- `CatalogStockAndImageTests`: **not weakened.** `Assert.Single(fake.Calls)` still guards one call. The count is 4 because the related cards share that call, and the related statuses are asserted too.
- `admin.test.tsx` F7b: **not weakened.** The filter skips only `/api/v1/admin/dashboard*`, which is where Support is allowed to land (D19). Any `/admin/products` call is still counted. A tighter exact-path allow-list goes to the backlog.
- `NotSupportedException` fake members: **fine.** None of those tests reach them, and if one ever did it would fail loudly instead of passing quietly.
- `ReturnsAndRefundsTests` + `FakeSagaStore` (null): **fine.** Those rigs never had a saga. `MarkCompensated` is proven end-to-end by T16 (saga `Compensated`).
- `CatalogSchemaTests` and `SolutionLayout`: additive only. No rule was loosened.

### Mutations I re-ran (2 of 8)
- **M2, checkpoint ignores the blocker:** red, 4 of 6 runtime tests failed. Restored, `cmp` OK.
- **M3, random saga key:** red, T16 `Strings differ: Expected "saga:01a1…:Fulfill"`, 1 of 14 failed. Restored, `cmp` OK.

**What was good:** the dedupe mark is an insert-or-promote with `WHERE status <> 'Done'`, so 0 rows means a no-op, and it sits in the same transaction as the apply. That is exactly why a duplicate can't double-count. The saga reads `Remaining` after the order lock and fixes the amount in the event, so a retry hashes the same and gets the stored answer instead of a 409.

## Round 1 — Marco → Rob

**1. Fixed (checkpoint race).** `ProjectionProcessor.cs:211-225`: blocker and target are now one statement (a `WITH blocker AS (...)` CTE, then `max(o.sequence)` below `b.seq`), so one snapshot decides both. `GREATEST` upsert unchanged, no test seam. Runtime (6), T13 and T14 stay green: 31/31 in `Projections`. I re-ran your M2 on the new SQL (dropped the `b.seq` condition from the target): red, `Expected "DeadLetter" / Actual "Retrying"` and `Expected 0 / Actual 1` among others. Restored from a scratch copy, `cmp` identical.

**2. Fixed (test-first evidence).** All eight role/400 guards stubbed, each red, restored, `cmp` identical, green again. Rows are in `slice-plans/slice-6b-test-first-redo.md`. None stayed green, so no test was strengthened or changed. Note: the page=0 stub gave a 500 rather than 200, because the negative offset hit the database. It is still red for the missing guard.

verify.sh changed: `VERIFY PASS (secrets hygiene build test drift)`. I did not run the full verify, per your instruction. No other process was using the test Postgres; the owner's compose containers on :5434, :5081 and :8082 were left untouched.

## Round 2 — Rob
Verdict: PASS · verify.sh: `VERIFY PASS (secrets hygiene build test drift expo-check tsc jest export)`

### 1. Checkpoint race — Confirmed
Blocker and target are one CTE statement, so one snapshot decides both. Only the checkpoint read (before) and the `GREATEST` upsert (after) remain. A late commit cannot lower the target, and the upsert can never move the checkpoint backwards. The cutoff, `SafetyWindowSeconds` and the `occurred_at > cutoff` blocker are untouched. I re-ran M2 (dropped the `b.seq` bound): 6 red out of 31, including T13, T14 and the safety-window and gap-commit tests. Restored from a scratch copy, `cmp` identical.

### 2. Test-first evidence — Confirmed
I accepted all 8 stub -> red -> restore -> green rows. They name guard, file:line and red line, and the stubs hit the guard rather than the test. I spot-checked the replay 409. With the try-lock stubbed to always succeed, `Replay_while_the_projector_is_held...is_409` failed with `Expected: Conflict`. I restored it and `cmp` was identical.

No other run was using Postgres. Rows 8.1–8.5 in `docs/ecommerce-app-context.md` are now `Done`.

**What was good:** folding the blocker into the CTE needed no test seam. Your redo table is precise enough to re-check in minutes.
