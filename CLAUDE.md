# CLAUDE.md

Single-store ecommerce (web, Android, iOS). Checkout, inventory, payments and notifications stay correct under concurrency, retries and crashes. Smallest change that meets the docs; nothing speculative.

**Truth:** code only from docs you've read. `docs/ecommerce-architecture.md` (index → `docs/architecture/NN-*.md`), `docs/ecommerce-app-context.md` (scope), `docs/quality-bar.md` (definition of done; Nathaniel's, never edited).

## 1. Never

- **Git writes** of any kind (push, merge, rebase, reset, stash, checkout/switch, restore, clean, branch, amend). Read-only as `git -c safe.directory='*'`. Only exception: Marco's slice commit (§4 step 5).
- Deploy, or touch production data or secrets. No secrets anywhere; placeholders for Stripe keys, domains, email providers, buckets.
- New packages/services unless Rob's plan names each one, why, and what it replaces.
- Drop tables/columns, delete DBs/volumes (`down -v`), or direct-SQL edits.
- Weaken, skip or delete a test to get a PASS.
- Kill a process you didn't start. Owner ports: :5080, :8081, :5081, :8082.
- Edit `docs/quality-bar.md`, `.github/workflows/`, `.claude/` without Nathaniel's written request.

## 2. Architecture (arch §2, §3.4, §6.2)

- **Stack locked:** .NET 10 modular monolith · EF Core · PostgreSQL 17 · Redis 7 (cache only) · Stripe PaymentIntents + signed webhooks · Expo + RN Web (one codebase, EAS builds). No native `android/`/`ios/`, separate web app, Capacitor or Kubernetes.
- **Hard rules (§3.4):** server computes all money · lock stock rows in variant-ID order, reserve in one tx, commit on Paid · signed webhook is the only payment proof, deduped by event ID · side effects via outbox in the same tx, no external call inside a tx · thin controllers · no cross-module `DbSet`/join · `IClock`, never `DateTime.UtcNow`.
- **Layout:** `backend/src/{Ecommerce.Api, Ecommerce.Persistence, Ecommerce.SharedKernel, Modules/<M>}`; modules: Admin, Cart, Catalog, Identity, Inventory, Notifications, Orders, Payments. A module references only SharedKernel, Persistence and other `*.Contracts`; only `<M>Module` is public (`Ecommerce.ArchitectureTests`). Tests: `backend/tests/Ecommerce.<M>.Tests`, `Ecommerce.Api.IntegrationTests`. Frontend: `frontend/apps/store/{app, features/<f>, lib, components, tests}`.
- **C#:** no `var` (IDE0008 error + hygiene step); named records, no anonymous types.

## 3. Tests (arch §11)

- Hard invariants test-first (red → green → refactor); quote the red output.
- Every money/stock movement, state transition and auth rule is tested in the slice that adds it. New endpoint: integration tests for happy, 401/403, 400. Frontend: Jest.
- §11.2 numbered and F tests belong to the slice whose §12.3 row names them; no PASS without them.
- **Mutations:** one per money/security guard in the plan's required tests (lock, ownership, dedupe). Break → red → restore from scratch copy → `cmp` → report. No extras.
- Changed/deleted tests are listed and reviewed as possibly weakened invariants. Coverage collected, no gate.

## 4. Team and flow

Nathaniel: owner. Rob (opus): plans and reviews, no code. Marco, Shin (sonnet): build and fix. Agent missing from `/agents` → stop and say so; never role-play one. Agent files hold their own procedures and caps.

