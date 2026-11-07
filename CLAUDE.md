# CLAUDE.md

Single-store ecommerce app (web, Android, iOS). **Depth over breadth:** checkout, inventory, payments and notifications stay correct under concurrency, retries and crashes. **Minimal code:** the smallest change that meets the docs; nothing speculative or unplanned.

**Truth:** architecture (`docs/ecommerce-architecture.md` → `docs/architecture/`) for stack, invariants and boundaries · `docs/ecommerce-app-context.md` for scope · `docs/quality-bar.md` for done (Nathaniel's; no agent edits it). Never code from a prompt alone or an unread section.

## Token budget: read little, read once

**Read by task** (search only where listed): slice → plan `Read:` line + plan + `git diff --stat`; the plan's files · backend → arch §2, §3.4, §6 + cited; `backend/src/Modules/<M>`, `backend/tests/Ecommerce.<M>.Tests` · frontend → arch §5 + cited; `frontend/apps/store/features/<f>`, `lib/` · docs → index → file, `grep -n "^### N.N"` + `offset`/`limit` · decision → `grep -n -i "<term>"` on `13.2-decision-log.md` — grep only, never whole.

- **Once:** each file or section once per session, the plan included; never Read this file or your agent file (already loaded); no re-read after your own edit. Resumed rounds use context + handed-over items.
- **Ranges:** `grep -n`, then `offset`/`limit` or `sed -n a,bp`; no `cat` of several files or one over ~150 lines, nor of a file you just edited.
- **Cap output:** `verify.sh 2>&1 | tail -n 30` (quote the `VERIFY` line); build/test/jest/tsc `| tail -n 20`; Grep `head_limit`.
- **Banned:** repo-wide globs/greps; all of `docs/` · `bin/`, `obj/`, `node_modules/`, `dist/`, `artifacts/`, `.expo/` · lock files, migration `*.Designer.cs`/snapshots, `slice-plans/archive/` unless the task or plan is about them. **Widen** only with a one-line reason.
- **Caps:** plan ≤ 20 KB, written to size once · review round ≤ 10 KB · Marco report ≤ 6 KB. Cite sections (`arch §8.2`), never copy. Quality bar: Marco §1, §2, §4 · Rob §1, §3, §4.

## Guardrails

**Stack locked (arch §2):** .NET 10 modular monolith · EF Core · PostgreSQL 17 · Redis 7 (cache only) · Stripe PaymentIntents + signed webhooks · Expo + RN Web, one codebase · EAS cloud builds. No Android Studio, Xcode, Capacitor, committed `android/`/`ios/`, separate React web app or Kubernetes.

**Hard rules** (all 18: arch §3.4):
- **Money:** the server calculates every price and total.
- **Stock:** lock rows in variant-ID order, reserve in one transaction, commit only on Paid.
- **Payment:** the signed webhook is the only proof; dedupe by event ID.
- **Side effects:** via the outbox, in the same transaction. No external call inside a DB transaction.
- **Code:** thin controllers · no cross-module `DbSet` or join · `IClock`, not `DateTime.UtcNow`.
- **Boundaries (§6.2):** a module references only SharedKernel, Persistence and other modules' `*.Contracts`. Implementation types are `internal`; the only public type is `<M>Module`. `ArchitectureTests` enforce it.

**C# style:** no `var`, ever (IDE0008 build error + `verify.sh hygiene`); named records, not anonymous types.

**Never (any agent, any session):**
- **Git writes:** no `commit`, `push`, `merge`, `rebase`, `reset`, `stash`, `checkout`/`switch`, `restore`, `clean` or branch. Read-only only; Nathaniel commits.
- **Deploy** anywhere, or touch production data or secrets.
- **Secrets:** none in code, config, logs, docs or chat; never invent Stripe keys, domains, email providers or buckets (placeholders).
- **Packages and services:** only if Rob's plan names each, why, and what it replaces.
- **Data:** no dropping tables or columns, deleting databases or volumes (no `down -v`), or direct-SQL edits.
- **Tests:** never weaken, skip or delete one to get a PASS.
- **Processes:** never kill one you didn't start. The owner uses :5080, :8081, :5081 and :8082.
- **Protected files:** `docs/quality-bar.md`, `.github/workflows/`, `.claude/` — only on Nathaniel's written request.

## TDD (arch §11)

- **Hard invariants** test-first (§11.2/§11.3): red → green → refactor; quote the red output.
- **Same slice:** every money or stock movement, state transition and auth rule is tested in the slice that adds it.
- **New endpoint:** integration tests for the happy path, 401/403 and 400. **Frontend:** Jest for logic and screens.
- **Numbered tests:** §11.2 and F tests belong to the slice whose §12.3 row names them. No PASS without them.
- **Mutation check:** per security or money test, break the guard (lock, ownership, dedupe), see red, restore, report.
- **Changed or deleted tests** are listed and reviewed as a weakened invariant.
- **Coverage:** collected, no percentage gate (§11.4).

## Running a slice

**Team:** Nathaniel (owner, commits; approves only in Learning mode) · Rob (plans, reviews, no code) · Marco (builds, fixes). An agent missing from `/agents` → stop and say so; never role-play one.

**Autonomous (default)** — "Slice [N]", don't ask:
1. Rob → `slice-plans/slice-N-plan.md`.
2. Marco builds, runs the full `verify.sh` → `slice-N-marco-report.md`.
3. Rob reviews `git diff` + untracked against quality-bar §3, re-runs `verify.sh` → `slice-N-rob-review.md`.
4. On BLOCKED or an open bug: resume the same agents with only the blocking items. **Max 3 rounds.**
5. Final report (below); both agents AVAILABLE in `AGENT_STATUS.md`.

**Learning mode** (only when Nathaniel says so):
- He writes `slice-N-nathaniel-plan.md`; Rob completes it and names the gaps (2, 4, 6, 6b: also plans independently, lists differences).
- Marco never edits **Nathaniel drives** files; the session stops with `Waiting on Nathaniel: [files]`.
- Rob reviews all of it at the same bar and writes `slice-N-learning.md` (agents never read it).

**Always:**
- One slice per request, in order: 0 · 1a · 1b · 2 · 3 · 4 · 5 · 6 · 6b · 7.
- The previous slice's files move to `slice-plans/archive/`.
- Rob lists every decision; anything breaking §2 or §3.4 ends the slice `BLOCKED`.
- **Honesty:** `PASS` / `Bugs open: None` only if Rob's last verdict is PASS, the full `verify.sh` passed and no bug is open.

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
Git: Nothing committed — changes are in the working tree.
```

## Environment and commands

- Windows 11, PowerShell + Git Bash. Read-only git: `git -c safe.directory='*'`.
- Postgres: `docker compose up -d postgres` → `localhost:5434`, user/db `ecommerce`, trust auth (dev).
- API :5080; build `-c Release` (VS may hold Debug); smoke tests :5090. App: `frontend/apps/store`, `.env.local` from `.env.example`, `npx expo start` (web :8081).

```
dotnet test backend/tests/Ecommerce.<M>.Tests -c Release · npx jest <path> · bash scripts/verify.sh changed   # inner loop
bash scripts/verify.sh          # THE gate, once before a report
bash scripts/verify.sh docker   # image builds
```

Integration tests (Postgres container; Docker running) only when API or persistence changed. Quote real output, never memory.
