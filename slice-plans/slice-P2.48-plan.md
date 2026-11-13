# Slice P2.48 — Copy order number button (customer order detail)

Mode: Autonomous · Risk tier: **Low** (CLAUDE.md §5 — UI-only, no money, stock, payment, auth or state transition) · Mutations: **none**.
Scope: tracker row P2.48 only (`docs/admin-dashboard-feature-tracker.md`, group C). Storefront `/orders/[id]`, not admin. P2.49–P2.55 are out of scope.

## 1. Read

- `docs/design-refs/northline-order-detail.md` lines 11–18 (order header) and the `.order-no` CSS line (grep `order-no`).
- `docs/architecture/05-frontend.md` §5.2, §5.5 (styling, tests by role/label).
- `docs/architecture/11-testing.md` §11.3 (what must be tested; jest projects ios/android/web).
- `docs/architecture/13.2-decision-log.md` — grep `expo-crypto` (precedent) and `Clipboard` (row added by this plan).
- Code: `frontend/apps/store/features/orders/components/OrderDetailView.tsx` (`Header`), `OrderDetailView.test.tsx`, `tests/render-with-query.tsx`, `tests/jest-setup.js`, `package.json`.

No backend, no API, no migration. §12.3 has no row for group C; the tracker row is the acceptance criterion.

## 2. Decisions

| Decision | Reason | Alternative rejected |
|---|---|---|
| **New package `expo-clipboard`**, installed with `npx expo install expo-clipboard`, then re-pinned **exact** in `package.json` (like every other dependency). Used as `setStringAsync(text)` | One API on web, Android and iOS; Expo SDK module, works in Expo Go and the web export; `expo install --check` keeps it SDK-aligned (decision-log row "Expo SDK patch alignment"). Replaces nothing — there is no clipboard code today (grep `clipboard` in `features lib components`: no hits). Same reasoning as the `expo-crypto` row | Web `navigator.clipboard` + native fallback: RN core removed `Clipboard`, so native would need `Share.share` (a share sheet, not a copy) or hide the button — two code paths and a different UX on two of three platforms. `@react-native-clipboard/clipboard`: not an Expo SDK module, needs a dev build |
| The button lives inside `Header` in `OrderDetailView.tsx`, right after the order-number chip, as a small local component `CopyOrderNumber` in the **same file** | One consumer; the header already owns the chip. No new shared component (no second consumer) | `components/CopyButton.tsx` — speculative reuse |
| The chip stays outside the header's `accessible` group: the header `View` keeps `accessibilityLabel="Order {n}"`, and the button is a **sibling** pressable placed after that `View` inside the same flex row (wrap header + button in a new outer `flex-row flex-wrap items-center gap-md` View) | A pressable nested inside an `accessible` View is merged away for screen readers and can't be found by role. Existing test `findByRole('header', { name: 'Order ORD-…' })` must stay unchanged | Putting the button inside the header group |
| Label "Copy" → "Copied" on success, "Copy failed" on failure (`setStringAsync` resolves `false` or rejects); resets to "Copy" after **2000 ms**, timer cleared on unmount and on a new press | Visible and announced feedback; no toast system exists, and none is added | A toast/snackbar — new infra with no other consumer |
| `accessibilityRole="button"`, `accessibilityLabel` = `Copy order number` / `Order number copied` / `Couldn't copy order number` matching the state; `min-h-touch` (44 pt touch target) | Tests find it by role + name (§5.5); touch-target invariant | Icon-only button without label |
| Copies `order.orderNumber` exactly (e.g. `ORD-2026-000001`) — no prefix, no URL | The row says "order number" | Copying the page URL |
| Styling: tokens only — `rounded-input border border-border bg-surface-raised px-md`, text `text-ui font-semibold text-foreground`; success text may use the existing accent/success token, failure the existing danger token (whatever `StatusPill` tones already use — no new tokens) | Design ref: tokens win, no second palette | New colour values |
| Jest: `jest.mock('expo-clipboard', () => ({ setStringAsync: jest.fn() }))` added to `tests/jest-setup.js` (global, like `expo-crypto`) with a one-line comment; tests set the resolved value per case | The native module has no JS implementation under jest-expo ios/android; every screen that imports `OrderDetailView` would otherwise load it | Per-file mocks only — the other order-detail test files (`OrderDetailView.refunds.test.tsx`, route tests) would break |

Record: Rob appends one row to `docs/architecture/13.2-decision-log.md` ("Clipboard on the client (P2.48 package)") in this planning step — done.

## 3. Files (all Marco)

