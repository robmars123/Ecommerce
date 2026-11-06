# Slice 1a follow-up (chip and search layout) — Review thread

## Round 1 — Rob → Marco
Verdict: BLOCKED · verify.sh: `VERIFY PASS (secrets hygiene build test drift expo-check tsc jest export)` — build 0 Warning(s) · jest 45 suites, 280 passed

The chip fix is right and small: `flexGrow: 0, flexShrink: 0` on the outer `style` of the horizontal ScrollView is the correct fix for a ScrollView that gets squashed in a column parent, and `minWidth: 0` is the real fix for the web input's min-content width. One line has no consumer and no test, so this round is blocked on it.

Scope: `CategoryFilter.tsx`, `SortPicker.tsx`, `SearchBar.tsx` and their three tests. `HomeView.tsx`, `ProductDetailView.tsx` and the untracked `ProductDetailContent.tsx` are also modified in this folder, but they aren't in your report or in this review's brief. Nathaniel should know they are in the working tree.

### 1. `maxWidth: '100%'` on the search row is unproven code — BLOCKING   (`SearchBar.tsx:71`)
**What I saw:** You added `maxWidth: '100%'` to `styles.row`. Your report says it isn't tested, and no test or plan item explains which layout needs it.
**Why it matters:** Both parents (`HomeView`, `ProductListView`) are column Views with the default `alignItems: 'stretch'`, so the row is already exactly as wide as its parent. In both screens the cap does nothing. The overflow came from the input's min-content width, and `minWidth: 0` fixes that. The line is speculative code with no test, and the minimal-code bar blocks that. It also hides the real cause: the next person sees two "fixes" and can't tell which one carries the load. If a future parent sets `alignItems: 'center'`, this line would silently change that layout with no test noticing.
**What I want:** Delete `maxWidth: '100%'` from `styles.row`. If you can name a real parent where the row still overflows with `minWidth: 0` alone, keep the line, add a test that pins it (`StyleSheet.flatten` on the row, found with `getByLabelText('Search products').parent`), and give that case in your reply.
**How I'll check:** I'll read the diff for `SearchBar.tsx`, then run `npx jest features/catalog/components/SearchBar` and the full `verify.sh`.
**Lesson:** Ship the one change that fixes the root cause. A second defensive change needs its own failing case, or it stays out.

### 2. Red evidence for the CategoryFilter and SearchBar tests — NOTE (accepted)
**What I saw:** You showed the test failing before the fix only for SortPicker.
**What I checked:** I checked the other two from `git show HEAD`. Before your change, neither ScrollView had a `style` prop, so `StyleSheet.flatten(undefined)` returns `undefined` and `toMatchObject` throws, which is a red test. The HEAD `SearchBar` input style is `{ flex: 1, minHeight: 44, … }` with no `minWidth`, so `toMatchObject({ flex: 1, minWidth: 0 })` fails. Both tests would have been red. Next time, run each test against the unfixed code and say so; reading the code is a fallback, not the proof.
**Limit:** these are style-contract tests. Jest can't measure layout, so the real proof is the manual check at phone width on web :8081 (the plan's verification step). Nathaniel still owes that check.

### 3. Both ScrollViews share `testID="chip-scroller"` — NOTE   (`CategoryFilter.tsx:15`, `SortPicker.tsx:27`)
**What I saw:** `ProductListView` renders `CategoryFilter` and `SortPicker` together, so that screen has two elements with the same testID.
**Why it matters:** A future screen-level test that calls `getByTestId('chip-scroller')` on the list screen throws "found multiple elements". Today's per-component tests pass because each renders only one scroller.
**What I want:** Use distinct IDs (`category-scroller`, `sort-scroller`) while you're in these files. A testID is fine here because a ScrollView has no accessible role to query by.

**What was good:** You put the fix on the ScrollView's own `style` and left `contentContainerStyle` alone, so the chips still scroll horizontally. You also fixed it once in the two shared components rather than in each of the screens that use them.

## Round 1 — Marco → Rob

1. **Fixed** — Removed `maxWidth: '100%'` from the row style in SearchBar.tsx (line ~71). I can't name a parent that overflows without it; the input's `flex: 1, minWidth: 0` (already tested) is the actual fix. No test change needed since nothing untested remains.
2. **Fixed** — Renamed testIDs to `category-scroller` (CategoryFilter.tsx, its test) and `sort-scroller` (SortPicker.tsx, its test). Tests pass.

## Round 2 — Rob → Marco
Verdict: PASS · verify.sh: `VERIFY PASS (secrets hygiene build test drift expo-check tsc jest export)` — build 0 Warning(s) · jest 45 suites, 280 passed · `npx jest features/catalog/components`: 12 suites, 60 passed

Both items are closed, and the diff now holds only the root-cause fixes.

### 1. `maxWidth: '100%'` on the search row — Confirmed
I read `SearchBar.tsx:71`. The row is back to `{ flexDirection: 'row', alignItems: 'center', gap: 8 }`, and `grep maxWidth` finds nothing in `SearchBar.tsx` or its test. The input keeps `flex: 1, minWidth: 0`, and the existing test pins that style. Every line that's left now has a test behind it.

### 3. Shared testID — Confirmed
`CategoryFilter.tsx:15` now uses `category-scroller` and `SortPicker.tsx:27` uses `sort-scroller`. Each test queries its own ID. No `chip-scroller` is left anywhere in the folder, so `ProductListView` no longer has two elements with the same ID.

**Still owed (not blocking the code review):** Nathaniel's manual check at phone width on web :8081. The chips should show at full height and the search box should stay inside the screen. Jest checks the styles, not the measured layout.

**What was good:** You removed the line rather than inventing a reason to keep it. That leaves the root cause easy to see for the next reader.
