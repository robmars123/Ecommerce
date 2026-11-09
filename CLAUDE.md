# CLAUDE.md

Single-store ecommerce app (web, Android, iOS). **Depth over breadth:** checkout, inventory, payments and notifications stay correct under concurrency, retries and crashes. **Minimal code:** the smallest change that meets the docs; nothing speculative or unplanned.

**Truth:** never code from a prompt alone or from a section you haven't read.
- `docs/ecommerce-architecture.md` → `docs/architecture/`: stack, invariants, boundaries.
- `docs/ecommerce-app-context.md`: scope.
- `docs/quality-bar.md`: the definition of done. It is Nathaniel's; no agent edits it.

## 1. Never (any agent, any session)

- **Git writes:** no `commit`, `push`, `merge`, `rebase`, `reset`, `stash`, `checkout`/`switch`, `restore`, `clean` or branch. Read-only only, as `git -c safe.directory='*'`. Nathaniel commits.
- **Deploy** anywhere, or touch production data or secrets.
- **Secrets:** none in code, config, logs, docs or chat. Never invent Stripe keys, domains, email providers or buckets; use placeholders.
- **Packages and services:** only if Rob's plan names each one, why it's needed, and what it replaces.
- **Data:** no dropping tables or columns, deleting databases or volumes (no `down -v`), or direct-SQL edits.
- **Tests:** never weaken, skip or delete one to get a PASS.
- **Processes:** never kill one you didn't start. The owner uses :5080, :8081, :5081 and :8082.
- **Protected files:** `docs/quality-bar.md`, `.github/workflows/`, `.claude/`. Change them only on Nathaniel's written request.

## 2. Architecture (arch §2, §3.4, §6.2)

- **Stack locked:** .NET 10 modular monolith · EF Core · PostgreSQL 17 · Redis 7 (cache only) · Stripe PaymentIntents + signed webhooks · Expo + RN Web, one codebase · EAS cloud builds. No Android Studio, Xcode, Capacitor, committed `android/`/`ios/`, separate React web app or Kubernetes.
- **Hard rules** (all 18 are in §3.4):
  - **Money:** the server calculates every price and total.
  - **Stock:** lock rows in variant-ID order, reserve in one transaction, commit only on Paid.
  - **Payment:** the signed webhook is the only proof; dedupe by event ID.
  - **Side effects:** through the outbox, in the same transaction. No external call inside a DB transaction.
  - **Code:** thin controllers · no cross-module `DbSet` or join · `IClock`, not `DateTime.UtcNow`.
- **Boundaries:** a module references only SharedKernel, Persistence and other modules' `*.Contracts`. Implementation types are `internal`; the only public type is `<M>Module`. `ArchitectureTests` enforce this.
- **C#:** no `var`, ever (IDE0008 build error plus `verify.sh hygiene`). Use named records, not anonymous types.

## 3. Tests (arch §11)

- **Hard invariants are test-first** (§11.2/§11.3): red → green → refactor. Quote the red output.
- **Same slice:** every money or stock movement, state transition and auth rule is tested in the slice that adds it.
- **New endpoint:** integration tests for the happy path, 401/403 and 400. **Frontend:** Jest for logic and screens.
- **Numbered tests:** §11.2 and F tests belong to the slice whose §12.3 row names them. No PASS without them.
- **Mutation check:** one for each money or security guard named in the plan's required tests (lock, ownership, dedupe). Break the guard, see red, restore from a scratch copy, confirm with `cmp`, report. No extra mutations.
- **Changed or deleted tests** are listed and reviewed as a possibly weakened invariant.
- **Coverage** is collected, with no percentage gate (§11.4).

## 4. Team and flow

**Team:**
- **Nathaniel:** owner. Commits; approves only in Learning mode.
- **Rob:** plans and reviews; writes no code. Runs on opus.
- **Marco and Shin:** build and fix. Run on sonnet.

If an agent is missing from `/agents`, stop and say so. Never role-play one.

**Autonomous (default).** On "Slice [N]", don't ask:
1. Rob writes `slice-plans/slice-N-plan.md`.
2. Marco builds, runs the full `verify.sh`, and writes `slice-N-marco-report.md`.
3. Rob reviews `git diff` plus untracked files against quality-bar §3, by risk tier (§5), and writes `slice-N-rob-review.md`.
4. On BLOCKED or an open bug: a fresh Marco, then a fresh Rob, each given only the blocking items and their file paths. **Max 3 rounds.**
5. Give the final report (§7). All agents show AVAILABLE in `AGENT_STATUS.md`.

**Learning mode** (only when Nathaniel says so):
- Nathaniel writes `slice-N-nathaniel-plan.md`. Rob completes it and names the gaps. For slices 2, 4, 6 and 6b, Rob also plans independently and lists the differences.
- Marco never edits files marked **Nathaniel drives**. The session stops with `Waiting on Nathaniel: [files]`.
- Rob reviews all of it at the same bar and writes `slice-N-learning.md`. Agents never read that file.

