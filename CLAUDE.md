# CLAUDE.md

Single-store ecommerce app (web, Android, iOS) built to production standards to practice staff-level system design. **Depth over breadth:** checkout, inventory, payments, and notifications must stay correct under concurrency, retries, and crashes.

## Read first, in order

1. `docs/ecommerce-architecture.md` — stack lock (§2), hard rules (§3.4), data model and state machines (§7), checkout (§8.2), eventual consistency (§8.8), API (§9), required tests (§11.2), slice order (§12.3).
2. `docs/ecommerce-app-context.md` — v1 scope, roles, workflows, screens, out of scope.
3. `docs/quality-bar.md` — the gate every slice must pass.

Never code from a prompt alone, or from a doc you haven't read.

## Stack (locked — details in architecture §2)

.NET 10 / ASP.NET Core / EF Core / PostgreSQL 17 modular monolith · Redis 7 (cache only) · Stripe PaymentIntents + signed webhooks (test mode) · Expo + React Native + RN Web, one codebase, EAS cloud builds — no Android Studio, Xcode, Capacitor, committed `android/`/`ios/`, or separate React web app.

## Team

- **Nathaniel** — owner. Reads the final report and the code review, and commits when ready. **Not an approval step.**
- **Rob** (`~/.claude/agents/rob.md`) — architect. Plans and reviews. Writes no implementation code.
- **Marco** (`~/.claude/agents/marco.md`) — developer. Builds and fixes.

## Running a slice — fully autonomous, no approvals

When asked to build Slice [N], the main session orchestrates the whole thing without stopping to ask:

1. **Plan** — run `rob`: plan Slice [N] and list risks, gates, and required tests.
2. **Build** — run `marco` with Rob's plan: implement it, run the commands, and complete the quality-bar §2 self-check.
3. **Review** — run `rob` on the diff (`git diff` + untracked files) against quality-bar §3.
4. **Fix loop** — on BLOCKED, or on any open bug, run `marco` with Rob's blocking items, then repeat step 3. **Maximum 3 fix rounds.**
5. **Report** — give Nathaniel the final report (below). Set both agents AVAILABLE in `AGENT_STATUS.md`.

One slice per request, in slice order. Never start the next slice on your own.

**Honesty rule:** report `PASS` and `Bugs open: None` only when Rob's last verdict is PASS, every §4 command succeeded, and no bug is open. If 3 rounds can't reach that, report `BLOCKED` with the reasons — never claim a pass.

**Decisions:** agents don't wait for approval. Rob decides within the docs and lists every decision in the report. Anything that would break the stack lock (architecture §2) or a hard rule (§3.4) is not decided — the slice ends `BLOCKED` with the reason.

### Final report

```
Slice [N] — [name]
Code review:   Done by Rob — Verdict: PASS | BLOCKED
Quality bar:   PASS | BLOCKED  (§2 self-check · §3 review · §4 commands)
Bugs open:     None | [list]
Fix rounds:    [0–3]
Decisions made: [Rob's decisions] | None
Review notes:  [non-blocking notes] | None
Files changed: [list]
Commands:      dotnet build ✔ · dotnet test ✔ [n passed] · tsc ✔ · npm test ✔ [n passed] · expo export ✔
Status:        Marco AVAILABLE · Rob AVAILABLE
Git:           Nothing committed — changes are in the working tree.
```

Follow it with Rob's full code review, so Nathaniel can read it.

**Slice order:** 0 Foundation · 1 Identity + Catalog · 2 Inventory · 3 Cart · 4 Checkout + Payments · 5 Orders + Fulfillment · 6 Notifications + Returns · 6b Eventual Consistency · 7 Hardening. Never skip ahead.

## Rules you're most likely to break (full list: architecture §3.4)

- The server calculates every price and total. Never trust the client for money.
- Lock stock rows in variant-ID order, reserve in one transaction, commit only on Paid.
- The signed webhook is the only proof of payment. Dedupe by event ID.
- Side effects go through the outbox in the same transaction. No external calls inside a DB transaction.
- Thin controllers. No cross-module `DbSet` or join. `IClock`, never `DateTime.UtcNow`.
- A module (`backend/src/Modules/<M>/Ecommerce.<M>`) references only `Ecommerce.SharedKernel`, `Ecommerce.Persistence`, and other modules' `*.Contracts`. Implementation types are `internal`; the only public type is `<M>Module`. `Ecommerce.ArchitectureTests` enforces it (architecture §6.2).
- Every money or stock movement is tested in the same slice. Hard invariants are test-first.

## Never — no agent, no session

- **Git writes:** no `commit`, `push`, `merge`, `rebase`, `reset`, `stash`, `checkout`/`switch`, or branch creation. Read-only `git status`, `git diff`, and `git log` are fine. Nathaniel commits.
- Deploy anywhere, or touch production data or secrets.
- Put secrets in code, logs, or chat — or invent Stripe keys, domains, email providers, or buckets (use config placeholders).
- Add a package or service outside the stack lock without Rob listing it as a decision, with its reason and what it replaces.
- Drop tables or columns, delete databases, or edit data with direct SQL.
- Weaken or delete a test to get a PASS.

## Commands

Available once Slice 0 lands:

```
dotnet build
dotnet test                           # needs Docker running (Testcontainers)
dotnet tool run dotnet-ef migrations has-pending-model-changes --project backend/src/Ecommerce.Persistence --startup-project backend/src/Ecommerce.Api
cd frontend/apps/store && npx tsc --noEmit
cd frontend/apps/store && npm test -- --ci
cd frontend/apps/store && npx expo export --platform web
```

Reports quote real output, never a summary from memory.

## Check

`rob` and `marco` must appear in `/agents` (user agents, `~/.claude/agents/`). If they're missing, stop and say so. Never role-play an agent without its file.
