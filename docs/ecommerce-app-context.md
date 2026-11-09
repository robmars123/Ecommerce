# Ecommerce App Context

**What the app does, for whom, and in what order.** Read with the architecture (index `docs/ecommerce-architecture.md`, sections in `docs/architecture/`), which owns the stack, hard rules, data model, state machines, API, and slice order.

**Conflicts:** architecture wins on invariants, payments, and boundaries. This file wins on v1 product scope. If a conflict changes a user-facing workflow, the slice ends `BLOCKED` with the conflict in its report.

---

## 1. What this app is

A **single-store** ecommerce app — not a marketplace — on web, Android, and iOS from one Expo codebase.

- **Customers** browse, search, filter, build a cart, check out, pay, track orders, and request returns.
- **Guests** can browse and build a cart on their device, but must log in or register to check out.
- **Admins** manage products, stock, orders, returns, and customers from a dashboard, and fulfill orders manually.

**The bar:** checkout, inventory, payment, and notifications stay correct under concurrency, retries, and crashes. A pretty product page with a broken checkout is a failed build.

## 2. Roles

| Role | Can | Cannot |
|---|---|---|
| Guest | Browse, search, view products, keep a device cart | Check out, see orders, use admin |
| Customer | Everything a guest can, plus check out, pay, view own orders and timeline, request returns, manage own profile | See others' orders, use admin, set prices or totals |
| Support | Read orders, customers, and timelines | Change stock, status, refunds, or products. See revenue KPIs |
| Admin | Everything: catalog, stock (audited), fulfillment, returns, refunds, dashboard | Change a placed order's totals, delete orders, act without audit where audit is required |

Enforced in the API per endpoint **and** per resource (architecture §3.4 rule 11).

## 3. v1 features

1. Catalog: search, category filter, sort, pagination
2. Product detail: variants, images, price, stock indicator
3. Cart: device cart for guests, server cart for customers, merge on login, edit quantity, remove
4. Checkout: address, shipping method, server-calculated total, Stripe payment
5. Payments: webhook-confirmed, retry on the same order, admin refunds
6. Orders: history, detail, status timeline, tracking number
7. Returns: customer request; admin approve, decline, and refund
8. Admin dashboard: KPIs, orders by status, recent orders, low stock, top products, needs attention
9. Admin catalog: products, variants, categories, images, publish/unpublish
10. Admin inventory: stock levels, audited adjustments, low-stock alerts
11. Admin orders: filter, process, ship with tracking, deliver, audited override
12. Admin customers: list and detail (read-only)
13. Notifications: confirmation and shipped emails; live status via SignalR
14. Auth: register, login, logout, refresh rotation, password reset by email

**Anything not on this list is out of scope until Nathaniel adds it in writing.**

### 3.1 Deliverables

Each feature splits into deliverables that each touch **one** backend module (`backend/src/Modules/<M>`) or **one** frontend feature folder (`frontend/apps/store/features/<f>`). A deliverable ships with its own tests (arch §11) and adds no scope beyond its feature line. Backend before the screen that uses it. Slice = the arch §12.3 row whose numbered tests it carries.

Status as of 2026-11-07: **Done** (Rob PASS) · **Review open** (code done, Slice 4 waiver pending) · **In progress** (Slice 6, Marco) · **Not started** · **Unverified** (exists in part; scope not confirmed).