**Always:**
- One slice per request, in order: 0 · 1a · 1b · 2 · 3 · 4 · 5 · 6 · 6b · 7.
- The previous slice's files move to `slice-plans/archive/`.
- Rob lists every decision. Anything that breaks §2 or §3.4 ends the slice `BLOCKED`.
- **Honesty:** report `PASS` and `Bugs open: None` only if Rob's last verdict is PASS, the full `verify.sh` passed and no bug is open.

## 5. Review budget

- **One review per slice.** That includes existing code still at "Review open": review it per slice, never per deliverable row.
- **Risk tiers:**
  - **High:** money, stock, payment, idempotency, auth and state transitions. Full review, plus the mutation checks in §3.
  - **Low:** read-only endpoints, copy, layout and docs. Marco's tests plus `verify.sh`; Rob reads the diff and runs no mutations.
- **Re-reviews:**
  - Marco quotes the red output for each of Rob's mutations.
  - Rob re-runs at most 2 of them and checks only the diff of the blocking items.
  - Re-reviews run on `model: sonnet`, unless a re-check finds a new BLOCKING issue.
- **verify.sh:**
  - Marco runs the full gate when he builds. In fix rounds he runs `verify.sh changed` plus the test projects he touched.
  - Rob runs the full gate once, in the round he gives PASS.
  - Only one `verify.sh` or integration run at a time: agents share Postgres and the Release build output.
- **Blocking only:** fix rounds fix BLOCKING items. NOTEs go to `slice-plans/backlog.md`.
- **No resumes:** never resume a finished agent, or one from an earlier round or task. Nathaniel sends follow-ups to the coordinator, who starts a fresh agent with only the items it needs.
- **Caps:**
  - Plan ≤ 20 KB, written to size once.
  - First review ≤ 10 KB; re-review round ≤ 3 KB.
  - Marco report ≤ 6 KB.
  - An agent's hand-back to the coordinator ≤ 1.5 KB: verdict, blocking items, the `VERIFY` line and files. The detail stays in the file.
  - Cite sections (`arch §8.2`), never copy them.
  - Quality bar: Marco covers §1, §2 and §4; Rob covers §1, §3 and §4.

## 6. Reading: read little, read once

- **Read by task.** Search only where listed:
  - **Slice:** the plan's `Read:` line, the plan and `git diff --stat`, then the plan's files.
  - **Backend:** arch §2, §3.4, §6 plus cited sections; `backend/src/Modules/<M>` and `backend/tests/Ecommerce.<M>.Tests`.
  - **Frontend:** arch §5 plus cited sections; `frontend/apps/store/features/<f>` and `lib/`.
  - **Docs:** the index, then the file. Use `grep -n "^### N.N"` with `offset`/`limit`.
  - **Decisions:** `grep -n -i "<term>"` on `13.2-decision-log.md`. Grep only, never the whole file.
- **Once:**
  - Read each file or section once per session, the plan included.
  - Never Read this file or your agent file; they are already loaded.
  - Don't re-read a file after your own edit.
  - Fix rounds use only the handed-over items.
  - In a review thread, read only the latest round: `grep -n "^## Round"`, then `offset`.
- **Ranges:** `grep -n`, then `offset`/`limit` or `sed -n a,bp`. No `cat` of several files, or of one over ~150 lines.
- **Cap output:**
  - `verify.sh 2>&1 | tail -n 30` (quote the `VERIFY` line).
  - Build, test, jest and tsc: `| tail -n 20`.
  - Grep: use `head_limit`.
- **Banned** unless the task or plan is about them:
  - repo-wide globs and greps, and all of `docs/` at once;
  - `bin/`, `obj/`, `node_modules/`, `dist/`, `artifacts/`, `.expo/`;
  - lock files, migration `*.Designer.cs` and snapshots, `slice-plans/archive/`.

  **Widen** only with a one-line reason.

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
Git: Nothing committed — changes are in the working tree.
```

## 8. Environment and commands

- Windows 11, PowerShell + Git Bash.
- Postgres: `docker compose up -d postgres` → `localhost:5434`, user/db `ecommerce`, trust auth (dev).
- API on :5080; build with `-c Release` (VS may hold Debug). Smoke tests on :5090.
- App: `frontend/apps/store`, `.env.local` from `.env.example`, `npx expo start` (web :8081).
- Integration tests need Docker running (Postgres container). Run them only when the API or persistence changed.
- Quote real output, never memory.

```
dotnet test backend/tests/Ecommerce.<M>.Tests -c Release · npx jest <path> · bash scripts/verify.sh changed   # inner loop
bash scripts/verify.sh          # THE gate
bash scripts/verify.sh docker   # image builds
```
