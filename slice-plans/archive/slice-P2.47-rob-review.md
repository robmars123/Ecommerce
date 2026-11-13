# Slice P2.47 + P2.46 — Review thread

## Round 1 — Rob → Marco
Verdict: BLOCKED · verify.sh: not run this round (BLOCKED round; your build run reported `VERIFY PASS`, 206 suites / 1564 tests)

The theme half (P2.47) is clean and matches the plan: one token source, `colorVariables` built from the dark keys, a guarded `merge`, the toggle in both places, and every JS-prop colour on `useThemeColors()`. My own web export shows `background-color:var(--color-background)` and 16 distinct `var(--color-*)` in the CSS. The only hex values left are Tailwind preflight defaults (`#e5e7eb`, `#9ca3af`, `#3b82f6`) and `bg-black/50`, not our tokens. The phone-width half (P2.46) has two gaps. Both are in the part the row exists to deliver.

### 1. A button whose label holds data still overflows at 375 px — BLOCKING   (`features/admin-inventory/components/InventoryRow.tsx:112`, `features/admin-catalog/components/CategoryRow.tsx:62-64`)
**What I saw:** You flagged `InventoryRow:112` and left it as "bounded by SKU length". The server allows a 64-char SKU (`Product.MaxSkuLength`), so `Adjust <sku>` can be ~70 characters of semibold body text, far past 343 px. The same applies to `Edit <name>` / `Delete <name>` in `CategoryRow:63-64`, where a category name can be 100 chars (`Category.MaxNameLength`). Your sweep marked that row "already flex-wrap". But `flex-wrap` only moves items to new lines. It does not shrink an item that is wider than the line by itself. `ActionButton`'s `Pressable` keeps `flex-shrink: 0` on react-native-web and has no max width. In a row it sizes to its max-content, which for these labels is one long line.
**Why it matters:** An admin on a phone opens Inventory with a long SKU (or Categories with a long name). The button runs off the right edge, and the vertical ScrollView clips it. The label is cut and the right part of the button can't be reached. Manual check 1 ("nothing is clipped") fails.
**What I want:** Bound the button, not each caller. Add `max-w-full` to the `Pressable` in `components/ActionButton.tsx` so every data-bearing button wraps its label inside the row width. Nothing changes when the label fits. Keep `InventoryRow:111`'s wrapper as it is, or drop the `flex-row` there; your choice. In `tests/admin-phone-width.test.tsx`, make `/api/v1/admin/categories` return one category with a 100-char name, so PW1 renders `CategoryRow` with long content. Today it returns `{ items: [] }`, and Categories is the one list route with no row at all. List the fix in your sweep section.
**How I'll check:** I'll read the diff to `ActionButton.tsx` and the categories fixture, and confirm PW1 is green for `/admin/categories` with the long name. Jest has no layout engine, so the overflow itself goes on Nathaniel's manual check 1 with a 64-char SKU and a 100-char category name. Add that line to the pending checklist in your report.
**Lesson:** `flex-wrap` bounds a row of short items; one item longer than the line needs its own `max-w-full` or shrink.

### 2. PG1 cannot see a horizontal ScrollView in this repo's own formatting — BLOCKING   (`tests/admin-phone-width-guard.test.ts`, PG1)
**What I saw:** PG1 runs `/<(ScrollView|FlatList)\b[^>]*\bhorizontal\b/` line by line. Prettier puts each prop on its own line once a tag has a few props. The repo already does this in `features/catalog/components/CategoryFilter.tsx:15-16` (`<ScrollView` on one line, `horizontal` on the next). Copy that block into any admin file and PG1 stays green.
**Why it matters:** The guard claims "no horizontal ScrollView or FlatList" in admin. If someone later adds a horizontal chip scroller to an admin screen, formatted the way Prettier formats it, PG1 passes. That brings back exactly the horizontal scroll P2.46 removes. A guard that reports green on the repo's own house style is a test that does not check what its name says.
**What I want:** Make PG1 independent of line layout. Flag any `horizontal` prop token in admin scope: a line matching `/(?:^|\s)horizontal(?:\s*=|\s*\/?>|\s*$)/` is enough, because no admin source uses the word as a prop today. Or match the whole file with a multiline regex and map the index back to a line. Keep the `file:line` message.
**How I'll check:** Put a scratch copy of `CategoryFilter.tsx`'s multi-line `<ScrollView horizontal …>` block into an admin file, then quote PG1 red with the file:line. Restore the file and `cmp` it. I'll re-run that one sanity check myself.
**Lesson:** A source guard has to match the code as the formatter writes it, not as one line in your head.

### 3. Changed existing tests — Confirmed, not weakened (no action)
- `design-tokens.test.ts` T2: the old check `colors` is `themes.dark` had to go with the export (D3). The new T2 keeps the LEGACY key check for dark. It adds the same check for light, the exact `var(--color-<key>)` mapping, and key-set equality with `themes.dark`. It is stricter than before. T1 and T3 are unchanged.
- `AdminShell.test.tsx`, `TopNav.test.tsx`: these are additions only (one import and one appended `describe` each), and the existing blocks are byte-identical. S-T1 covers both widths and asserts the menu stays closed.

### 4. TR2 via react-dom into jsdom — Accepted
This follows the existing pattern in `lib/platform.test.web.tsx:2-5`. `react-dom` is already installed (`package.json` is untouched). It asserts the custom-property value with `style.getPropertyValue('--color-background')`, not a class, which is what the plan asked for.

### 5. NOTE — `AdminReturnsView` flex-wrap
`:44` and `:108` now wrap. Order numbers have a fixed short format, so the Approve/Decline buttons fit once finding 1 lands. No action.

**What was good:** The `merge` guard in `lib/theme.ts` plus TH4 (unknown mode and non-JSON) closes the white-screen path cleanly. Applying `useThemeStyle()` on the `QuickAddPicker` modal root with a one-line reason means the web portal case won't be rediscovered the hard way.

## Round 2 — Rob → Marco
Verdict: PASS · verify.sh: VERIFY PASS (secrets hygiene format build test drift expo-check tsc jest export); jest 206 suites / 1564 tests.

### 1. Long action labels — Confirmed
`ActionButton.tsx` Pressable now has `max-w-full`, so a long label wraps inside the row instead of widening it. The width test mounts a 100-char category, and the inventory mock already uses a long SKU. Jest has no layout engine, so there is no red. The fix is a one-class change with an obvious effect, and manual check 1 (`scrollWidth <= innerWidth`, both themes, 64-char SKU and 100-char category) covers what Jest cannot. That is enough.

### 2. PG1 regex — Confirmed
I appended a `<ScrollView` with `horizontal` on its own line to `AdminSearch.tsx`. PG1 went red with `AdminSearch.tsx:109: horizontal`. I restored the file from a scratch copy and `cmp` showed it identical. The old regex would have missed this case.

No new blocking issues.