| ID | Deliverable | Module / folder | Slice | Status |
|---|---|---|---|---|
| **1** | **Catalog** | | | |
| 1.1 | Product list API: search, category filter, sort, pagination | Catalog | 1a | Done |
| 1.2 | Category list API | Catalog | 1a | Done |
| 1.3 | Shop screen: search, filter, sort, paging | `catalog` | 1a | Done |
| **2** | **Product detail** | | | |
| 2.1 | Product detail API: variants, images, price | Catalog | 1a | Done |
| 2.2 | Stock indicator (`InStock` / `Low` / `Out`) | Inventory | 2 | Done |
| 2.3 | Product detail screen with Add to cart | `catalog` | 1a/3 | Done |
| **3** | **Cart** | | | |
| 3.1 | Device cart for guests | `cart` | 3 | Done |
| 3.2 | Server cart API: add, edit quantity, remove, server prices | Cart | 3 | Done |
| 3.3 | Merge on login API | Cart | 3 | Done |
| 3.4 | Cart screen and merge-result notice | `cart` | 3 | Done |
| **4** | **Checkout** | | | |
| 4.1 | Stock reservation: lock in variant-ID order, reserve, commit on Paid, expire | Inventory | 2/4 | Done |
| 4.2 | Shipping methods API | Orders | 4 | Done |
| 4.3 | Checkout API: address, server total, idempotency key | Orders | 4 | Done |
| 4.4 | PaymentIntent creation | Payments | 4 | Done |
| 4.5 | Checkout screen: address, shipping, total, Stripe payment | `checkout` | 4 | Done |
| **5** | **Payments** | | | |
| 5.1 | Signed webhook confirms payment; dedupe by event ID | Payments | 4 | Review open |
| 5.2 | Retry payment on the same order | Orders | 4 | Review open |
| 5.3 | Admin refund API: ceiling, idempotency, audit | Payments | 6 | In progress |
| 5.4 | Stripe refund consumer and refund webhook | Payments | 6 | In progress |
| 5.5 | Admin refund form | `admin-orders` | 6 | In progress |
| **6** | **Orders** | | | |
| 6.1 | Order history and detail API (owner only) | Orders | 5 | Done |
| 6.2 | Status timeline and tracking number in detail | Orders | 5 | Done |
| 6.3 | Order history and detail screens | `orders` | 5 | Done |
| 6.4 | Refund rows in order detail (API + screen) | Orders, `orders` | 6 | In progress |
| **7** | **Returns** | | | |
| 7.1 | Return request API (customer) | Orders | 6 | In progress |
| 7.2 | Admin returns API: list, approve, decline (audited) | Orders | 6 | In progress |
| 7.3 | Return request form | `orders` | 6 | In progress |
| 7.4 | Admin returns screen | `admin-orders` | 6 | In progress |
| — | Refund of a return: uses 5.3–5.5 | | | |
| **8** | **Admin dashboard** | | | |
| 8.1 | Projector runtime: consumer checkpoints, gap handling, replay | Persistence | 6b | Not started |
| 8.2 | KPIs and orders-by-status projection | per arch §6.1 | 6b | Not started |
| 8.3 | Recent orders, low stock, needs-attention reads | per arch §6.1 | 6b | Not started |
| 8.4 | Top products projection | per arch §6.1 | 6b | Not started |
| 8.5 | Dashboard screen | `admin-dashboard` | 6b | Not started |
| **9** | **Admin catalog** | | | |
| 9.1 | Product create and edit API, with variants and image keys | Catalog | 1b | Done |
| 9.2 | Publish and unpublish API | Catalog | 1b | Done |
| 9.3 | Category create API | Catalog | 1b | Done |
| 9.4 | Category edit and delete | Catalog | — | Unverified |
| 9.5 | Image upload to storage (API returns a key only) | Catalog | — | Unverified |
| 9.6 | Admin product list, create and edit screens | `admin-catalog` | 1b | Done |
| 9.7 | Admin categories screen | `admin-catalog` | — | Unverified |
| **10** | **Admin inventory** | | | |
| 10.1 | Stock levels API | Inventory | 2 | Done |
| 10.2 | Audited adjustment API | Inventory | 2 | Done |
| 10.3 | Low-stock alerts | Inventory | 2 | Unverified |
| 10.4 | Inventory screen: levels, adjust, low stock | `admin-inventory` | — | Not started |
| **11** | **Admin orders** | | | |
| 11.1 | Process, ship with tracking, deliver, audited override APIs | Orders | 5 | Done |
| 11.2 | Admin order list API with filter | Orders | — | Not started |
| 11.3 | Admin order detail API | Orders | 6 | In progress |
| 11.4 | Admin order list screen with filter | `admin-orders` | — | Not started |
| 11.5 | Order action buttons on the admin detail screen | `admin-orders` | — | Not started |
| **12** | **Admin customers** | | | |
| 12.1 | Customer list and detail API (read-only) | Identity | — | Not started |
| 12.2 | Customer list and detail screens | `admin-customers` | — | Not started |
| **13** | **Notifications** | | | |
| 13.1 | Outbox runtime: retry, backoff, dead letter, alert | Persistence | 6 | In progress |
| 13.2 | Order events: paid, shipped, status changed | Orders | 6 | In progress |
| 13.3 | Email sender with placeholder senders | Notifications | 6 | In progress |
| 13.4 | Confirmation and shipped emails | Notifications | 6 | In progress |
| 13.5 | SignalR hub and live-status consumer | Notifications | 6 | In progress |
| 13.6 | Live status on the order screens | `orders` | 6 | In progress |
| 13.7 | Real email provider | — | — | Waiting on Nathaniel (arch §2) |
| **14** | **Auth** | | | |
| 14.1 | Register, login, logout, refresh rotation API | Identity | 1b | Done |
| 14.2 | Password reset token and confirm API | Identity | 1b | Done |
| 14.3 | Password-reset email through the outbox | Identity | 6 | In progress |
| 14.4 | Auth screens | `auth` | 1b | Done |

