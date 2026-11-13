# Northline order detail: design reference

Source: the "Northline Order.html" mock-up Nathaniel attached on 2026-11-12. It is the target look for the **customer** page `/orders/[id]`.

Nathaniel's decisions (2026-11-12):
- **Page:** the customer page `/orders/[id]`. Drop the admin-only parts: the "Need to help this customer?" card, the Customer card, and the "Admin — Order detail" footer.
- **Scope:** match the layout now, using only the data the page already has. Everything below marked **[feature]** gets a tracker row and is not built yet.

## Structure (top to bottom)

1. **Top bar.** It already exists (shell). This slice doesn't change it. The avatar initial and cart count are **[feature]**.
2. **Breadcrumb:** `Orders › ORD-…`. The order number is in mono; the "Orders" link goes to `/orders`.
3. **Order header card** (`.order-head`):
   - An "ORDER DETAIL" eyebrow, then the h1 "Order" with the order-number chip in mono. Copy button: **[feature]**.
   - Meta line: "Placed {date} · {n} item(s)". The customer name is admin-only, so drop it.
   - Actions: "← All orders" goes to `/orders`. "Reorder item": **[feature]**.
   - Status pills row, with a border on top: the order status pill (tone from `statusTone`) and the payment status pill.
   - The `pill-note` explanation text: **[feature]**.
4. **Cancellation alert** (`.alert`, red) explaining why the order was cancelled: **[feature]**. The app has no explanation text yet.
5. **Two-column grid**, `minmax(0,1fr) 360px`, which becomes one column at ≤960px:
   - **Left:**
     - **Items** card: the head is "Items" plus a sub line. Each row has the name and variant, a "× qty" chip, and the line total on the right. The thumbnail, SKU, unit price ("each") and colour attribute are **[feature]** unless the API already returns them.
     - **Timeline** card: the sub line reads "Newest last". It's a vertical line with dots (done = muted, cancelled = red with ×). Each entry has a title, a meta date and a reason tag (mono, red). Description text: **[feature]**.
   - **Right:**
     - **Totals** card: a table of Subtotal, Shipping (with "· {method}"), Tax and Total. The Total row has a border on top and is larger and bolder. Amounts are right-aligned with tabular-nums. The note under it is **[feature]**.
     - **Payment** card (method row, amount due, captured): **[feature]**. Show it only if the data exists.
     - **Shipping** card: a method row (icon, method name, rate) and a key/value list with "Deliver to" (the address) and "Fulfilment" (tracking link if shipped).

## CSS values (verbatim from the mock-up)

Use them as **design intent**, mapped onto the existing tokens in `lib/design-tokens.js` (P2-style). Don't add a second palette. Where a value differs from the P2-style tokens (for example `--text-2 #c6cdd9` or the rgba soft fills), the token wins unless Rob logs a decision.

