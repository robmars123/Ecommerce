# Slice P2-style-orders — Marco report (LOW risk, no mutations)

Concerns: None. Plan gap, minor: Amendment 1 puts `StatusPill` in place of `OrderStatusBadge`; I deleted `OrderStatusBadge.tsx` (no remaining importer). `OrderListView.test.tsx` and `design-tokens-style.test.ts` changes are the earlier build's, listed below.
Bugs found: None
Bugs fixed: None
Bugs open: None

State on takeover: no verify.sh, jest or expo export process from the stopped build was running (only my shells, VS Code, and a `find` for `@expo/cli` that is not mine; untouched). Jest on `features/orders components lib`: 144 suites / 1133 tests green. D1-D3, O1-O5 and D5 were present and green.

## Red first (new pure functions)
- `features/orders/layout.test.ts` (L1): `Cannot find module './layout' from 'features/orders/layout.test.ts'`.
- `status-labels.test.ts` (O6): `TypeError: (0 , _statusLabels.paymentTone) is not a function` for all 5 cases.
Then `layout.ts` and `paymentTone` implemented; both green.

## Built
- `features/orders/layout.ts` `orderDetailLayout(w)` (A1, tokens 981/561), used with `useWindowDimensions`.
- `components/StatusPill.tsx` (A2: 7px dot, `text-ui-sm font-bold`, tone classes); `OrderStatusBadge.tsx` deleted. `paymentTone()` in `status-labels.ts`.
- A3: `font-mono` class; `#root .font-mono, #root .font-mono *` rule in `public/index.html` = `webFontFamily.mono`; F6 in `lib/web-fonts.test.ts`. Native: NativeWind preset maps `font-mono` to `mono` (Android) / `Courier New` (iOS) (`nativewind/dist/tailwind/native.js:172`). Web: the rule, checked by F6 (the live DOM was not inspected, see Not done).
- A4: `Panel` `subtitle` (A4 test in `Panel.test.tsx`).
- A5: SKU and unit price shown.
- `OrderDetailView`: breadcrumb, header panel (eyebrow, h1 header View "Order {number}", meta, All orders action, order + payment pills), two-column grid at >= 981 (right column 360px), compact below 561, items row (name, mono SKU, x qty chip, "each", total), Totals ("Shipping · {method}", tabular-nums, Total border-top), Shipping (method row, Deliver to, Fulfilment only with shipments; Tracking panel removed), Refunds last. `ScreenTitle` only while loading/error.
- `OrderTimeline`: 18px rail, muted dot, Cancelled red dot with hidden "×", 2px line except last, status + date row, mono danger reason tag.
- `OrderListView`: mono number, `ui-sm` date, `StatusPill`, `panel-title` total.
- `OrderDetailView.refunds.test.tsx` has the same sentinel change.

## Tests changed (before -> after; every value assertion kept)
- `OrderDetailView.test.tsx`, 6 sentinels: `findByText('ORD-2026-000001')` -> `findByRole('header', { name: 'Order ORD-2026-000001' })` (number now shown twice).
- `OrderDetailView.refunds.test.tsx`, 3 sentinels: same change.
- `'Black tee, M × 2'` text -> `getByText('Black tee, M')` + `getByText('× 2')` (name and qty chip are separate Texts).
- `getByLabelText('Shipping $5.00')` -> `'Shipping · Standard shipping $5.00'`.
- Earlier build, as the plan table: `$40.00` text -> item label query; Subtotal/Shipping/Total `getByText` -> `getByLabelText` same strings; `OrderListView.test.tsx` link names -> `/^ORD-…\b/`, O2 exact string `ORD-2026-000001 Mar 4, 2026 Paid $57.50`; `AdminPanel.test.tsx` -> `Panel.test.tsx`; `design-tokens-style.test.ts` +2 keys.
- New: L1, O6, O7 (x2, press link -> pathname `/orders`), O8, O9, O10, A4, F6 (plus O1-O5 from before).
- No other test changed. P2.46 (phone width) and P2.47 (light/dark) pass unchanged.

## Not done
- Web check at 375/800/1280 and `scrollWidth` (plan §5.7) was not run: no browser session in this run. Rob or Nathaniel should check `/orders/<id>`, hover URL on the row link, light and dark.
- `prettier --write` was applied to 3 files after verify's format step failed.

Changed: `frontend/apps/store/` `features/orders/{layout.ts, layout.test.ts, status-labels.ts(+test), components/{StatusPill.tsx, OrderDetailView.tsx(+test, refunds.test), OrderTimeline.tsx, OrderListView.tsx}}`, `components/Panel.tsx(+test)`, `public/index.html`, `lib/web-fonts.test.ts`; earlier build: tokens, 11 admin importers, ReturnRequestForm, TrackingLink.

verify.sh (2 full runs, same result): secrets, hygiene, format, build (0 warnings), test, drift, expo-check, tsc, export PASS; jest FAIL, `VERIFY FAIL: jest`: 1 failed / 1815 passed, `tests/routes.test.tsx` first case `index.tsx renders at /products` exceeds the 5 s timeout (cold load of the app tree on 24 parallel workers; 23-24 s suite). The same file passes alone (48/48), and the full suite passes with `npm test -- --ci --maxWorkers=3`: 232 suites, 1816 tests. Not caused by an assertion; not weakened or changed. Rob/Nathaniel to decide (backlog: raise that test's timeout or cap workers).

Quality-bar §2: tests for each change checked; no money/stock/auth touched; no API or package change; web visual check unchecked (reason above).

## Round 1

Concerns: None
Bugs found: None
Bugs fixed: routes.test.tsx cold-load timeout under 24 workers; test now passes in the full run.
Bugs open: None
Tests changed: `frontend/apps/store/tests/routes.test.tsx` — before: default 5 s timeout; after: `jest.setTimeout(15_000);` plus 2-line reason comment after `jest.mock('expo-secure-store');`. No assertion or jest config changed. Reason: cold load of the whole `app/` tree is ~1.2-1.4 s alone, over 5 s under parallel load; 15 s still fails a real hang.
Changed: frontend/apps/store/tests/routes.test.tsx
verify.sh (full): VERIFY PASS (secrets hygiene format build test drift expo-check tsc jest export); jest 232 suites / 1816 tests passed.
Quality-bar §2: unchanged from round 0; no production code touched.