Rows with Slice "—" have no arch §12.3 row yet; Nathaniel assigns one before work starts.

---

## 4. Workflows

### 4.1 Browse

1. **Home:** best sellers, categories, search.
2. **Product list:** search, filter, sort (relevance, price ↑/↓, newest), and pagination — all server-side.
3. **Card:** image, name, price, category, stock indicator (In Stock / Low / Out). The indicator is display-only; checkout never trusts it.
4. Empty results show an empty state. Errors show a message with a retry — never a blank screen.

### 4.2 Product detail

1. Opened by slug: images, name, description, category, variants, price, stock.
2. Variants (size, colour) each have their own SKU, price, and stock. Selecting one updates price and stock.
3. Buyable only if the product is active **and** the variant has available stock. Inactive products are invisible to customers, even by direct link.
4. Related products: same category, best sellers first. No ML.

### 4.3 Add to cart

1. Choose a variant and quantity; the server validates it (active, 0 < qty ≤ cap). An invalid request changes nothing and returns a clear error.
2. Lines store the variant, quantity, and a display price snapshot. The real price is recalculated at checkout.
3. Add may be optimistic, but the server result wins. If price or stock moved, show what changed.

### 4.4 Guest cart merge

1. A guest cart lives on the device. On login it is merged into the customer's single server cart.
2. **Merge rule:** same variant → sum quantities, capped at available stock. Never drop silently. Show a merge result: kept, capped, removed, and why.
3. Clear the device cart after a successful merge.

### 4.5 Checkout and payment — the core workflow

Technical design: architecture §8.2. From the customer's view:

1. Checkout requires login; after login the customer returns to checkout with the cart intact.
2. Enter the address and choose a shipping method. The displayed total is an **estimate**.
3. Submit. The server re-validates the cart, reserves stock for **15 minutes**, and returns the authoritative total. If an item sold out or a price changed, show exactly which, and let the customer fix it and retry.
4. Pay with Stripe (Stripe.js on web, Stripe RN SDK on mobile).
5. Show a pending state until the server confirms payment. The order becomes **Paid** only when Stripe's webhook confirms it.
6. Card declined → retry payment on the **same order** while the reservation holds.
7. Reservation expires unpaid → the order is **Cancelled** and stock is released. The customer can check out again.
8. Paid → confirmation email, and live status in the app.

Must survive double-click, network retry, refresh after paying, duplicate or late webhooks, and crashes.

### 4.6 Track an order

1. **Orders:** number, date, total, status, tracking link once shipped.
2. **Detail:** items (snapshot prices), address, shipping method, payment status, timeline, tracking.
3. Live updates via SignalR while the order is open. A refresh is always correct without it.
4. Customers see only their own orders.

### 4.7 Return and refund

1. From a **Delivered** order, the customer requests a return: items, quantities, reason.
2. Nothing is refunded or restocked automatically.
3. The admin approves or declines with a reason. On approval, the admin refunds through Stripe.
4. A refund never deletes an order or changes its totals; it adds a refund record, a status change, and an audit entry.
5. Restock is a separate, audited admin adjustment.

### 4.8 Admin dashboard

The admin home is a dashboard, not a link list. Data is projected and shows an **as-of** time.

