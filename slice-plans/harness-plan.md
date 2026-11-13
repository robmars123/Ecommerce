# Harness plan — evaluation and task list

Author: Rob · 2026-11-12 · Not a slice. No code was written for this plan.

Read for building it: CLAUDE.md §1, §3, §5, §8 · arch §11.2–§11.4, §14 · quality-bar §4, §5 · `scripts/verify.sh` · `.claude/settings.json` · `compose.yaml` · README "stripe listen" section.

## 0. Verdict

| # | Meaning | Verdict | One-line reason |
|---|---|---|---|
| 1 | Correctness test harness | **Exists. Do not build a new one.** Add one small tool (repeat runs). | The harness arch §11.1 asks for is already built. It has Testcontainers Postgres, WebApplicationFactory, a fake gateway, a fake clock, and a driven outbox. Every §11.2 test 1–30 has a named test. |
| 2 | End-to-end / app harness | **No browser or device E2E.** Add a scripted API smoke run in **Slice 7**. | §11.3 rules out Detox, Maestro and Playwright for v1. Slice 7 (§12.3 row) and §14 do need "Stripe test mode end to end" and "staging end-to-end works", and no script does that yet. |
| 3 | Agent / dev harness | **Needed, small.** Three script changes need no sign-off. Four `.claude/` changes **need Nathaniel's sign-off.** | Nothing enforces the one-verify-at-a-time rule. Nothing undoes a mutation for you. The git deny list has gaps. Nothing flags changed or deleted tests. |

## 1. Correctness harness — what exists

Evidence, all under `backend/tests/Ecommerce.Api.IntegrationTests/`:

- `PostgresFixture.cs`: one `postgres:17-alpine` Testcontainer per assembly, one fresh database per test (`CreateDatabaseAsync`), and `MaxParallelThreads = 8` sized to the 100-connection limit. Arch §11.1 and quality-bar §4.3 ("in-memory EF does not count") are met.
- `ApiFactory.cs`: `WebApplicationFactory<Program>` with a random JWT key and a random webhook secret per factory (no committed secrets). The outbox and projection workers are off, so tests drive `OutboxProcessor` and the projectors themselves, and every run is deterministic.
- Fakes: `Checkout/FakePaymentGateway.cs` (Stripe), `Catalog/CatalogTestEnv.cs` `FakeClock : IClock`, `CollectingSink.cs` (logs/alerts), and `RecordingEmailSender` plus the module fakes in each `Ecommerce.<M>.Tests/Application/`.
- Hard-rule coverage, by test name:
  - T1: `Checkout/CheckoutTests.cs` and `Inventory/ReserveConcurrencyTests.cs`, which has a start gate, a deadlock case and a lock-order case.
  - T2: concurrent same key.
  - T3: concurrent webhook (`Checkout/WebhookTests.cs`).
  - T4 and T24: on the fake clock (`Checkout/RetryAndExpiryTests.cs`).
  - T8: a failure inside the webhook tx, then redelivery. Rule 6 ("gateway never sees an open transaction") has its own test.
  - T9 and T15: `Notifications/OrderEmailTests.cs`.
  - T11–T14, T17, T18: `Projections/*`.
  - T16: `Refunds/FulfillmentSagaTests.cs`.
  - T19 and T25: `WebhookTests.cs`.
  - T26–T28: `Refunds/*`.
  - Outbox races and backoff: `Outbox/OutboxProcessorTests.cs`.
- Frontend: F3–F6 and F8–F13 are tagged in Jest. F1 is `tests/routes.test.tsx`, F2 is `lib/api-client*.test.ts`, and F7 is `features/auth/components/AdminGate.test.tsx`.

**Gap.** Quality-bar §5 says "flaky tests are bugs … prove it with repeated runs", and there is no tool to repeat one test N times under the verify lock. Concurrency tests (T1, T2/T3/T24 concurrent, T26, the outbox race) can pass by luck on a single run.

