# Slice P2-style-orders — Review thread

## Round 1 — Rob → Marco
Verdict: BLOCKED · verify.sh: not re-run (known red, `VERIFY FAIL: jest`, 1 failed / 1815 passed, per your report). My runs: `npx jest tests/routes.test.tsx` → `Tests: 48 passed, 48 total`; touched files (`features/orders`, `Panel.test`, `web-fonts.test`, `design-tokens-style.test`) → `Test Suites: 26 passed` · `Tests: 162 passed`.

LOW risk, so I read the diff only and ran no mutations. The restyle follows Amendment 1, and every value assertion survives. Only one thing stands in the way of PASS: the jest gate is red, and the cause is the test harness, not this slice's code.

### 1. Cold-load route test times out under 24 workers — BLOCKING   (`frontend/apps/store/tests/routes.test.tsx:17`)
**What I saw:** I ran the file alone with `--json`. The first built route, `index.tsx renders at /products`, takes **1213 ms (ios) / 1364 ms (android)**. Every later route takes 62–132 ms, `orders/[id].tsx` included (82 / 86 ms). So the first test's cost is the one-time require and transform of the whole `app/` tree, not rendering. Under 24 parallel workers that one-time cost grows past jest's 5 s default.
**Did this slice make it worse:** not measurably. Your import delta is `Panel` (it replaces `AdminPanel`, so no new module), `StatusPill`, and `features/orders/layout.ts`. These are three small modules, they bring in no new package, and `layout.ts` only imports `@/lib/design-tokens`, which is already loaded. The same timeout appeared during the earlier P2-style build, before any of these files existed. The orders route itself stays at about 85 ms. This is CPU contention on a cold module graph, not new slowness.
**Why it matters:** while the gate stays red, no slice can PASS, and the failure comes and goes with machine load. That trains everyone to ignore jest failures.
**What I want:** the smallest fix is a per-file timeout. After `jest.mock('expo-secure-store');` add:
```ts
// Each file's first renderRouter requires and transforms the whole app/ tree once (~1.3 s alone, >5 s under 24 workers).
// A hang still fails at 15 s; per-route render time is ~100 ms.
jest.setTimeout(15_000);
```
Change no assertion and touch no jest config. Don't use `maxWorkers`: capping workers slows every file on every machine to fix one cold start. Keep it as the fallback if a second `renderRouter` file starts timing out (backlog).
**Is this hiding a real hang (§1)?** No. A hang is a promise that never settles, and it still fails at 15 s. The `findAllByRole` waits keep their own 1 s default. Against the measured 1.3 s cold time, 15 s is about 10× headroom, and that headroom only covers module loading.
**How I'll check:** the full `bash scripts/verify.sh 2>&1 | tail -n 30` shows `jest` PASS with 1816 tests, and `git diff` on `routes.test.tsx` shows only those 3 lines.
**Lesson:** a timeout may cover a cold start you have measured. It must never cover an unexplained wait. Measure before you raise it, and state why in the code.

### 2. Web check not run — NOTE
You report that §5.7 is not done: 375/800/1280, `document.documentElement.scrollWidth` at 375, light and dark, the hover URL on the list row link, and `font-mono` reaching the DOM. F6 proves the rule is in `public/index.html`, but not that NativeWind emits a `.font-mono` class on the element. The rule's specificity is right: `#root .font-mono *` (1,1,0) beats `#root *` (1,0,0). This is recorded in backlog for Nathaniel's manual check. It does not block a LOW-risk layout slice.

### 3. Report path and test title — NOTE
`StatusPill.tsx` lives in `features/orders/components/`, not `components/` as your report says. That is the right place, since it has only orders consumers this slice. The first O7 test is titled "both links lead back" but presses only "Orders"; the second test covers "All orders". Rename it when the file is next touched.

### Checklist (all confirmed in the diff)
- **Changed tests, values kept:** `OrderDetailView.test.tsx`: 6 sentinels → `findByRole('header', { name: 'Order ORD-2026-000001' })`, `'Black tee, M × 2'` → `'Black tee, M'` + `'× 2'` + O4 label `'Black tee, M × 2, $40.00'`, `Shipping $5.00` → `'Shipping · Standard shipping $5.00'`, and `Total $45.00` kept. `OrderDetailView.refunds.test.tsx`: 3 sentinels, same change. `OrderListView.test.tsx`: names → `/^ORD-…\b/`, O2 exact `ORD-2026-000001 Mar 4, 2026 Paid $57.50`, and Track excluded. Each change is listed in the plan and justified.
- **`OrderStatusBadge`:** deleted. `grep` finds 0 hits for `OrderStatusBadge` and `AdminPanel` in `.ts/.tsx`.
- **D1 rename:** the admin-catalog, -customers, -inventory and -orders hunks touch only `Panel` lines. The DashboardView/AdminShell churn is from the earlier P2-style slice.
- **Mono rule:** it comes after the sans rule and equals `webFontFamily.mono` (`"JetBrains Mono", monospace`), as F6 asserts. This closes the P2-style backlog item about mono having no consumer.
- **No faked data:** SKU, unit price, item count (Σ quantity), method name and shipments all come from `OrderDetail`. Payment pill renders only when `paymentStatus` is non-null.
- **No [feature] built:** there is no copy button, reorder, cancellation alert, Payment card, thumbnail, description or totals note. Tracker rows P2.48–P2.55 were added as Todo.
- **Breakpoints:** `layout.ts` uses the `sidebarBreakpoint` 981 and `narrowBreakpoint` 561 tokens. L1 covers 560/561/980/981.
- **P2.46/P2.47:** both rows unchanged in the tracker diff and still Done.

**What was good:** you measured the alone-vs-parallel behaviour and asked for a decision instead of quietly editing the test or config. The `Record<OrderStatus, StatusTone>` table means a new status becomes a compile error instead of a grey pill.

## Round 1 — Rob → Marco
Verdict: PASS · verify.sh: `VERIFY PASS (secrets hygiene format build test drift expo-check tsc jest export)` — jest 232 suites / 1816 tests passed.

### 1. Cold-load timeout — Confirmed (`frontend/apps/store/tests/routes.test.tsx:18`)
I read the diff of that file: it adds only the 2-line comment and `jest.setTimeout(15_000);` after `jest.mock('expo-secure-store');`. No assertion and no jest config changed, and the file has no other hunks.
The fix does not hide a hang. The measured cold load is about 1.2-1.4 s alone, so 15 s is roughly 10x that. A real hang still exceeds it and fails. The timeout is scoped to this one file.

No new findings. Nothing added to the backlog.
