# Slice P2.36 — Review thread

## Round 1 — Rob → Marco
Verdict: PASS · verify.sh: `VERIFY PASS (secrets hygiene format build test drift expo-check tsc jest export)` — build 0 warnings, jest 193 suites / 1503 tests.

Low tier, layout only. The diff is the two planned files and nothing else under `frontend/`; the decision path is untouched. No mutations (plan's Review method).

### What I checked
- **`git diff --stat`:** `app/admin/returns.tsx`, `features/admin-orders/components/AdminReturnsView.tsx`, `AGENT_STATUS.md`, plus the P2.37 files moved to `slice-plans/archive/` (coordinator housekeeping, not slice code). Untracked: the P2.36 plan and your report, and the archived P2.37 files.
- **`ReturnCard`, line by line:** exactly two changed lines, both `className=` values: the root (`rounded-control border … p-md` → `border-b border-border py-md`) and the `open` block (`gap-sm` → `gap-sm rounded-control bg-surface-raised p-md`). `act`, the `decide.mutate` payload, the `open` condition, both `disabled` guards, `pending`, labels and the alert `Text` are identical to `HEAD`.
- **Chips:** the `key`, `accessibilityRole`, `accessibilityState` and `onPress` body (`setStatus` + `setPage(1)`) are unchanged, and so is the label text. The classes match the orders/products chip pattern, and every token exists in `lib/design-tokens.js`.
- **Pager:** before, it rendered only inside `query.data` → `items.length !== 0` → `totalPages > 1`. Now the condition is `query.data && query.data.items.length > 0 && query.data.totalPages > 1`, which is the same. The buttons, `onPress`, `disabled` and the `Page X of Y` string are byte-identical.
- **Results branch:** the same data / empty / error / loading order inside one `AdminPanel`, and the same strings.
- **Header:** `Stack.Screen headerShown: false` follows the P2.34 pattern, and `ScreenTitle` is unchanged.
- **Freeze list:** `git diff --stat` on hooks, `api.ts`, `types.ts`, the admin-orders tests, `components/` (AdminPanel and the shared components), `app/admin/_layout.tsx`, `features/admin-shell`, `features/auth` is empty. No test was changed or added.
- **Theme rule:** there is no `style=`, no hex and no arbitrary `[...]` value in the diff.

### 1. `keyboardShouldPersistTaps` on admin ScrollViews — NOTE
**What I saw:** the new `ScrollView` root doesn't set it, as the plan says.
**Why it matters:** on native, the first tap on Approve/Decline after typing a note may only dismiss the keyboard. Orders and categories have the same gap.
**What I want:** nothing in this slice. It's logged in `slice-plans/backlog.md` so all admin forms get one decision.
**Lesson:** a cross-screen behaviour change belongs in its own row, not in a layout row.

**What was good:** you kept the pager's guard as one explicit conjunction instead of nesting it back into the empty-state branch, so it's easy to see it equals `HEAD`. You also kept `ReturnCard` to two className edits, which made the freeze check a one-minute read.

Manual check (plan step 8, 1280/375 px plus native note → Approve) is still open for Nathaniel.
