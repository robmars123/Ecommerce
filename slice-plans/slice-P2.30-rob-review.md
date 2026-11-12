# Slice P2.30 (+ P2.24, P2.32) — Review thread

## Round 1 — Rob → Marco
Verdict: PASS · verify.sh: `VERIFY PASS (secrets hygiene format build test drift expo-check tsc jest export)` — build 0 warnings; jest 208 suites / 1602 passed

Low tier, so I read the diff and checked your evidence. I ran no mutations. The diff is exactly the plan: one production file, two test files with additions only, and `backend/src` untouched. I found no blocking issues and no bugs.

### What I checked
- **Scope.** `git diff --stat -- backend/src` is empty. `AdminInventoryAdjustTests.cs` is +13/-0 (S1 only), and `DashboardView.test.tsx` is +86/-0 (Q1, Q2 as an `it.each`, Q3, B1–B4). Every frozen test is byte-identical, including `offers the quick actions`, the needs-attention `alert` count, and `Blue Shirt · 9 sold`.
- **Red evidence.** You quoted Q1 and Q2[Support] red (`toBeNull()` received `Add product`; `toEqual` `+4`), then B1 red (`Unable to find … Stock is running low. Review inventory`). You quoted the B3 self-check red and its restore. M1 red is `Expected: Forbidden Actual: OK`, then `cmp` is clean, S1 re-runs green, and `backend/src` is empty. That is the full chain the plan asked for, and you quoted the filtered green this time (the P2.21 NOTE is closed).
- **Role matrix (D6).** `QuickActions` filters `QUICK_ACTIONS` through `adminRouteDecision(role, a.href) === null`. Admin gets all five and Support gets Orders only. Grepping the file for `'Admin'`/`'Support'` finds nothing, so there is no second role table. `role` is read once and used only by the banner and `QuickActions`. `data.kpis ? …` is unchanged (D7).
- **Banner (D2–D4).** The condition sits inside the `data ?` block, so nothing renders while loading or on error (B4). It is `data.lowStock.length > 0 && adminRouteDecision(role, '/admin/inventory') === null`. The copy is `Stock is running low` / `Review inventory` with no count, the accessible name matches D3, and it is `accessibilityRole="link"`, not `alert`. B2 covers the empty case and B3 the role clause.
- **Restyle (D8, D9).** Top products and Quick actions now use `AdminPanel`, and the strings are unchanged. The classes use theme tokens only (`border-warning`, `text-warning`, `bg-surface`, `rounded-card`, `rounded-control`, `min-h-touch`). The file has no `style=`, hex value or `[...]`. `flex-wrap` is on the banner and the actions row, and the PG1–PG3 and PW1 checks pass inside the jest step. `Section` is still used by Orders by status and Needs attention.

### 1. Support's low-stock rows still link to `/admin/inventory` — NOTE   (`DashboardView.tsx`, Low stock panel)
**What I saw:** The banner is now hidden from Support, but the P2.31 low-stock rows below it still link to `/admin/inventory`, so the gate sends Support back to `/admin`.
**Why it matters:** Support taps a row and gets bounced back. The server still refuses the data (S1 proves it), so this is a UX problem, not a security one.
**What I want:** In a later row, render those rows as plain text for roles that `adminRouteDecision` refuses. → backlog.
**Lesson:** When you hide one entry point by role, hide the others that lead to the same route.

**What was good:** You reused `adminRouteDecision` for both the banner and the actions, so the dashboard cannot drift from the route gate. B4 stubs a failing fetch instead of relying on timing, so "no banner before data" is a real assertion.
