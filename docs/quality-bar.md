# Quality Bar

**What code must prove before a slice reports PASS, and before it ships.** Scope lives in `docs/ecommerce-app-context.md`; design in `docs/ecommerce-architecture.md`. On an invariant, the architecture wins and this file is updated to match. On process, this file wins.

| Who | Enforces | Section |
|---|---|---|
| Marco (builder) | Self-check before reporting. Never commits | §2 |
| Rob (architect) | Code review. Architecture failures block; style never does | §3 |
| CI | Automated checks. Red blocks PASS and deploy | §4 |
| Nathaniel | Reads reports, commits, owns this file and waivers. Not an approval step | §7–8 |

No agent waives a gate or edits this file.

---

## 1. Blocking rules

**Architecture §3.4, rules 1–18.** Breaking any one blocks PASS. Rules 1–6 (money, idempotency, stock, Postgres truth, webhook truth, outbox) are never waived for a release.

Also blocking:

- A test weakened or deleted to get a pass instead of the code being fixed.
- Work from two slices at once, or an unrelated refactor mixed into a slice.
- Open decisions (Stripe keys, domain, email provider, bucket) filled with invented values instead of placeholders.

## 2. Marco's self-check

Answer every item in the report. "N/A" requires a reason.

- [ ] Module boundaries respected (rules 7–8, architecture §6.2): a module references only `Ecommerce.SharedKernel`, `Ecommerce.Persistence`, and other modules' `*.Contracts` — never another module's implementation project; implementation types are `internal` (the module's only public type is `<Module>Module`); no cross-module `DbSet` or join; no logic in controllers; new module code has tests in `Ecommerce.<Module>.Tests` and `Ecommerce.ArchitectureTests` is green.
- [ ] Every money or stock movement in this slice has a test (rule 12). Required tests for this slice (architecture §12.3) exist and pass.
- [ ] Frontend: every new or changed exported function/hook in `lib/` and `features/`, and every component with behavior, has a co-located test; the slice's frontend tests (architecture §11.3, F1–F8) exist and pass; no test under `app/`, no focused (`.only`/`fit`) test, no unexplained `.skip`. "No frontend change" is the only N/A.
- [ ] `IClock`, not `DateTime.UtcNow`, in domain and application code (rule 9).
- [ ] Prices and totals are calculated server-side only (rule 1).
- [ ] Idempotency and outbox handled for every external state change (rules 2, 6).
- [ ] Commands in §4 were run, and their **actual output** is pasted — not "tests pass" from memory.
- [ ] Open decisions are left as config placeholders.
- [ ] Docs were updated if a decision, workflow, or rule changed. Code that contradicts a doc is incomplete.
- [ ] Every change is explained in plain language in the report.

Any unchecked box without a written reason means the slice is not done, even if the app runs.

## 3. Rob's code review

Review in order. Stop at the first blocking failure.

**Blocking**

- [ ] **Stack and dependencies:** stack lock (architecture §2) intact; no unapproved package or service.
- [ ] **Slice order:** this is the next slice; the previous one reported PASS.
- [ ] **Boundaries:** module contracts only — a module project references only SharedKernel, Persistence, and other modules' `*.Contracts`, never another module's implementation or the host; implementation types `internal`, `InternalsVisibleTo` only to the module's test project (and `Ecommerce.Api.IntegrationTests`); thin controllers; inside a module, dependency direction Api → Application → Domain (folders/namespaces), Domain uses only SharedKernel and itself; per-module tests live in `Ecommerce.<Module>.Tests`; architecture tests (`Ecommerce.ArchitectureTests`) exist for every rule, are green, and were not weakened.
- [ ] **Invariants:** idempotency, reservation and expiry, webhook truth, outbox, legal transitions, price snapshots, audit entries.
- [ ] **Tests:** the slice's required tests exist, pass on real Postgres where required, and the test-first ones were shown failing first. Frontend tests (architecture §11.3) cover the slice's new logic and behavior, assert behavior (not snapshots alone), and F4–F6 were shown failing first.
- [ ] **Security:** per-resource auth, validated write DTOs, no mass assignment, webhook signature verified, rate limits considered, nothing secret logged.
- [ ] **Data:** migration is backward-compatible (expand/contract); indexes exist for new hot-path queries.
- [ ] **Frontend:** RN components only; shared screens and API client; no money logic on the client; checkout idempotency key; no optimistic checkout, payment, or return submit.
- [ ] **Waivers:** none expired (§8).

**Non-blocking** — naming, formatting, comments, log wording, minor UI polish. Fix it if trivial; otherwise file a follow-up.

**Report**

```
Verdict: PASS | BLOCKED
Blocking failures: [file:line — issue — fix] | None
Non-blocking notes: [...] | None
Decisions made: [decision — reason] | None
Files reviewed: [...]
```

Rob never approves a design and its implementation in the same review.

## 4. Automated checks

CI (GitHub Actions) runs these in order on every push and PR. None are optional.

1. **Build:** `dotnet build` and Expo type-check (`npx tsc --noEmit`, which also type-checks tests). Errors fail. New warnings are a review note until Slice 7, then they fail. No suppression without a reason.
2. **Unit tests:** `dotnet test` (domain + application). This includes the per-module test projects and `Ecommerce.ArchitectureTests` (module reference, visibility, layer, and clock rules — architecture §6.2). A test project that discovers zero tests fails (`TreatNoTestsAsError`). No skipped tests without a reason and a follow-up task. **Frontend:** `npm test -- --ci --coverage` in `frontend/apps/store` (Jest, architecture §11.3). Zero tests fail; `--passWithNoTests` is banned. A hygiene step fails on any test file under `app/` and on any focused test (`.only`, `fit`, `fdescribe`).
3. **Integration tests:** Testcontainers Postgres + WebApplicationFactory. In-memory EF does not count.
4. **Migrations:** apply cleanly to an empty Postgres database; no pending model changes (drift check). Command: `dotnet tool run dotnet-ef migrations has-pending-model-changes --project backend/src/Ecommerce.Persistence --startup-project backend/src/Ecommerce.Api`.
5. **Secret scan:** diff and repo (`sk_live`, `sk_test`, `whsec_`, private keys, passwords). Any hit blocks; a false positive is listed in the report with its reason.
6. **Artifacts:** API Docker image builds; `npx expo export --platform web` builds. EAS mobile builds run on demand for release candidates, not on every PR.
7. **Staging (after deploy):** `/health/live` and `/health/ready` return 200, or no traffic is routed.

Test results (TRX) and coverage reports (backend cobertura, frontend lcov) are uploaded as CI artifacts for reading — there is **no coverage percentage gate** (§5).

**Before reporting, Marco runs and pastes:** `dotnet build`, `dotnet test`, `npx tsc --noEmit`, `npm test -- --ci`, and `npx expo export --platform web` (the last three from `frontend/apps/store`, or "no frontend change").

## 5. Testing bar

Coverage percentage alone proves nothing — a repo can have 90% coverage and still oversell. The bar is: the **required tests (architecture §11.2, tests 1–19)** exist, use real Postgres where it matters, and the test-first tests (1, 2, 3, 4, 11, 13, 16, 19) were shown failing for the right reason before the fix.

- **Can't write the test?** You don't understand the invariant yet. Stop and ask Rob for a plan-mode challenge.
- **Frontend bar (architecture §11.3):** the client never proves money or stock — it proves the UI contracts around them: the checkout idempotency key is reused on retry (F4), checkout and returns are never optimistic (F5, F8), and the request carries no price or total while the displayed total is the server's (F6). F4–F6 are test-first. Tests query by accessible role and label (RNTL), assert behavior, and mock `fetch` with Jest only. A snapshot alone is not a test.
- **Silent suites are failures.** A test project or Jest run that finds zero tests fails CI, and focused tests are rejected — an empty or narrowed suite that reports green is worse than a red one.
- **Flaky tests are bugs.** Never add retries to hide them. Fix the race, clock, or isolation, and prove it with repeated runs.

## 6. Code bar

Beyond architecture §3 and §10:

- Frontend naming follows architecture §5.4. A route file renamed against Expo Router is a bug (it changes the URL), not a naming nit.
- No god classes or god repositories. A service that emails, charges, and ships gets split.
- No dead or commented-out code. A `TODO` names an owner and the slice that will fix it.
- Known-vulnerable packages are updated or replaced before release, unless waived.
- Accessibility: usable touch targets, readable text without zoom, plain-language errors with a retry action.
- Performance: optimize checkout by shortening transactions — never by removing a lock, a transaction, or an idempotency check. A slow query gets an index or a rewrite before more hardware.

## 7. Definition of done

**Slice PASS** when all of these are true:

- [ ] Rob's plan is READY (full plan-mode challenge for Orders, Inventory, Payments, and 6b).
- [ ] §2 self-check is complete.
- [ ] The slice's required tests pass, and every §4 command succeeds.
- [ ] §3 review is PASS with no open bugs, within 3 fix rounds.
- [ ] Docs are updated if a decision changed.
- [ ] The final report (`CLAUDE.md`) was delivered, both agents are AVAILABLE, and nothing was committed by an agent.

**Commit** (Nathaniel) once a slice is PASS — one slice per commit.

**Release allowed** when architecture §14 is fully green and the secret scan passes on the release commit.

## 8. Waivers

- Only Nathaniel grants waivers, and only in writing here.
- Rules 1–6 can be waived only on a named experiment branch that never ships.
- An expired waiver is a blocking failure again. Rob checks expiry dates on every review.

**Granted:** None.

Format: `Waiver: [rule] — Reason: [...] — Scope: [slice/release] — Expires: [date/slice] — Approved by: Nathaniel`