**Considered and rejected:**
- A shared `Ecommerce.TestKit` project to dedupe the four `FakeClock` copies. It is a refactor with no new consumer or capability, and it would add a cross-test-project reference. → backlog NOTE at most.
- A real process-kill test for T8. A killed process leaves Postgres to roll back the open transaction, which is what the current in-tx failure test proves. A process kill adds flakiness and proves nothing more.

## 2. E2E / app harness — what exists

- Arch §11.3 "Not in v1": no device or browser E2E. EAS release candidates are smoke-tested by hand. This is a recorded decision, so I won't overturn it.
- `compose.yaml` has postgres, api (:5081) and web (:8082). README documents `stripe listen --forward-to localhost:5080/api/v1/webhooks/stripe`. CLAUDE.md §8 names a smoke port (:5090, db `ecommerce_smoke`), but **no smoke script exists** (`scripts/` holds only `verify.sh`).
- Dev seed exists (`Catalog/CatalogDevSeedTests.cs` covers it).

**Gap.** The §14 checkbox "Stripe test mode end to end: checkout → webhook → Paid → email → shipped → delivered" and the Slice 7 exit "staging end-to-end works" have no repeatable procedure. Backlog line 20 already asks the Slice 7 deploy smoke to upload and fetch an image through the CDN.

## 3. Agent / dev harness — what exists

- `scripts/verify.sh`: the steps run one at a time, each with a log, and print a `VERIFY PASS/FAIL` line. A `changed` mode exists. **There is no lock.** Two agents can run it at once, fight over the Release output, and load Docker twice. CLAUDE.md §5 forbids this, but only in prose.
- `.claude/settings.json`: the deny list covers commit, push, merge, rebase, reset, stash, checkout, switch, restore, clean and branch for `git` and for `git -c safe.directory=*`. **It does not cover:** `git add`, `tag`, `cherry-pick`, `revert`, `am`, `apply`, `rm`, `mv`, `worktree`, `update-ref`, `notes`, `pull`, `fetch`, or `git -C <dir> <write>`. All of these are "git writes of any kind" under CLAUDE.md §1. It also has no `Edit(.claude/**)` deny, though CLAUDE.md §1 forbids editing `.claude/`. There are no hooks.
- Mutations (CLAUDE.md §3: break → red → restore from scratch copy → `cmp`) are done by hand with `cp`/`cmp`. Agents cannot `git restore`, so a failed restore leaves broken production code in the tree, silently.
- Changed or deleted tests must be "listed and reviewed" (CLAUDE.md §3; quality-bar §3). Nothing produces that list.
- `AGENT_STATUS.md` drift seen today: when this task started, Rob's row was BUSY on P2.13 after P2.14 had been reviewed, and "Last reports → Marco" still points at a 9.7-gapfill report.
- `.github/workflows/ci.yml` already matches the quality-bar §4 order. **No CI change is needed.**

## 4. Task list

Tier follows CLAUDE.md §5. Every script reuses the existing `g()` read-only git wrapper style. No task adds a package.