- **KPIs** (today / 7 days / 30 days): revenue (Paid minus refunds), order count, average order value, customers (new vs returning if easy, else total).
- **Orders by status:** a count per status; tapping one filters the order list.
- **Recent orders:** number, customer, date, total, status — paginated, newest first.
- **Low stock:** variants at or below the threshold (config, default 5), each linking to adjust.
- **Top products:** top 5 by quantity sold in the period.
- **Needs attention:** pending returns, dead letters, and projector lag beyond target — shown as alerts.
- **Quick actions:** Add Product, Adjust Stock, View Orders, View Returns.

Support sees a read-only order and customer view, with no revenue KPIs.

### 4.9 Admin catalog

1. List with search, category filter, and status filter (Active / Inactive / Out of Stock).
2. Create: name, slug, description, category, images (stored in object storage, keys only), variants (SKU, price). Inactive by default.
3. Set initial stock (audited), then publish. Only then can customers see and buy it.
4. Edit freely — placed orders keep their snapshots.
5. Unpublish hides the product immediately; existing orders are unaffected.

### 4.10 Admin inventory

1. Per variant: product, SKU, on hand, reserved, available, low-stock flag.
2. Adjust up or down with a required reason; audited (actor, before, after, reason).
3. An adjustment that would make available negative is rejected — and the attempt is audited.
4. No direct SQL in production, ever.

### 4.11 Admin orders and fulfillment

1. Filter by status, date, customer, or order number; paginated.
2. Detail: customer, items, address, shipping, payments, timeline, audit history.
3. Manual fulfillment: **Paid → Processing** (pick) → **Shipped** (carrier + tracking; shipped email) → **Delivered**.
4. Illegal transitions are rejected in the domain and the API.
5. Override: only a legal transition, with a reason, audited. It never rewrites totals or history.

### 4.12 Admin customers

List with search by name or email. Detail: profile, orders, total spent (Paid minus refunds), returns and refunds. Read-only in v1 — no editing, banning, or impersonation.

### 4.13 Notifications

- **Confirmation email** on Paid — exactly once per order.
- **Shipped email** with carrier and tracking.
- **SignalR** status updates while an order is open.
- No SMS, marketing email, or push in v1. Push (FCM/APNs) can come later behind `lib/platform.ts`.

---

## 5. Screens (Expo Router, identical on all platforms)

| Route | Who | Screen |
|---|---|---|
| `/` | Public | Home |
| `/products` | Public | List: search, filter, sort, pagination |
| `/products/[slug]` | Public | Detail, variants, add to cart |
| `/cart` | Public | Lines, quantities, merge result after login |
| `/checkout` | Customer | Address, shipping, authoritative total, payment |
| `/orders`, `/orders/[id]` | Customer | History; detail, timeline, tracking, return request |
| `/login`, `/register`, `/reset-password` | Public | Auth, returning to checkout when applicable |
| `/admin` | Admin, Support | Dashboard |
| `/admin/products`, `/admin/products/new`, `/admin/products/[id]` | Admin | Catalog: list, create, and edit with a customer preview |
| `/admin/inventory` | Admin | Stock and adjustments |
| `/admin/orders`, `/admin/orders/[id]` | Admin, Support | Orders and fulfillment |
| `/admin/returns` | Admin | Return requests |
| `/admin/customers`, `/admin/customers/[id]` | Admin, Support | Customers (read-only) |

A customer opening an admin route is redirected and receives no data — the API blocks it by role.

---

## 6. Out of scope in v1

- Marketplace or third-party sellers
- Subscriptions, loyalty, gift cards, discount codes (a server-validated code only if Nathaniel adds it)
- ML recommendations, personalization, external search engines
- Multi-warehouse, backorders, pre-orders
- SMS, marketing email, push notifications
- Warehouse automation or carrier APIs (admin enters tracking manually)
- Admin editing, banning, or impersonating customers
- Microservices, Kafka, Elasticsearch — only when a metric proves the need
- Kubernetes or any container orchestrator (Helm, k8s manifests, DOKS) — App Platform runs the one Docker image. Only when Nathaniel adds it in writing
- App Store and Google Play submission (builds are store-ready only)

*Updated 2026-11-02 · Companion: `docs/ecommerce-architecture.md`, `docs/quality-bar.md`.*
