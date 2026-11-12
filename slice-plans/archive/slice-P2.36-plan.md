# Slice P2.36 — Returns restyle

Tracker row P2.36 "Returns" (`admin-orders`, tier 1, "layout only"); nothing outside this row. Tracker footer: restyle rows keep existing tests unchanged; tier-1 rows use theme tokens only. Conventions carried from `slice-plans/archive/slice-P2.34-plan.md` and `slice-plans/archive/slice-P2.37-plan.md` (header hidden per route, `ScrollView` page container, `AdminPanel`, orders chip pattern, className-only inside components that hold actions or forms, no copy changes).

Mode: Autonomous. **Risk tier: Low.** The screen holds the **approve / decline** return decision (a state transition, Requested → Approved/Declined). There is no refund action and no money input on this screen (`Refunded` is only a filter value). The decision logic is **frozen**: `ReturnCard` changes `className` values only, and hooks, API and types stay untouched, so nothing behavioural changes and the tier stays Low (same as `OrderActions`/`RefundForm` in P2.35). Role gating is `AdminGate` in `app/admin/_layout.tsx` plus server policies, not in this slice. Review: Marco's tests + full `verify.sh` + Rob reads the diff and checks `ReturnCard` line by line; no mutations. If anything in the decision path has to change, stop: the tier would become High and need a new plan.

## Read:

- This plan.
- `frontend/apps/store/features/admin-orders/components/AdminReturnsView.tsx` (the only feature file that changes) and `AdminReturnsView.test.tsx`: test names and queries only.
- `frontend/apps/store/app/admin/returns.tsx`; `app/admin/orders/index.tsx` (header-hiding pattern).
- `frontend/apps/store/components/AdminPanel.tsx`.
- `frontend/apps/store/features/admin-orders/components/AdminOrderListView.tsx` lines 70–152 (container, chip, panel, row and pager classes to copy).
- `frontend/apps/store/lib/design-tokens.js` (token names only).
- `docs/quality-bar.md` §1, §3, §4. Nothing else.

**Docs (Rob):** none needed. There is no new design decision; the conventions are the P2.34 ones. The coordinator sets the tracker row to Done after PASS.

## Decisions