| # | Task | Files | Enables / proves | Packages | Tier | Lands in |
|---|---|---|---|---|---|---|
| H1 | **Verify lock.** Use an atomic `mkdir artifacts/verify/.lock` holding the PID and start time, removed by `trap … EXIT`. If the lock is held by a live PID → exit 3 with "verify.sh already running (pid, since)". A dead PID → reclaim it and print that it was stale. | `scripts/lib/lock.sh` (new; consumers are H1, H3, H4), `scripts/verify.sh` | Enforces CLAUDE.md §5 "one verify/integration run at a time" | none | Low | Harness mini-slice (standalone prerequisite) |
| H2 | **`testdiff` step.** It lists test files (`backend/tests/**`, `*.test.ts(x)`) that are modified, deleted or untracked against `HEAD`, and counts removed `[Fact]`/`[Theory]`/`it(`/`test(` lines and removed `Assert`/`expect(` lines. It is informational only and never fails. It runs in the default and `changed` step lists. | `scripts/verify.sh` | Gives Rob the "changed/deleted tests" list for quality-bar §3 review without reading the whole diff | none | Low | Harness mini-slice |
| H3 | **Mutation helper.** `scripts/mutate.sh --file F --expr 'sed-E' (--dotnet PROJ --filter X \| --jest PATH)`. Steps: take the lock, copy F to `artifacts/mutate/<name>.orig`, apply the expression, and **refuse if `cmp` shows no change**. Run the tests and capture the last 20 lines. Restore from the copy under `trap EXIT INT TERM`, then `cmp` must be identical. Exit 0 = killed (red); 1 = survived (green, a finding); 2 = the mutation did not apply. If a `.orig` is left over from a hard kill, it refuses to start and prints the `cp` restore command. | `scripts/mutate.sh` (new) | CLAUDE.md §3 mutations (lock, ownership, dedupe guards) with the restore guaranteed and proven by `cmp` | none | Low (touches source only temporarily; restore is proven) | Harness mini-slice |
| H4 | **Repeat runner.** `scripts/repeat-tests.sh PROJ FILTER [N=20]`. It takes the lock, builds once, runs `dotnet test --no-build --filter` N times, stops at the first red and keeps its log, and prints `REPEAT n/N green`. | `scripts/repeat-tests.sh` (new) | Quality-bar §5 repeated-run proof for T1, T2/T3/T24 concurrent, T26, the outbox race | none | Low | Harness mini-slice |
| H5 | **API smoke against the real host.** `scripts/smoke.sh [base-url]` (default :5090, db `ecommerce_smoke`). Seed → register → cart → `POST /checkout` → confirm the intent with the Stripe CLI test card → wait for the signed webhook through `stripe listen` → poll the order to `Paid` → check one email in the dev pickup dir → admin ship → deliver → `/health/ready`. Keys come from env vars only, never a file. It fails fast with a plain message if `stripe` or a key is missing. The image upload and CDN fetch from backlog line 20 are an optional `--staging` stage. | `scripts/smoke.sh` (new), README "Smoke run" section | §14 "Stripe test mode end to end", "reservation expiry in staging with a short TTL" (`--ttl` flag sets `Inventory:ReservationTtl` on the smoke host), and the Slice 7 exit "staging end-to-end works" | none (curl, node for JSON, Stripe CLI already required by README) | **Medium** (payment flow, test mode only) | **Slice 7** |
| S1 | **Read-only git guard hook.** A PreToolUse hook on Bash and PowerShell. It splits on `&&`, `;`, `\|` and `\|\|`, strips `-c k=v` and `-C dir`, then allows only `status diff log show ls-files grep rev-parse blame cat-file shortlog describe`. Anything else → exit 2 with the CLAUDE.md §1 line. Covered by a table of allow/deny cases run by `scripts/test-guard.sh`. | `.claude/hooks/git-readonly.sh`, `.claude/settings.json` (hooks), `scripts/test-guard.sh` | Closes the deny-list gaps in §3, whatever form the command takes | none | Medium (a wrong rule blocks legitimate reads) | **Needs Nathaniel's sign-off** (`.claude/`) |
| S2 | Add `Edit(.claude/**)` and `Write(.claude/**)` to deny | `.claude/settings.json` | Enforces CLAUDE.md §1 | none | Low | **Needs Nathaniel's sign-off** |
| S3 | Allow `Bash(bash scripts/mutate.sh*)`, `Bash(bash scripts/repeat-tests.sh*)` and `Bash(bash scripts/smoke.sh*)` | `.claude/settings.json` | Lets agents run H3–H5 without prompts | none | Low | **Needs Nathaniel's sign-off**, after H3–H5 land |
| S4 | *(Optional)* SubagentStop hook: warn (never block) when the finishing agent's row in `AGENT_STATUS.md` is still BUSY | `.claude/hooks/status-check.sh`, `.claude/settings.json` | Stops the stale BUSY rows seen today | none | Low | **Needs Nathaniel's sign-off** |

**Not on the list (rejected):**
- Playwright, Detox or Maestro: §11.3 rules them out for v1, and they are new packages and new CI.
- MSW: §11.3 bans it.
- A .NET smoke console project: H5 does the same job with tools we already have.
- CI changes: CI already matches quality-bar §4.
- A shared TestKit project: see §1.

