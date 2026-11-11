# Slice P2.6 — Review thread

## Round 1 — Rob → Marco
Verdict: PASS · verify.sh: `VERIFY PASS (secrets hygiene build test drift expo-check tsc jest export)` — jest `Test Suites: 183 passed, 183 total | Tests: 1383 passed, 1383 total`, build `0 Error(s)|0 Warning(s)` (my run, 2026-11-11)

Risk tier Low (display and in-app links only; no endpoint, fetch, token or auth rule), so I read the diff and ran the gate; no mutations. The diff is what the plan asked for: one pure function plus one internal component, S1–S18 untouched, no new packages, tokens or providers, no `style`/hex/rgb.

What I checked in the code, not the report:
- `breadcrumbFor` (`breadcrumb.ts`): labels come only from ready `ADMIN_NAV` items or the 3-entry detail table plus `New product`; no URL segment is ever rendered. `products/new` is matched before the generic detail label. Empty segments (`/admin//`, `''`), depth > 3, non-ready nav (`analytics`, `settings`), `/administrator` and sections without a detail route (`categories/x`, `inventory/x`, `returns/x`) all return `[]`. A single trailing slash is stripped. Matches the contract table row for row.
- B6 reads correctly against `adminRouteDecision` (`null` = allowed): for every path a role can open, every crumb href is also allowed. Support's ancestors (`/admin`, `/admin/orders`, `/admin/customers`) are covered by real inputs.
- `AdminShell.tsx`: the wide top bar always renders (the slot for P2.7/P2.9), the phone bar is two rows with row 1 unchanged, so S2/S7/S17 still hold. The current crumb is plain `Text`; ancestors are `Link asChild` + `Pressable accessibilityRole="link"` with `min-h-touch`, same as the sidebar. The `›` separator is hidden from accessibility.
- Tests: S19–S25 query inside `within(getByLabelText('Breadcrumb'))`, so they don't collide with sidebar `Dashboard`/`Orders`. S20/S23 press real links and assert `toHavePathname`; S19 asserts the id is rendered nowhere. No existing test changed.

### 1. F7b timeout on the first full run — NOTE   (`frontend/apps/store/tests/app/admin.test.tsx:38`)
**What I saw:** the failing test is the first one in that file, and it calls `renderRouter('./app', …)`, which loads the whole route tree cold. Your slice only adds a small pure import to `AdminShell`. It passed alone, in your second full run, and in my full run (1383/1383).
**Why it matters:** it isn't a logic failure, but a gate that sometimes goes red under parallel load trains people to rerun until green, and that hides real failures.
**What I want:** not in this slice. I've put it in `backlog.md`: if it happens again, give that `describe` a longer per-test timeout (or warm the router in a `beforeAll`). Never retry blindly.
**Lesson:** a timeout that clears on rerun is a flake only if the slice didn't touch that code path. Here it didn't, so it's a backlog item, not a blocker.

### 2. Current crumb may not truncate on very narrow widths — NOTE   (`AdminShell.tsx` `Breadcrumb`, last `Text`)
**What I saw:** `numberOfLines={1}` sits on a `Text` inside a `flex-row` wrapper that has no `shrink`/`min-w-0`. In Yoga that text won't shrink below its intrinsic width, so the ellipsis may never kick in. `flex-wrap` on the container moves it to its own line instead. S22 only checks the prop.
**Why it matters:** with today's fixed labels (≤ 16 chars) nothing overflows, even at 320 px, so this is cosmetic.
**What I want:** confirm it during the 320 px manual check that's already in the backlog. Add `shrink` only if it actually overflows.
**Lesson:** a Jest prop assertion proves intent, not layout. Geometry needs eyes on a screen.

**What was good:** the early-return chain in `breadcrumbFor` makes "unknown → `[]`" the default rather than a special case, and B7 ties section labels to `ADMIN_NAV`, so a rename can't drift. The red output you quoted first (12 failed, 48 passed) fits a genuine test-first run.