| Decision | Reason | Alternative rejected |
|---|---|---|
| **Hide the Stack header** on `app/admin/returns.tsx` only: `<><Stack.Screen options={{ headerShown: false }} /><AdminReturnsView /></>` (import `Stack` from `expo-router`). `ScreenTitle title="Returns"` unchanged | Same as P2.34/P2.37. The breadcrumb already maps `/admin/returns` → `Returns` (P2.6) | Editing `_layout.tsx` or `ScreenTitle` |
| **Root becomes `ScrollView`** with `contentContainerClassName="w-full max-w-content gap-lg self-center p-lg"` | The current root `View` does not scroll, so a page of return cards is cut off inside the shell. It matches orders list/detail and categories | Keeping `View` (content clipped); a `FlatList` (structural change, not layout) |
| **No `keyboardShouldPersistTaps`** | No other admin `ScrollView` sets it. Adding it here would be a behaviour change on native. Logged as a backlog NOTE | Adding it in a layout-only row |
| **Status chips** use the orders chip pattern: `min-h-touch min-w-touch justify-center rounded-pill border px-md`. Selected: `border-primary bg-primary-subtle`, text `text-body font-semibold text-primary-text`. Unselected: `border-border bg-surface`, text `text-body text-foreground`. Same `key`, `accessibilityRole`, `accessibilityState`, `onPress` body (`setStatus` + `setPage(1)`), label text. The chip row stays directly under the title, outside any panel | One chip style across admin lists. The filter stays local state; making it URL-driven would be new behaviour, not layout | URL-driven status (P2.34 did it for orders, but that row asked for it; this one doesn't) |
| **Results in one `AdminPanel` (no title)**: the list / `No return requests.` / `ErrorState` / `LoadingState` branch, with the same order and conditions | Same as orders/products lists | One panel per card (nested borders, heavier) |
| **Pager moves under the panel**, outside it. It renders only when `query.data && query.data.items.length > 0 && query.data.totalPages > 1`, which is exactly today's condition. Same two `ActionButton`s, labels, `onPress`, `disabled`, and the same `Page X of Y` string. Row class `flex-row items-center gap-md` | Matches orders list (pager under the list) | Leaving it inside the panel (inconsistent). A different condition (would show the pager on an empty page) |
| **`ReturnCard` is className-only (decision freeze).** Root `gap-sm rounded-control border border-border p-md` → `gap-sm border-b border-border py-md` (a row in the panel, like `CategoryRow`). The `open` decision block `gap-sm` → `gap-sm rounded-control bg-surface-raised p-md`. No other line changes: `act`, `decide.mutate({ id, decision, reason: note.trim() })`, `open`, the `disabled={note.trim().length === 0}` guards, `pending`, labels, the alert, every `Text` string | The only state transition on the screen. A diff that touches only `className=` can be checked line by line | A status pill or a re-ordered card (changes text nodes or structure next to the actions) |
| **No copy changes.** Every visible string and accessible name stays byte-identical, including `` `${orderNumber} · ${status}` `` as one `Text` | Tests query exact text; tier-1 is layout only | Splitting number and status into two nodes (breaks `findByText`) |
| **Reuse `AdminPanel` as is**, with no new component and no change to shared components (`ActionButton`, `TextField`, `ErrorState`, `LoadingState`, `ScreenTitle`) | Already the admin surface; shared ones serve the storefront | An `AdminCard` (no second consumer) |
| Restyle rules: RN primitives and token utilities only. No `style=`, no hex, no arbitrary `[...]` values, no new tokens | Tier-1 theme rule | — |
| **No new tests.** `AdminReturnsView.test.tsx` stays byte-identical to `HEAD` and green | Tracker footer for restyle rows; no new behaviour. `AdminPanel` A1–A2 already exist | Class snapshot tests (brittle) |

## Files

All paths under `frontend/apps/store/`.

| File | Change |
|---|---|
| `app/admin/returns.tsx` | Header hidden (decision). |
| `features/admin-orders/components/AdminReturnsView.tsx` | `AdminReturnsView`: `ScrollView` root and container classes; chip classes; results branch inside `<AdminPanel>`; pager moved under the panel with the exact condition above. Imports: add `ScrollView` and `AdminPanel`; keep `View` (still used by the chip row, card and pager), `Pressable` and `Text`. No unused import. `FILTERS`, `useState`s and `useReturnsQuery(status, page)` unchanged. `ReturnCard`: className values only (decision). |

Nothing else changes. No backend, no migration, no new package.

## Decision freeze (checked by Rob in review)

These stay untouched (`git diff` empty): `features/admin-orders/hooks/**` (incl. `useDecideReturnMutation`, `useReturnsQuery`), `features/admin-orders/api.ts`, `types.ts`, every `*.test.ts(x)` in `features/admin-orders/`, `components/AdminPanel.tsx`, `app/admin/_layout.tsx`, `features/admin-shell/**`, `features/auth/**`, and the shared components listed in Decisions.

Inside `ReturnCard` (from `function ReturnCard` to its closing `}`), every changed line is a `className=` value. The `act` body, the `decide.mutate` payload, the `open` condition, both `disabled` guards, `pending`, labels and the error `Text` are identical to `HEAD`. The `AdminReturnsView` filter `onPress` body and the pager `onPress`/`disabled` expressions are identical to `HEAD`.

## Required tests

There are no new tests (decision). The gate is that this existing suite is byte-identical and green, and keeps proving what it proves today:

| Suite | Must still prove |
|---|---|
| `features/admin-orders/components/AdminReturnsView.test.tsx` | Requested-first list with items and reason; empty state; filter → server status, `All` sends none; approve/decline disabled until a note, send it, refetch; decline endpoint; 409 plain message; decided return has no actions and shows its note; paging; load error + `Try again` |
| `components/AdminPanel.test.tsx` (A1–A2) | Unchanged |
| Rest of `features/admin-orders` Jest suites | Unchanged and green |

If a test fails after a layout change, fix the layout, never the test. If a test cannot pass with the planned layout, stop and report it with the failure output. Do not edit the test.

## Build order

1. Set yourself BUSY. On the untouched tree, run `npx jest features/admin-orders components/AdminPanel 2>&1 | tail -n 20` from `frontend/apps/store` and quote the pass count (baseline).
2. `app/admin/returns.tsx`: header hidden.
3. `ReturnCard`: the two className changes only. Run the suite.
4. `AdminReturnsView`: `ScrollView`, chips, `AdminPanel`, pager move. Run the suite; the pass count must equal the baseline.
5. Run `npm run format` from `frontend/apps/store` (repo config only). Then `git -c safe.directory='*' status --short`: only the two slice files (plus the plan and report) may be modified or new. If `format` touched any other file, report it and stop; do not revert with git.
6. Grep your diff for `style=`, `#[0-9a-fA-F]`, `-\[`: none. Run `git diff` on the freeze list: empty. Run `git diff` on `ReturnCard`: only `className=` lines.
7. Run the full `bash scripts/verify.sh 2>&1 | tail -n 30` and quote the `VERIFY` line. Write the report to `slice-plans/slice-P2.36-marco-report.md` (≤ 6 KB): baseline and final Jest counts, files, the freeze checks with their output, and the `VERIFY` line. Set yourself AVAILABLE.
8. Manual check (open for Nathaniel): at 1280 px and 375 px, `/admin/returns` shows one title (no Stack header) and no horizontal scroll, a long page scrolls to the pager, and on native, typing a note then tapping Approve works (first-tap behaviour as before; see Risks).

## Review method (Rob)

Low tier. `git diff --stat`; read both changed files in full. In `ReturnCard`, confirm that every changed line is a `className=` value. Confirm the pager condition equals `HEAD`'s, the freeze list has no diff, and no test file changed. Run the full `verify.sh` in the PASS round. No mutations: the decision logic is untouched, and its guards are already covered by the existing tests (disabled-without-note, decided-has-no-actions, 409).

## Risks

- **Decision path drift:** any non-className edit in `ReturnCard` is BLOCKING and turns this into a High-tier slice that needs a new plan.
- **Pager condition:** moving the pager out of the `items.length === 0 ? … : …` branch could show it on an empty page. The exact condition is in Decisions; the existing paging and empty-state tests cover both sides.
- **Keyboard on native:** inside a `ScrollView`, the first tap after typing the note may only dismiss the keyboard. Report it; don't add `keyboardShouldPersistTaps` here (backlog NOTE: decide for all admin forms at once).
- **`getByText` collisions:** no new text is added. `Requested` (chip) and `ORD-… · Requested` (card) are different exact strings, as today.
- **Scope creep:** no URL-driven filter, no status pill, no copy change, no shared-component restyle.
- One `verify.sh`/Jest run at a time.