```css
:root{ /* dark */
  --bg:#0a0d12; --bg-2:#0e1219; --surface:#12161f; --surface-2:#171c28; --surface-3:#1c2333;
  --border:#232a3a; --border-strong:#2e3850;
  --text:#f1f4f9; --text-2:#c6cdd9; --muted:#8b95a9; --faint:#6b768c;
  --accent:#10b981; --accent-strong:#0da678; --accent-ink:#04241a; --accent-soft:rgba(16,185,129,.13);
  --red:#f87171; --red-soft:rgba(248,113,113,.12); --red-border:rgba(248,113,113,.35);
  --amber:#fbbf24; --amber-soft:rgba(251,191,36,.12); --amber-border:rgba(251,191,36,.32);
  --radius:14px; --radius-sm:10px; --shadow:0 12px 32px rgba(0,0,0,.38);
}
html[data-theme="light"]{
  --bg:#f4f6f8; --bg-2:#eef1f4; --surface:#ffffff; --surface-2:#f8fafc; --surface-3:#eef2f6;
  --border:#e2e7ee; --border-strong:#cbd5e1;
  --text:#0f172a; --text-2:#334155; --muted:#64748b; --faint:#94a3b8;
  --accent:#0a7a5c; --accent-strong:#086a4f; --accent-ink:#ffffff; --accent-soft:#e6f4ef;
  --red:#dc2626; --red-soft:#fef2f2; --red-border:#fecaca;
  --amber:#b45309; --amber-soft:#fffbeb; --amber-border:#fde68a;
  --shadow:0 12px 28px rgba(15,23,42,.08);
}
body{font-size:15px;line-height:1.55}
.wrap{max-width:1180px;margin:0 auto;padding:22px 20px 56px}
.crumbs{display:flex;align-items:center;gap:8px;color:var(--muted);font-size:13.5px;margin-bottom:14px;flex-wrap:wrap}
.crumbs a{color:var(--muted);font-weight:600;padding:6px 2px}
.crumbs .sep{color:var(--faint)} .crumbs .here{color:var(--text);font-weight:600}
.order-head{background:var(--surface);border:1px solid var(--border);border-radius:var(--radius);padding:22px 24px;box-shadow:var(--shadow)}
.head-row{display:flex;gap:18px;align-items:flex-start;flex-wrap:wrap}
.head-main{flex:1;min-width:240px}
.eyebrow{font-size:12px;font-weight:700;letter-spacing:.09em;text-transform:uppercase;color:var(--muted);margin:0 0 4px}
h1{font-size:26px;line-height:1.15;margin:0;letter-spacing:-.02em;font-weight:800;display:flex;align-items:center;gap:10px;flex-wrap:wrap}
.order-no{font-family:"JetBrains Mono",monospace;font-size:15px;font-weight:600;background:var(--surface-3);border:1px solid var(--border);padding:5px 9px;border-radius:8px;letter-spacing:-.01em}
.head-meta{margin-top:8px;color:var(--muted);font-size:14px;display:flex;gap:8px;flex-wrap:wrap;align-items:center}
.head-meta .dot{width:3px;height:3px;border-radius:50%;background:var(--faint)}
.head-actions{display:flex;gap:9px;flex-wrap:wrap}
.btn{border:1px solid var(--border-strong);background:var(--surface-2);color:var(--text);border-radius:10px;padding:8px 14px;font-weight:600;font-size:14px}
.pills{display:flex;gap:8px;flex-wrap:wrap;margin-top:16px;padding-top:16px;border-top:1px solid var(--border)}
.pill{display:inline-flex;align-items:center;gap:7px;font-size:13px;font-weight:700;padding:6px 11px;border-radius:999px;border:1px solid}
.pill .pdot{width:7px;height:7px;border-radius:50%;background:currentColor}
.pill-cancelled{color:var(--red);background:var(--red-soft);border-color:var(--red-border)}
.pill-pay{color:var(--amber);background:var(--amber-soft);border-color:var(--amber-border)}
.alert{margin-top:16px;display:flex;gap:12px;background:var(--red-soft);border:1px solid var(--red-border);border-radius:var(--radius);padding:14px 16px}
.grid{display:grid;grid-template-columns:minmax(0,1fr) 360px;gap:16px;margin-top:16px;align-items:start}
.col{display:grid;gap:16px}
.card{background:var(--surface);border:1px solid var(--border);border-radius:var(--radius);overflow:hidden}
.card-head{padding:16px 20px 0;display:flex;align-items:center;justify-content:space-between;gap:12px}
.card-head h2{font-size:15px;margin:0;font-weight:700;letter-spacing:-.01em}
.card-head .sub{color:var(--muted);font-size:13px;font-weight:500}
.card-body{padding:14px 20px 20px}
.item-row{display:flex;gap:14px;align-items:center;padding:12px 0}
.item-name{font-weight:700;font-size:15px} .item-variant{color:var(--muted);font-size:13.5px;margin-top:1px}
.qty-chip .q{font-weight:700;background:var(--surface-3);border:1px solid var(--border);border-radius:8px;padding:5px 10px;font-size:13.5px}
.qty-chip .lbl{display:block;color:var(--faint);font-size:11.5px;margin-top:4px}
.line-total{text-align:right;min-width:76px} .line-total .amt{font-weight:700;font-size:15.5px}
.totals td{padding:7px 0;font-size:14.5px;color:var(--text-2)}
.totals td:last-child{text-align:right;font-weight:600;color:var(--text);font-variant-numeric:tabular-nums}
.totals tr.grand td{border-top:1px solid var(--border);padding-top:13px;font-size:17px;font-weight:800;color:var(--text)}
.kv dt{font-size:12px;font-weight:700;letter-spacing:.06em;text-transform:uppercase;color:var(--faint);margin-top:13px}
.kv dd{margin:3px 0 0;font-size:14.5px;color:var(--text-2)} .addr{line-height:1.6}
.method-row{display:flex;align-items:center;gap:11px;background:var(--surface-2);border:1px solid var(--border);border-radius:10px;padding:11px 13px}
.method-icon{width:36px;height:36px;border-radius:8px;background:var(--surface-3)}
.timeline:before{content:"";position:absolute;left:11px;top:14px;bottom:14px;width:2px;background:var(--border)}
.timeline li{position:relative;padding:10px 0 10px 40px}
.tl-dot{position:absolute;left:3px;top:13px;width:18px;height:18px;border-radius:50%;background:var(--surface);border:2px solid var(--border-strong)}
.tl-dot.done{border-color:var(--muted);background:var(--muted)}
.tl-dot.cancelled{border-color:var(--red);background:var(--red)}
.tl-title{font-weight:700;font-size:14.5px;display:flex;gap:9px;align-items:baseline;flex-wrap:wrap}
.tl-date{color:var(--muted);font-weight:500;font-size:13px}
.reason-tag{font-family:"JetBrains Mono",monospace;font-size:12px;background:var(--red-soft);color:var(--red);border:1px solid var(--red-border);padding:2px 7px;border-radius:6px;font-weight:600}
@media (max-width:960px){ .grid{grid-template-columns:1fr} }
@media (max-width:640px){
  .wrap{padding:16px 14px 44px} .order-head{padding:18px} h1{font-size:22px}
  .item-row{flex-wrap:wrap} .line-total{margin-left:auto}
  .head-actions{width:100%} .head-actions .btn{flex:1}
}
```

## [feature] items to add as tracker rows (not styling)

- Copy order number button
- Reorder item
- Cancellation alert with an explanation and `pill-note` text
- Item thumbnail, SKU, unit price ("each")
- Payment card: method, amount due, captured
- Timeline entry description text
- Totals note ("never charged …")
- Top bar avatar initial and cart count badge
