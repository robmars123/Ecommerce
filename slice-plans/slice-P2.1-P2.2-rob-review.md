# Slice P2.1–P2.2 — Review thread

## Round 1 — Rob → Marco
Verdict: BLOCKED · verify.sh: not re-run (BLOCKED round, CLAUDE.md §5); your report: `VERIFY PASS (secrets hygiene build test drift expo-check tsc jest export)`, 179 suites / 1271 tests.

Low-risk slice, read as a diff, no mutations. Scope is tight and matches the plan: the token split is exactly D1–D4, the shell sits inside `AdminGate`, the Stack, its `screenOptions` and its comment are verbatim, and `git diff` confirms `package.json`, `tailwind.config.js`, `app/_layout.tsx`, `features/auth/` and every existing test are untouched. One layout bug blocks it.

### 1. The phone sidebar ignores `w-sidebar` and takes half the screen — BLOCKING   (`features/admin-shell/components/AdminShell.tsx:25`)
**What I saw:** `Sidebar` carries `w-sidebar max-w-full flex-1`. On the wide layout that is harmless (its wrapper is a column, so `flex-1` only stretches height). But in the phone overlay (line 85) you put `Sidebar` in a `flex-row` next to the scrim `Pressable`, which is also `flex-1`. `flex-1` is `flex: 1 1 0%` on web and `flex: 1` (basis 0) in Yoga, and a non-auto flex-basis wins over `width` on the main axis. So both children start at 0 and grow equally.
**Why it matters:** at 375px the panel is ~187px, not 240px; at 767px it is ~383px. At 187px minus the `p-lg` padding there are ~155px for the "S" mark, "Store Admin" and the 44px "Close" button, so the header row wraps or clips — exactly the phone case P2.2 exists for. D7 specifies a `w-sidebar` panel, and the `width.sidebar` token and T3 (< 320) only mean something if the panel actually uses it. Jest can't see this, and the step-4 manual check that would have caught it wasn't done.
**What I want:** remove `flex-1` from the `Sidebar` root. Height still works: in the overlay the row's default `align-items: stretch` fills the panel vertically, and on wide the `bg-surface border-r` wrapper already stretches in its row parent. Keep your deviation 4 layout (panel left, scrim `flex-1` beside it) — it's fine once the panel has a fixed width. No test changes needed.
**How I'll check:** read the one-line diff (no `flex-1` on the `Sidebar` root, `w-sidebar max-w-full` kept); S1–S6 and R1–R2 still green under `verify.sh changed` + jest; Nathaniel's 375px browser check shows a 240px panel with the scrim to its right.
**Lesson:** in a flex row, `flex-1` sets the size on the main axis; a width token on the same element is dead the moment it gets `flex-1`.

### 2. Manual 1280/375 browser check outstanding — NOTE
You couldn't run a browser; that's honest and fine. It stays open for Nathaniel before commit (after fix 1): sidebar at 1280, toggle + 16px gutter at 375, no horizontal scroll, Stack header/back readable on all seven admin screens. Logged in the backlog.

### 3. Jest default window width not reported — NOTE
Risk 1 asked for it. All 179 existing suites passing unchanged is the evidence that matters, so I don't need it now; the backlog notes it for P2.3, which adds more nav text that could collide.

### Your deviations
1. Mock path as planned — **Accepted.**
2. R1/R2 pin width 1280 — **Accepted**; "wide" must not depend on jest-expo's default.
3. Async RNTL API and `toBeExpanded()/toBeCollapsed()` — **Accepted**; same role/state assertion, no class strings.
4. Scrim beside the panel instead of under it — **Accepted** as layout, but it is what exposed finding 1.
5. Manual check not done — **Accepted**, see NOTE 2.
6. 8-digit hex compiles — **Confirmed**: export/build passed per your VERIFY line.

**What was good:** `colors: dark` as the same object plus T2's `toBe` makes "one token source" a test, not a promise. Not mounting the sidebar while closed keeps it out of the tab order with no extra code, and R2 proves the guard still runs before the shell.

## Round 1 — Rob → Marco
Verdict: PASS · verify.sh: VERIFY PASS (secrets hygiene build test drift expo-check tsc jest export); build 0 warnings; jest 179 suites / 1273 tests.

### 1. `flex-1` on Sidebar root — Confirmed fixed
`AdminShell.tsx:25` is now `w-sidebar max-w-full gap-xl bg-surface p-lg`, with no `flex-1`. The only remaining `flex-1` is on the scrim Pressable (line 87), which is correct: it takes the space beside the fixed-width panel. S7 in `AdminShell.test.tsx:81-87` asserts the panel has `w-sidebar` and that the class list does not contain `flex-1` (exact token match, so `flex-1` inside another class cannot fool it). Your quoted red matches the old code.

**What was good:** the S7 test uses a token-level check, and the fix is a one-class change with nothing extra added.
