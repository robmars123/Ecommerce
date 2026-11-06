# Ecommerce Architecture — index

**Single source of truth for design, invariants, tests, and slice order.** Content lives in `docs/architecture/`, one file per section. If a request conflicts with it, the slice ends `BLOCKED` with the conflict in its report — never silently pick the easier path.

Section numbers (`arch §8.2`) are cited everywhere. **Never renumber**; a new subsection goes at the end of its file.

## How to read it

1. Find the sections for your task in the table below. Open **only** those files.
2. Inside a big file, jump to a subsection: `grep -n "^### 8.2" docs/architecture/08-workflows.md`, then Read with `offset`/`limit`.
3. **Never** read every file, glob the folder, or read the decision log end to end. Search the log: `grep -n -i "refresh" docs/architecture/13.2-decision-log.md`.

## What to read, by task

| Task touches | Architecture | App context |
|---|---|---|
| **Always** | §2, §3.4, the slice's §12.3 row (`grep -n "^| 1b " docs/architecture/12-process-slice-plan.md`) | — |
| Module, project, or layer | §3.1, §6 | — |
| Schema, migrations, indexes, statuses | §7 | — |
| Endpoints, auth, security | §9, §10.1 | §2 |
| Browse, cart | §8.1 | §4.1–4.4 |
| Checkout, payment, refunds | §8.2, §8.4, §8.5 | §4.5, §4.7 |
| Inventory | §8.3, §8.7 | §4.10 |
| Fulfillment, orders | §7.3, §8.6 | §4.6, §4.11 |
| Projections, saga, dashboard | §8.8 | §4.8 |
| Caching, observability, scaling, deploy | §10.2–10.5 | — |
| Frontend | §5 | §5 |
| Tests | §11 (required list: §11.2) | — |
| Making or checking a decision | §13.0; grep §13.2 | §6 |
| Release | §14 | — |

## Files

| § | File | KB | Holds |
|---|---|---|---|
| 1 | [01-context.md](architecture/01-context.md) | 1 | Purpose, constraints |
| 2 | [02-stack-lock.md](architecture/02-stack-lock.md) | 3 | Locked stack, open decisions |
| 3 | [03-principles-hard-rules.md](architecture/03-principles-hard-rules.md) | 5 | §3.1 monolith · §3.2 SOLID · §3.3 production-ready · **§3.4 the 18 hard rules** |
| 4 | [04-system-diagram.md](architecture/04-system-diagram.md) | 1 | Components |
| 5 | [05-frontend.md](architecture/05-frontend.md) | 10 | §5.1 structure · §5.2 rules · §5.3 routes · §5.4 naming · §5.5 styling |
| 6 | [06-backend-modules.md](architecture/06-backend-modules.md) | 8 | §6.1 modules · §6.2 layout, layers, boundary tests |
| 7 | [07-data-model.md](architecture/07-data-model.md) | 10 | §7.1 tables · §7.2 indexes · §7.3 state machines · §7.4 configured values (stock threshold, shipping, tax, carriers, totals) |
| 8 | [08-workflows.md](architecture/08-workflows.md) | 9 | §8.1 browse, cart · **§8.2 checkout, retry payment** · §8.3 race · §8.4 outbox |
| 8.5 | [08.5-refund-fulfillment-admin.md](architecture/08.5-refund-fulfillment-admin.md) | 4 | §8.5 cancellation and refund · §8.6 fulfillment · §8.7 admin catalog and inventory |
| 8.8 | [08.8-eventual-consistency.md](architecture/08.8-eventual-consistency.md) | 2 | §8.8 eventual consistency (Slice 6b) |
| 9 | [09-api.md](architecture/09-api.md) | 8 | Endpoints · catalog, auth, cart, checkout, orders, refund contracts |
| 10 | [10-cross-cutting.md](architecture/10-cross-cutting.md) | 4 | §10.1 security · §10.2 caching · §10.3 observability · §10.4 scaling · §10.5 deploy |
| 11 | [11-testing.md](architecture/11-testing.md) | 10 | §11.1 pyramid · §11.2 required tests · §11.3 frontend · §11.4 CI |
| 12 | [12-process-slice-plan.md](architecture/12-process-slice-plan.md) | 6 | §12.1 ownership · §12.2 process · **§12.3 slice plan** |
| 13 | [13-tradeoffs.md](architecture/13-tradeoffs.md) | 6 | §13.0 constraints · §13.1 stack tradeoffs |
| 13.2 | [13.2-decision-log.md](architecture/13.2-decision-log.md) | 21 | Decision log — **append-only, search don't read** |
| 14 | [14-release-gate.md](architecture/14-release-gate.md) | 1 | Production readiness |
| 15 | [15-next-steps.md](architecture/15-next-steps.md) | 0.4 | Next steps |

## Editing rules

- A new decision is **one appended row** in `13.2-decision-log.md`. Grep the log for an existing row first; update that row instead of adding a duplicate.
- Edit the section file, never this index — except to add a file or fix a size above ~10% off.
- No section file over ~10 KB. If one grows past it, split at a `###` boundary into a new file and add it here.
- No "Updated …" changelog lines in files — git history is the changelog.

| Related doc | Answers | Wins on |
|---|---|---|
| `docs/ecommerce-app-context.md` | What does it do, for whom? | v1 product scope, workflows, screens |
| `docs/quality-bar.md` | What must code prove to merge and ship? | Process gates |
