# Slice P2.41 — Review thread

## Round 1 — Rob → Marco
Verdict: PASS · verify.sh: `VERIFY PASS (secrets hygiene format build test drift expo-check tsc jest export)` · build 0 Error(s)/0 Warning(s) · jest 193 suites / 1503 tests

You built the plan's layout-only change on four files and changed nothing else. This is a Low-tier review (plan Review method), so I ran no mutations. I read the full diff of the four files myself rather than relying on your report.

### What I checked

1. **Scope.** `git status --short` under `frontend/apps/store` lists only `app/admin/customers/index.tsx`, `app/admin/customers/[id].tsx`, `AdminCustomerListView.tsx` and `AdminCustomerDetailView.tsx`. The P2.36 archive moves in the tree are not part of this slice, and I did not review them.
2. **Freeze list.** I ran `git diff --stat` on `features/admin-customers/{hooks,api.ts,api.test.ts,types.ts,components/*.test.tsx}`, `components/`, `app/admin/_layout.tsx`, `features/{admin-shell,auth,catalog,orders}` and `lib/`. The output is empty. No test changed, and you added none, as the plan says.
3. **Frozen expressions.** I ran `git diff -w -U0` on both views, which hides the Prettier re-indentation. Every remaining `+`/`-` line is one of these: an `AdminPanel` import or open/close tag, a `className` value, the inner list `View` dropping `gap-xs`, or the removed `Section` root and header `Text`. The D5 line `customer.totalSpent !== null ? … Total spent …` does not appear, so it is byte-identical apart from indentation. The same holds for every template string, the `shown === 0` / `count > shown` conditions, the `Link` `href`s, the 404 branch and `onRetry`, `parseParams` and `change`, the `SearchBar` `onSearch` arrow, and the `Pagination` condition `data && (data.totalPages > 1 || page > 1)` with its props.
4. **Header role.** `Section` now renders `<AdminPanel title={title}>`. `AdminPanel` emits the same `accessibilityRole="header"` `Text` with the same classes, and you removed the old header. Each section title therefore appears once by role, and C2 stays green.
5. **Theme rule.** The changed lines have no `style=`, no hex and no arbitrary `[...]` values. Every class is an existing token utility (`border-border`, `gap-lg`, `max-w-content`, `py-sm`).
6. **Routes.** Both screens hide the Stack header with `<Stack.Screen options={{ headerShown: false }} />`, the same pattern as orders. `useLocalSearchParams` is unchanged.

### 1. Title copy differs from the other restyled screens — NOTE
**What I saw:** These screens keep `Admin · Customers` and `Admin · Customer`, while orders and products use plain titles. The plan kept them on purpose because the slice allows no copy changes.
**What I want:** Align the titles in a later copy row. I have added this to `slice-plans/backlog.md`.

**What was good:** You moved the header into `AdminPanel` instead of nesting a second one, so C2 passed without touching the test. You also kept the unusual pager condition for the past-the-end case (L3) instead of copying the orders version.

Open for Nathaniel: the manual check at 1280 px and 375 px (plan step 8).
