# Agent Status

Agents update this at the **start and end of every task**.
🟢 AVAILABLE — no task in progress · 🔴 BUSY — task in progress

| Agent | Status | Slice | Task | Fix round | Updated |
|---|---|---|---|---|---|
| Marco (developer) | 🟢 AVAILABLE | 4 | Fix round 1 done; full verify.sh PASS | 1 | 2026-11-07 10:00 |
| Tomas (developer 2) | 🟢 AVAILABLE | — | Ready | 0 | 2026-11-07 11:33 |
| Rob (architect, on demand) | 🟢 AVAILABLE | 4 | Review round 2: BLOCKED on owner §8 waiver line only (slice-plans/slice-4-rob-review.md) | 2 | 2026-11-07 03:55 |

**Rules**

- Never leave a task BUSY after reporting it done.
- Never mark AVAILABLE while the slice's tests are failing.
- `Updated` uses the format `YYYY-MM-DD HH:MM` (local time).

## Last reports

- Marco: slice-plans/slice-4-marco-report.md (previous: slice-plans/archive/slice-3-marco-report.md)
- Tomas: none yet
- Rob: slice-plans/slice-4-rob-review.md (previous: slice-plans/slice-4-plan.md)