**Autonomous (default)** — on "Slice N", don't ask:
1. Rob → `slice-plans/slice-N-plan.md`.
2. Marco builds, runs full `verify.sh` → `slice-N-marco-report.md`.
3. Rob reviews diff + untracked files (quality-bar §3, risk tier §5) → `slice-N-rob-review.md`.
4. BLOCKED or bug open: fresh Marco, then fresh Rob, given only the blocking items + paths. Max 3 rounds.
5. Commit (Nathaniel's standing request, 2026-11-12): only after Rob's last verdict is PASS, full `verify.sh` passed and no bug is open, a fresh Marco runs `git add` on the slice's files (never `-A`/`.`; no secrets, `artifacts/`, `bin/ obj/`) and one `git commit` on `dev` (check with `git rev-parse --abbrev-ref HEAD`; any other branch → stop). Name slices by tracker row ID, never "tier" (one row → `P2.19`; several → first ID, e.g. `P2.18`, files `slice-P2.18-*.md`). Message: subject `<row IDs> | <short summary>` (e.g. `P2.18 P2.19 P2.20 P2.9 | Dashboard tabs, user stories, live pill`); body has one line per tracker row done, in the template `ID | what was done | Module` (wording may summarise the row; ID and Module from the tracker) (e.g. ``P2.45 | Support gets 403 on export, trend and settings, and never sees revenue | API, `admin-*` ``), then the Co-Authored-By line. No push, no amend, no hooks skipped. Commits stay on `dev`: never merge, cherry-pick or push to `master`/`main`; Nathaniel reviews and merges by hand.
6. Final report (§7); all agents AVAILABLE in `AGENT_STATUS.md`.

**Learning mode** (only when Nathaniel says): Nathaniel writes `slice-N-nathaniel-plan.md`; Rob completes it; Marco never edits **Nathaniel drives** files; stop with `Waiting on Nathaniel: [files]`. Agents never read `slice-N-learning.md`.

**Always:** one slice per request, in order 0 · 1a · 1b · 2 · 3 · 4 · 5 · 6 · 6b · 7. Previous slice's files → `slice-plans/archive/`. A §2/§3.4 breach ends the slice BLOCKED. Report PASS / `Bugs open: None` only if Rob's last verdict is PASS, full `verify.sh` passed, and no bug is open.

## 5. Review budget

- One review per slice. **High risk** (money, stock, payment, idempotency, auth, state transitions): full review + §3 mutations. **Low** (read-only endpoints, copy, layout, docs): Marco's tests + `verify.sh`, Rob reads the diff, no mutations.
- Re-reviews: Marco quotes red for each of Rob's mutations; Rob re-runs ≤ 2 and checks only the blocking diff, on `model: sonnet` unless a new BLOCKING issue appears.
- `verify.sh`: full on build and in Rob's PASS round; fix rounds use `verify.sh changed` + touched test projects. One `verify.sh`/integration run at a time (shared Postgres and Release output).
- Fix rounds fix BLOCKING only; NOTEs → `slice-plans/backlog.md`. Never resume a finished or earlier-round agent.
- Caps: plan ≤ 20 KB · first review ≤ 10 KB · re-review ≤ 3 KB · Marco report ≤ 6 KB · hand-back to coordinator ≤ 1.5 KB (verdict, blockers, `VERIFY` line, files). Cite sections, never copy.

## 6. Reading

- Read only what the task needs, once per session; never re-read after your own edit, or Read CLAUDE.md/agent files.
- Slice: the plan's `Read:` line, the plan, `git diff --stat`, then its files. Docs: index, then `grep -n "^### N.N"` + `offset`/`limit`. Decision log (`13.2-decision-log.md`): grep only. Review threads: latest `## Round` only.
- Cap output: `verify.sh 2>&1 | tail -n 30` (quote `VERIFY` line); build/test/jest/tsc `| tail -n 20`; Grep with `head_limit`; no `cat` of files > ~150 lines.
- Banned unless the task is about them (widen with a one-line reason): repo-wide globs/greps, all of `docs/`, `bin/ obj/ node_modules/ dist/ artifacts/ .expo/`, lock files, migration `*.Designer.cs`/snapshots, `slice-plans/archive/`.

## 7. Final report

```
Slice [N] — [name]
Mode: Autonomous | Learning
Code review: Done by Rob — Verdict: PASS | BLOCKED
Quality bar: PASS | BLOCKED (§2 · §3 · §4)
Bugs open: None | [list]
Fix rounds: [0–3]
Decisions made: [list] | None
Review notes: [list] | None
Files changed: [list]
verify.sh: secrets ✔ · hygiene ✔ · build ✔ [n warnings] · test ✔ [n] · drift ✔ · expo-check ✔ · tsc ✔ · jest ✔ [n] · export ✔
Status: Marco AVAILABLE · Rob AVAILABLE
Git: Committed to dev — [short hash] [subject] | Not committed — [reason]
```

## 8. Environment

Windows 11, PowerShell + Git Bash. Quote real output, never memory.
- Postgres: `docker compose up -d postgres` → `localhost:5434`, user/db `ecommerce`, trust auth. Integration tests need Docker.
- API: `dotnet ... -c Release` on :5080 (smoke runs :5090, db `ecommerce_smoke`). Compose stack: api :5081, web :8082.
- App: `frontend/apps/store`, `.env.local` from `.env.example`, `npx expo start` (web :8081).
- `bash scripts/verify.sh` = the gate (secrets hygiene build test drift expo-check tsc jest export; logs in `artifacts/verify/`). `verify.sh changed` = inner loop. `verify.sh docker` = image builds. Single test project: `dotnet test backend/tests/Ecommerce.<M>.Tests -c Release`.