| File | Change |
|---|---|
| `frontend/apps/store/package.json` (+ `package-lock.json` via the install) | add `expo-clipboard` at the SDK 58 version `expo install` picks, pinned exact (no `~`/`^`) |
| `frontend/apps/store/tests/jest-setup.js` | global mock of `expo-clipboard` (`setStringAsync: jest.fn(async () => true)`) |
| `frontend/apps/store/features/orders/components/OrderDetailView.tsx` | `CopyOrderNumber({ orderNumber })` + wire into `Header` as in §2 |
| `frontend/apps/store/features/orders/components/OrderDetailView.copy.test.tsx` | **new** — the tests in §5 (separate file keeps the existing 199-line test untouched) |
| `docs/admin-dashboard-feature-tracker.md` | P2.48 `Todo` → `Done` after verify passes |

Module contract (internal to the file): `function CopyOrderNumber({ orderNumber }: { orderNumber: string })`. State `'idle' | 'copied' | 'failed'`, one `useRef` timer, `useEffect` cleanup clears it. Handler: `try { const ok = await setStringAsync(orderNumber); setState(ok ? 'copied' : 'failed') } catch { setState('failed') }`, then schedule reset. No logging of the order number needed; if you log the failure, use `lib/logger` (no `console.*`).

No `app/` change (the route stays thin). No `lib/platform` split (the package covers all platforms).

## 4. Build order

1. `cd frontend/apps/store && npx expo install expo-clipboard`; re-pin exact; run `npx expo install --check` (must say up to date). If the install fails (offline, no SDK-58 version), **stop and report BLOCKED** — do not hand-roll a `navigator.clipboard` fallback.
2. Add the global mock to `tests/jest-setup.js`.
3. Write `OrderDetailView.copy.test.tsx` first; run it and quote the red output (button not found).
4. Implement `CopyOrderNumber` and wire it into `Header`. Green.
5. Run the existing `OrderDetailView.test.tsx` and `OrderDetailView.refunds.test.tsx` unchanged — they must pass without edits.
6. Full `bash scripts/verify.sh 2>&1 | tail -n 30` (expo-check runs because `package.json` changed). Mark the tracker row Done. Report to `slice-plans/slice-P2.48-marco-report.md`.

## 5. Required tests (`OrderDetailView.copy.test.tsx`)

Mount with `renderRouterWithQuery({ 'orders/[id]': OrderDetailScreen }, { initialUrl: '/orders/o-1' })`, `signInAs('Customer')`, fetch spied to `makeOrderDetail()` — same setup as `OrderDetailView.test.tsx`. Never `renderRouter('./app')`. Import the mock via `import * as Clipboard from 'expo-clipboard'` and `jest.mocked(Clipboard.setStringAsync)`.

1. **(test-first)** Shows a button `getByRole('button', { name: 'Copy order number' })` in the header, and the header `findByRole('header', { name: 'Order ORD-2026-000001' })` still resolves (the button didn't break the group).
2. **(test-first)** Pressing it calls `setStringAsync` exactly once with `'ORD-2026-000001'`, then `findByRole('button', { name: 'Order number copied' })` and the visible text "Copied".
3. `setStringAsync` resolves `false` → button becomes `Couldn't copy order number` / "Copy failed".
4. `setStringAsync` rejects → same failed state; no unhandled rejection (test passes with no console error).
5. Reset: with `jest.useFakeTimers()` after the screen has loaded (or advance via `act(() => jest.advanceTimersByTime(2000))`), the label returns to `Copy order number`. Restore real timers in `afterEach`.
6. Not rendered in loading/error states: when fetch returns 404 problem, `queryByRole('button', { name: 'Copy order number' })` is null.

Existing tests: zero edits expected. Any change to an existing test must be listed in the report with its reason (reviewed as a possibly weakened invariant).

No §11.2 numbered tests belong to this row. No backend tests. No mutations (Low tier).

## 6. Risks

- **Jest native-module load:** without the global mock, ios/android projects throw on import of `expo-clipboard` in every test that renders `OrderDetailView` — the global mock in step 2 covers it. The web jest project runs only `*.test.web.*` files, so it is unaffected.
- **Accessibility group merge:** if the button ends up inside the `accessible` header View, test 1 fails — keep it a sibling (§2).
- **Fake timers vs react-query/router:** enable fake timers only after `findByRole` resolves, or use `advanceTimersByTime` inside `act`; leaking fake timers into other tests breaks them — restore in `afterEach`.
- **Version drift:** `expo install --check` is a blocking gate; an exact pin that differs from the SDK recommendation fails it.
- **Phone width:** at ≤640 px the header row wraps; the outer `flex-wrap` keeps chip + button on one line or wraps cleanly. No new layout test (existing phone-width tests cover admin only).

## 7. Review

Low tier: Rob reads the diff (`git diff --stat`, the four files, the new test), checks the package is exactly `expo-clipboard` pinned exact and `expo-check` passed, no existing test edited, then runs full `verify.sh` for PASS. No mutations.