## 5. How to build it

**Order of work**
1. **H1, then H2, H3, H4** as one standalone "Harness H" mini-slice before the next P2 row group. It touches only `scripts/`, which CLAUDE.md §1 does not protect. Low tier: Marco builds, Rob reads the diff, no mutations of app code (CLAUDE.md §5).
2. **S1–S4**, only after Nathaniel's written request. They can land in any order relative to step 1, except S3, which goes after H3–H5.
3. **H5** inside Slice 7, alongside the staging deploy. Medium tier: Rob reviews the smoke flow against §8.2 and §14.

**How each part is checked**

| Task | Check (quote real output) |
|---|---|
| H1 | Start `verify.sh secrets` in the background and immediately run a second `verify.sh secrets`. The second exits 3 with the PID message. Then kill the first (`kill` its MSYS PID) and run again: the stale lock is reclaimed and the run says so. Finally a full `bash scripts/verify.sh` ends `VERIFY PASS`. |
| H2 | In a scratch copy of a test file, delete one `[Fact]` (restore after with `cp` + `cmp`). `verify.sh testdiff` lists the file as `M` and reports `-1 test`. A clean tree prints `no test changes`. |
| H3 | Run the known T1 lock mutation: drop `FOR UPDATE` in `backend/src/Modules/Inventory/Ecommerce.Inventory/Infrastructure/InventoryStore.cs`, `--dotnet backend/tests/Ecommerce.Api.IntegrationTests --filter ReserveConcurrencyTests`. Expect exit 0 (red) and `cmp` identical after. Run a deliberate no-op expression: expect exit 2. Ctrl-C mid-run: the file is restored and `cmp` is identical. |
| H4 | `repeat-tests.sh backend/tests/Ecommerce.Api.IntegrationTests T1_ 20` → `REPEAT 20/20 green`. A second concurrent `verify.sh` is refused by the lock. |
| H5 | On a local :5090 host with `stripe listen` and test keys in env vars, `smoke.sh` ends `SMOKE PASS` with the order ID and every status in sequence. Run it twice: the second run creates a new order and the first order is unchanged. With `stripe listen` stopped, it fails at the webhook wait with a plain message, and the order stays `PendingPayment`. |
| S1 | `scripts/test-guard.sh` runs at least 30 cases and every one matches its expected result. Cases include `git -C x commit`, `cd a && git add .`, `git -c safe.directory='*' tag v1`, `git log \| head`, and a `git status` written in PowerShell. Then a live attempt at `git add` is blocked with the §1 message. |
| S2–S4 | One attempted edit of `.claude/agents/rob.md` is denied (S2). The new scripts run without a prompt (S3). An agent that ends while still BUSY prints the warning (S4). |

**Doc upkeep when built.** Add one decision-log row (§13.2), "Dev harness scripts (lock, mutate, repeat, smoke)", after grepping for an existing row. Add CLAUDE.md §8 mentions of `mutate.sh`, `repeat-tests.sh` and `smoke.sh`; CLAUDE.md is Nathaniel's file, so that needs his request. H5 also ticks no §14 box on its own: Nathaniel runs it against staging.

## 6. Open decisions for Nathaniel

1. **Run "Harness H" (H1–H4) as a standalone mini-slice now**, outside the §12.3 order, or fold it into Slice 7? I recommend now: every remaining slice benefits from the lock and the mutation helper.
2. **Sign off on S1–S4** (`.claude/` changes). S1 and S2 close real gaps in the "git writes of any kind" and "no `.claude/` edits" rules. S4 is optional.
3. **Keep "no browser or device E2E in v1"** (§11.3)? I recommend yes; revisit Playwright for the web export after v1, with its own decision row.
4. **Smoke credentials:** H5 needs Stripe **test-mode** keys and the Stripe CLI on your machine, set in env vars, never committed. Will you run H5 yourself, or allow agents to run it with keys you export into the session?
5. **`testdiff` severity:** informational (recommended, because legitimate deletions happen) or failing on any removed `[Fact]`/`it(` without a `// removed:` justification line?
