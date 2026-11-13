# Slice P2-style-orders — Customer order pages restyle (plan)

Risk tier: **LOW** (layout only, CLAUDE.md §5). No mutations. Marco's tests + full `verify.sh`; Rob reads the diff.

**Read:** this plan · `docs/admin-dashboard-css-values.md` §1 (red/amber rows), §5 (`.panel*`, `.link`), §6 (`.status-row`, `.badge*`) · decision log row "Admin styling to the CSS values doc (P2-style" (grep) · `frontend/apps/store/lib/design-tokens.js` · `components/AdminPanel.tsx` + test · `features/orders/components/*` (views + tests) · `features/orders/status-labels.ts` · `tests/order-fixtures.ts`.

## 1. Scope

In: `/orders`, `/orders/[id]` — `OrderListView`, `OrderDetailView`, `OrderTimeline`, `TrackingLink`, `ReturnRequestForm` (rendered block only); the `AdminPanel → Panel` rename (D1); two token keys (D3).
Out: behaviour, API, data, hooks, routes, packages; other storefront pages; admin markup beyond the rename.

## 2. Decisions

| # | Decision | Reason | Rejected |
|---|---|---|---|
| D1 | **Rename** `components/AdminPanel.tsx` → `components/Panel.tsx` (`Panel`, same props `title?`/`children`, same markup); update the 11 admin importers (import + tag only); `AdminPanel.test.tsx` → `Panel.test.tsx` (A1/A2 unchanged but the name). Delete the old files, no alias | Props and markup are already neutral, only the name is admin. Every later storefront slice needs the same panel | Importing `AdminPanel` into customer pages (misleading name inherited by every storefront slice); a second near-identical `Panel`; an alias re-export (dead indirection) |
| D2 | `statusTone(status): StatusTone` in `features/orders/status-labels.ts`, `StatusTone = 'success' \| 'warning' \| 'danger' \| 'neutral'`, backed by `Record<OrderStatus, StatusTone>`. New `features/orders/components/OrderStatusBadge.tsx` maps tone → classes | Pure, testable without class assertions; a new status is a compile error. Two consumers (list row, detail header) | Shared `components/Badge` (no second consumer this slice); a `switch` in the component |
| D3 | Add `danger-subtle`, `danger-border` to both themes in `lib/design-tokens.js`, values = doc §1 `--red-bg`/`--red-border` | The P2-style rule: "plus only the keys a built element uses" — the Cancelled chip is that element, mirroring `warning-subtle/-border`. One token source, CSS variables → dark mode free. No hex in components | Danger chip as `bg-surface border-danger` (full-red outline the doc doesn't use); hex in a component (banned) |
| D4 | Item and totals rows are `accessible` Views with `accessibilityLabel`: totals `` `${label} ${amount}` `` (`Subtotal $40.00`), items `` `${name} × ${qty}, ${amount}` `` | Label and amount become two Texts; grouping keeps one screen-reader stop per row and gives tests a value-carrying query | Querying the amount Text alone — `$40.00` is both item line and subtotal in the fixture, so it is ambiguous |
| D5 | List row: card holds **one** `Link asChild` → `Pressable accessibilityRole="link"` (no label) wrapping number, date, badge, total; `min-h-touch`, `flex-row flex-wrap`. The optional `Track` link sits **outside** the Pressable, below it in the card | One ≥ 44 px target; no nested links. The accessible name is the row text in reading order | `accessibilityLabel={orderNumber}` (hides date/status/total from screen readers); Track inside the row (nested interactive) |
| D6 | Single column on all widths | Smallest change; the complaint is missing structure | Doc `.grid-2` at ≥ 981 px — layout logic + tests; backlog |
| D7 | Chip uses the `uppercase` class; Text child stays the plain label | Doc `.badge`; existing `getByText('Delivered')` and the timeline-order test keep matching | Upper-casing the string |

### Status → colour role (D2)

| OrderStatus | Tone | Classes |
|---|---|---|
| PendingPayment | warning | `border-warning-border bg-warning-subtle text-warning` |
| Paid, Processing, Shipped | neutral | `border-border bg-background text-muted` (doc `.badge-gray`) |
| Delivered | success | `border-primary-subtle bg-primary-subtle text-primary-text` |
| **Cancelled** | **danger** | `border-danger-border bg-danger-subtle text-danger` |
| Refunded, Returned | neutral | gray as above |

Shared (doc `.badge`): `self-start rounded-pill border px-sm py-xs text-badge font-bold uppercase`. Only Delivered is success (the one finished-well state); in-progress states stay neutral so attention goes to awaiting payment and cancelled.

## 3. Files

| File | Change |
|---|---|
| `components/Panel.tsx`, `components/Panel.test.tsx` (new, from `AdminPanel*`); `AdminPanel.tsx`, `AdminPanel.test.tsx` deleted | D1 |
| 11 admin importers (`admin-customers` ×2, `admin-orders` ×3, `admin-inventory`, `admin-catalog` ×4, `admin-dashboard/DashboardView.tsx`) | Import + tag only; `grep -rn AdminPanel frontend/apps/store --include=*.tsx` → 0 |
| `lib/design-tokens.js`; `lib/design-tokens-style.test.ts` | D3; T4 key list += the two keys |
| `features/orders/status-labels.ts` + new `status-labels.test.ts` | D2 |
| `features/orders/components/OrderStatusBadge.tsx` (new) | `{ status }` → one `Text`, tone classes, child `statusLabel(status)` |
| `OrderDetailView.tsx` | Local `Section` removed; panels per §4 |
| `OrderListView.tsx`, `OrderTimeline.tsx` | §4 |
| `TrackingLink.tsx` | Text `text-ui font-semibold text-primary-text underline` (doc `.link`); logic, `min-h-touch` untouched |
| `ReturnRequestForm.tsx` | Form, success alert and `ErrorState` branches each render inside `<Panel title="Return items">`; its own header Text removed (Panel title is the header). Logic untouched |
| `OrderDetailView.test.tsx`, `OrderListView.test.tsx` | §6 |

## 4. Layout spec (P2-style tokens; arbitrary px only where no token exists)

**Page**: keep `w-full max-w-content flex-1 self-center p-lg`; gap → `gap-gutter`. `ScreenTitle` unchanged.

**Detail summary** (`<Panel>`, no title): `flex-row flex-wrap items-center justify-between gap-sm` with order number `text-panel-title font-bold text-foreground` + badge; then date and the unchanged `Payment: …` line, both `text-meta text-muted`.

**Items** (`<Panel title="Items">`): row `flex-row items-start justify-between gap-md py-sm`, `border-b border-border` except the last (solid; RN single-side dashed is unreliable on iOS). Name `flex-1 text-ui text-foreground` (wraps); amount `shrink-0 text-right text-ui font-semibold text-foreground`. D4 label.

**Totals** (`<Panel title="Totals">`): local `TotalRow({ label, amount, emphasis? })` (4 uses). Row `flex-row justify-between gap-md`; label `text-ui text-muted`, amount `text-ui text-foreground`. Total: `mt-xs border-t border-border pt-md`, both Texts `text-body font-bold text-foreground`. D4 label.

**Shipping** (`<Panel title="Shipping">`): method `text-label font-semibold uppercase text-subtle`; address lines in one `View gap-xs`, each `text-ui text-foreground`, strings unchanged.

**Tracking / Refunds**: panels, conditionals unchanged. Refund row: amount·status `text-ui text-foreground`, date `text-meta text-muted`.

**Timeline** (`<Panel title="Timeline">`): keep `accessibilityRole="list"`; entries `gap-md`; each `flex-row gap-md`: dot `mt-[6px] h-sm w-sm rounded-pill bg-primary`, then a column — status `text-ui font-semibold text-foreground`, date and reason `text-meta text-muted`.

**List row** (D5): card `overflow-hidden rounded-panel border border-border bg-surface web:shadow-card`; Pressable `min-h-touch flex-row flex-wrap items-center gap-x-md gap-y-xs px-card py-md`: number `text-ui font-semibold text-foreground`, date `text-meta text-muted`, badge, total `ml-auto text-ui font-semibold text-foreground`. Track link in `px-card pb-sm`.

**375 px / modes**: no fixed width but the 8 px dot; amounts `shrink-0`, names wrap, rows wrap. Token classes only → both modes.

## 5. Build order

1. **Test-first** O1 → red (quote it) → `statusTone`.
2. D3 tokens + T4.
3. D1 rename; jest A1/A2 green.
4. `OrderStatusBadge`, `OrderTimeline`, `TrackingLink`, `ReturnRequestForm`.
5. `OrderDetailView` + its test changes + O3–O5.
6. `OrderListView` + its test changes + O2.
7. Web check (`npx expo start`, :8081), `/orders` and `/orders/<id>` at 375 px and desktop, light and dark: report `document.documentElement.scrollWidth` at 375 and that both modes are legible.
8. Full `bash scripts/verify.sh 2>&1 | tail -n 30`.

## 6. Tests

New (Jest):
- **O1 (test-first)** `statusTone` `it.each` over all 8 statuses per §2; explicit `Cancelled → danger`.
- **O2** list: first row link has the full accessible name (number, date, status, total, in order) — run once, quote RNTL's actual string, assert it exactly; `getAllByRole('link', { name: 'Track' })` stays length 1 and the row link's name has no `Track`.
- **O3** totals: `getByLabelText` `Subtotal $40.00`, `Shipping $5.00`, `Tax $0.00`, `Total $45.00`.
- **O4** item: `getByLabelText('Black tee, M × 2, $40.00')`.
- **O5** `getByRole('header', { name })` for Items, Totals, Shipping, Timeline.
- T4 extended (D3).

Existing tests affected — every value assertion kept:

| Test | Lines | Change | Why |
|---|---|---|---|
| `OrderDetailView.test.tsx` "shows items, totals…" | 33 | `getByText('$40.00')` → O4 label query | Subtotal amount is now its own `$40.00` Text → 2 matches; new query pins the item row |
| same | 34, 35 | `getByText` → `getByLabelText`, same strings | Label/amount split into two Texts |
| `OrderListView.test.tsx` | 41, 42, 64, 78, 103, 116 | `name: 'ORD-…N'` → `name: /^ORD-…N\b/` (41 also O2 exact) | Link name is now the whole row (D5); still anchored on the number |
| `AdminPanel.test.tsx` → `Panel.test.tsx` | all | Name only | D1 |
| `design-tokens-style.test.ts` T4 | 5 | +2 keys | D3, additive |

Must stay green unchanged: timeline order (L48 — badge `Shipped` is still the first match), payment line, refunds, return form, tracking, `getByText('Delivered')`, `getAllByText('$57.50')`, `getAllByText('Mar 4, 2026')`. Any other test change is BLOCKING unless justified in the Marco report.

## 7. Risks

- RNTL v14.1 accessible-name separator decides the O2 string — quote, don't guess.
- `Link asChild` + Pressable must still render `<a href>` on web — check hover shows the URL.
- Rename churn in 11 admin files — Rob checks each hunk is import + tag only.
- `uppercase` via NativeWind on web — visual check.

## 8. Backlog candidates

Two-column order detail at ≥ 981 px (D6); `OrderStatusBadge` tones for the admin order list.

## Amendment 1 — design ref (2026-11-12)

**Read** += `docs/design-refs/northline-order-detail.md` · `features/admin-dashboard/layout.ts`. Supersedes D6, D7, §4 Detail/Timeline/List row. Built: D1–D3, O1–O5, D5.

| # | Decision | Rejected |
|---|---|---|
| A1 | `features/orders/layout.ts` `orderDetailLayout(w)` → `{ twoColumn: w ≥ sidebarBreakpoint, compact: w < narrowBreakpoint }` + `useWindowDimensions`. Tokens win: 981/561 over 960/640 | media variants |
| A2 | `OrderStatusBadge` → `StatusPill({ tone, label })`: 7 px dot, `text-ui-sm font-bold`, no uppercase. `paymentTone()`: Pending warning, Succeeded success, Failed danger, else neutral | 2nd chip |
| A3 | Mono: `font-mono` + `#root .font-mono, #root .font-mono *` rule in `public/index.html` = `webFontFamily.mono` | inline style |
| A4 | `Panel` optional `subtitle` | own card |
| A5 | `sku`, `unitPrice` are in `OrderItem` → shown | dropping real data |

**Map**: 26/22 `page-title(-sm)` · 17 `title` · 15–15.5 `panel-title` · 14–14.5 `ui` · 13–13.5 `ui-sm` · 12 `label`; text-2→foreground, faint→subtle, surface-2/3→surface-raised, border-strong→border; radius 8 control, 10 input.

### Detail, top → bottom
- `ScreenTitle` only while loading/error.
- **Breadcrumb**: Link "Orders" → `/orders` (`min-h-touch`), `›`, mono number.
- **Header** `<Panel>`: eyebrow "Order detail" `uppercase`; h1 = `accessible` header View labelled `Order {number}`: "Order" + mono chip; meta "Placed {date} · {n} item(s)", n = Σ quantity; action Link `/orders` "← All orders" (label "All orders", `min-h-touch`); pills row `border-t pt-lg`: order pill, `Payment: {label}` pill if non-null.
- **Grid** `gap-lg`; twoColumn: row, left `min-w-0 flex-1`, right `w-[360px]`.
- **Left**: Items: name bold + mono SKU · "× {q}" chip + "{unit} each" · line total right; D4 label kept. `ReturnRequestForm` (Delivered). Timeline, subtitle "Newest last": 18 px rail, muted ring dot, Cancelled `bg-danger` + hidden "×", 2 px line except last; status + date in one row; reason = mono danger tag.
- **Right**: Totals: "Shipping · {method}", amounts `tabular-nums`, Total `border-t text-title font-bold`. Shipping: method row (name, rate); dt `text-label uppercase`: "Deliver to" address, "Fulfilment" carrier · number + `TrackingLink`, omitted with no shipments (Tracking panel gone). Refunds unchanged, last.
- **Page** `p-xl`; compact: `px-gutter py-lg`, h1 `-sm`, item rows wrap (total `ml-auto`), action full width.
- Not built ([feature] → tracker P2.48–P2.55); method icon dropped.

**List**: D5 plus mono number, date `ui-sm`, `StatusPill`, total `panel-title font-bold`; O2 string unchanged.

### Tests
New: **L1** layout 560/561/980/981 (red first) · **O6** `paymentTone` ×5 · **O7** header `Order ORD-2026-000001`; "Orders" and "All orders" → `/orders` · **O8** `Placed Mar 4, 2026 · 2 items`, `TEE-BLK-M`, `× 2`, `$20.00 each` · **O9** Cancelled reason shown · **O10** no shipments → no "Fulfilment" · **A4** subtitle · **F6** index.html has `webFontFamily.mono`.
Changed, values kept: 9 `findByText('ORD-…01')` sentinels → O7 query (number shown twice) · `'Black tee, M × 2'` → `'Black tee, M'` + `'× 2'` · `Shipping $5.00` → `Shipping · Standard shipping $5.00`. Others BLOCKING.

**Order**: jest on the tree → L1/O6 red → A2–A4 → detail → list → web check (§5.7, 375/800/1280) → `verify.sh`. **Risk**: `font-mono` must give a platform family on native and reach the DOM on web (quote both); else drop it.
