# Slice P2.57 — Theme toggle label names the target theme

Tracker row: P2.57 | Theme toggle label names the theme it switches to: in Light it shows "Dark", in Dark it shows "Light" | `components`
Risk tier: **Low** (copy only; no money, stock, auth or state-transition change). CLAUDE.md §5: Marco's tests + `verify.sh`, Rob reads the diff, no mutations.

## 1. Read

- This plan.
- `frontend/apps/store/components/ThemeToggle.tsx`, `frontend/apps/store/components/ThemeToggle.test.tsx`.
- No architecture sections needed beyond CLAUDE.md §2 (frontend layout) and quality-bar §1, §3, §4.

## 2. Decisions

| Decision | Reason | Alternative rejected |
|---|---|---|
| Flip only the visible label: `mode === 'dark' ? 'Light' : 'Dark'` | Smallest change meeting the row; store, toggle behaviour and callers (TopNav, AdminShell) untouched | Replacing the switch with a button ("Switch to light") — wider change, touches a11y role and callers for no requirement |
| Keep `accessibilityRole="switch"`, `accessibilityLabel="Dark mode"`, `accessibilityState.checked = mode === 'dark'` | The accessible name overrides the visible text; screen readers still announce "Dark mode, switch, on/off", which stays truthful. Changing it would break TG2's role queries for no spec reason | Accessible label tracking the visible text ("Light") — a checked switch named "Light" while in dark mode would be read as wrong state |
| No decision-log row | Copy change, no design decision the docs lack | — |

## 3. Files

- `frontend/apps/store/components/ThemeToggle.tsx` — line 16 label expression only.
- `frontend/apps/store/components/ThemeToggle.test.tsx` — TG1, TG2 text assertions (spec change, see §5).
- `docs/admin-dashboard-feature-tracker.md` — row P2.57 → Done after PASS.

No backend, no migrations, no new packages. Grep confirmed: no other test (`TopNav.test.tsx`, `AdminShell.test.tsx`, `admin-phone-width.test.tsx`) asserts 'Dark'/'Light' text — only `ThemeToggle.test.tsx:16` and `:23`.

## 4. Build order

1. Change the tests first (§5), run `npx jest components/ThemeToggle` from `frontend/apps/store` → quote red (TG1 cannot find "Light"; TG2 cannot find "Dark").
2. Flip the label in `ThemeToggle.tsx:16` → green.
3. Full `bash scripts/verify.sh 2>&1 | tail -n 30`, quote the `VERIFY` line → `slice-P2.57-marco-report.md`.

## 5. Required tests (test-first)

Changed tests — **spec changes, not weakened invariants** (the row redefines the label; role, name, checked-state and store assertions all stay):

- **TG1** (rename to `TG1: in Dark shows a checked Dark mode switch labelled Light`): keep the role/name/`toBeChecked` assertions; replace `getByText('Dark')` with `getByText('Light')`, and add `expect(screen.queryByText('Dark')).toBeNull()`.
- **TG2** (`pressing it flips to light and back`): after the first press keep `not.toBeChecked()` and the store `'light'` assertion; replace `getByText('Light')` with `getByText('Dark')`. After the second press keep `toBeChecked()` and add `getByText('Light')` so both directions of the label are covered.

No new tests, no deleted tests. No mutations (Low tier).

## 6. Risks

- Users may read "Light" while in dark mode as the current state. Accepted: that is exactly Nathaniel's requested behaviour; the switch's accessible state stays correct.
- None architectural.
