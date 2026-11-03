# Ecommerce App Context — For Claude Code Agents

## How to use this file

Read this file FIRST, before writing any code, together with `ecommerce-architecture.md`.

- This file answers: **what does the app do, for whom, in what order?**
- `ecommerce-architecture.md` answers: **how is it built safely, what are the invariants, and what tests prove it?**
- If the two files conflict on an invariant, a payment rule, or a module boundary, `ecommerce-architecture.md` wins. If they conflict on product scope, this file wins for v1 scope. Stop and ask Nathaniel if the conflict changes a user-facing workflow.

Agents on this project:
- **Rob (Architect)** — owns architecture, slice order, module boundaries, and plan-mode challenges. Reviews, does not implement.
- **Marco (Software Developer 2)** — implements one slice at a time, in slice order. Reports concerns, bugs found, bugs fixed, and what shipped.
- **Nathaniel** — owns the product decisions, approves plans, reviews every diff, and merges. No slice starts until the previous slice's tests pass and he has reviewed it.

## 1. What this app is

A single-store ecommerce app, not a marketplace.

A customer can browse products, search and filter, add items to a cart, check out and pay, track an order, and request a return. An admin can manage products, stock, orders, and customers from a dashboard, and fulfil orders manually.

A visitor who is not logged in can browse and build a guest cart. They must log in (or register) before checkout.

The goal of this build is depth, not breadth: the checkout, inventory, payment, and notification flows must be correct under concurrency, retries, and crashes. A pretty product page with a broken checkout is a failed build.

## 2. Stack lock — do not change without Nathaniel's approval

- **Backend:** .NET Core — ASP.NET Core on .NET 10, C#, Entity Framework Core, modular monolith (one API, one deployment, hard module boundaries inside it)
- **Database:** PostgreSQL 17 — system of record for all money, inventory, and order state. Never in Redis alone.
- **Cache:** Redis 7 — cache only. Losing Redis must never lose an order or oversell stock.
- **Frontend:** ONE codebase — Expo + React Native + React Native Web, TypeScript, Metro, Expo Router
  - Web: `expo export --platform web` to a CDN / static host
  - Android: Cloud EAS Build to a signed AAB (no Android Studio, no local Gradle, no local Android SDK)
  - iOS: Cloud EAS Build to a signed IPA (no Mac needed, no Xcode)
  - Store-ready, NOT submitted. Do not build a separate React web app. Do not use Capacitor.
  - Full React Native only: all screens are React Native components. Web is those same components via React Native Web. No native Android Studio project, no native Xcode project, no Kotlin/Java, no Swift. For local testing use Expo Go on a physical phone and web export, not an Android emulator.
- **Frontend state:** TanStack Query for server state, Zustand for cart UI state only. Cart truth is re-validated server-side at checkout.
- **Auth:** ASP.NET Core Identity + JWT access token (short-lived) + rotating refresh token
- **Payments:** Stripe PaymentIntents + signed webhooks, test mode first. No real keys in code, in chat, or in this file.
- **Email:** Provider-agnostic `IEmailSender` (SMTP / Resend / SendGrid behind the interface)
- **Images:** S3-compatible object storage + CDN. Never store images in Postgres as blobs.
- **Real-time:** SignalR for order status updates to the customer only. Not required for checkout correctness.
- **Background work:** Hosted worker inside the API process — reservation expiry, outbox publisher, email retries. No separate worker service in v1.
- **Tests:** xUnit + Testcontainers (real Postgres) + WebApplicationFactory. Integration tests use real Postgres, not in-memory EF.
- **Hosting:** DigitalOcean App Platform + Managed Postgres + Managed Redis + Spaces, Docker from CI. GitHub Actions: build, test, migrate check, Docker build. No deploy on red.

## 3. Users and roles

| Role | Can do | Cannot do |
|---|---|---|
| Guest (not logged in) | Browse catalog, search, view product detail, build a guest cart | Check out, view orders, request a return, access admin |
| Customer | Everything a guest can do, plus check out, pay, view own orders and status timeline, request a return, manage own profile | View other customers' orders, access admin, change prices or order totals |
| Support (read-only orders) | View orders and customers to help, view status timeline | Change stock, change order status, refund, edit products |
| Admin | Everything, plus manage products and variants, adjust stock (with reason, audited), fulfil and ship orders, approve refunds and returns, view the dashboard | Change an order's total after it is placed, delete an order, edit data without an audit trail where audit is required |

Authorization is enforced in the API per endpoint AND per resource. Hiding a link in the frontend is not authorization. `GET /orders/{id}` must check `order.user_id == current_user_id` or Admin/Support role.

## 4. Core features at a glance (v1 only)

1. Catalog browsing, search, filter, sort, pagination
2. Product detail with variants, images, price, and stock indicator
3. Cart: guest cart, logged-in cart, merge on login, quantity updates, remove
4. Checkout: address, shipping method, server-calculated total, Stripe payment
5. Payments: PaymentIntent, signed webhook as truth, retry payment on the same order, admin refund
6. Orders: history, detail, status timeline, tracking number
7. Returns: customer return request, admin approve/decline, refund flow
8. Admin dashboard: revenue, orders, low stock, recent orders, top products
9. Admin catalog management: create/edit products, variants, categories, images, publish/unpublish
10. Admin inventory management: stock levels, adjustments with reason and audit log, low-stock alerts
11. Admin order management: list, filter by status, ship, add tracking, override status (audited)
12. Admin customer view: customer list, order history per customer (read-only for Support)
13. Notifications: order confirmation email, shipped email, order status updates via SignalR
14. Auth: register, login, logout, refresh token rotation, password reset (via email link, never in chat)

That is the whole v1. If a feature is not in this list, it is out of scope until Nathaniel adds it in writing.

## 5. Workflows — the app, step by step

### 5.1 Shopper: browse the catalog

1. Customer opens Home. Home shows best sellers, categories, and a search box.
2. Customer opens Product List. List is server-driven: search, category filter, sort, and pagination all happen on the API, never "load all products and filter in the browser."
3. Sort options in v1: relevance (Postgres full-text rank), price low to high, price high to low, newest.
4. Each product card shows: image, name, price, category, and a stock indicator (In Stock / Low Stock / Out of Stock). Stock indicator is display only and may be cached briefly. Checkout never trusts it.
5. Empty results show a clear empty state, not a blank page. Errors show a clear message with a retry, not a crash.

### 5.2 Shopper: view a product

1. Customer opens Product Detail by slug: images, name, description, category, variants, price, stock indicator.
2. Variants in v1: a product can have variants such as size or colour, each with its own SKU, price, and stock. Selecting a variant updates price and stock indicator for that variant only.
3. A product is only buyable when `is_active = true` AND the selected variant has available stock. Inactive products are not listed to customers, even by direct link (admin can preview them).
4. Related products in v1 are simple rules only: same category, best sellers first. No ML recommendation engine in v1.

### 5.3 Shopper: add to cart

1. Customer taps Add to Cart on a product card or detail page, choosing a variant and quantity.
2. API validates: variant exists, variant is active, quantity is greater than 0 and within a reasonable per-order cap. Invalid requests return a clear 400 with no cart change.
3. Cart line stores: variant, quantity, and a price snapshot for display. The authoritative price is always re-calculated server-side at checkout.
4. Customer can update quantity or remove a line from the Cart screen. Cart badge updates from server-validated cart state after each change.
5. Cart add can be optimistic in the UI, but the server result wins. If stock or price moved, the UI must show what changed, not silently keep the old value.

### 5.4 Guest cart and login merge

1. A guest cart is stored locally on the device and, when the guest later logs in, merged on the server.
2. Merge rule: same product + variant sums quantity, capped at available stock. Never silently drop items. If a merge changes quantity or drops an unavailable item, show the customer a merge result screen: what was kept, what was capped, what was removed and why.
3. After merge, there is exactly one cart per user. Guest cart data is cleared after a successful merge.

### 5.5 Shopper: checkout and payment (the core workflow)

This is the most important workflow in the app. Read Section 8.2 of `ecommerce-architecture.md` before coding it. Summary, in plain language:

1. Customer opens Checkout from Cart. They must be logged in. If not, they are sent to login/register first, then returned to checkout with the cart intact.
2. Customer enters shipping address and chooses a shipping method. The frontend shows an estimated total. That total is an estimate only.
3. Customer submits checkout. The frontend generates one UUID for this checkout attempt and sends it as `Idempotency-Key`.
4. API, in one Postgres transaction:
   - Checks the idempotency key. If an order already exists for it, return that order. Do not create a second order.
   - Re-validates the cart: every variant still active, price snapshot taken, stock available.
   - Locks inventory rows in a consistent order (`SELECT ... FOR UPDATE`) to prevent oversell and deadlocks.
   - Reserves stock: `quantity_reserved` goes up, `quantity_on_hand` does not change yet. Reservation expires in 15 minutes (config value, not hard-coded).
   - Creates the order as `PendingPayment` with order items (price snapshots) and a payment record.
5. API creates a Stripe PaymentIntent AFTER that transaction commits, for the server-calculated grand total, with `order_id` in metadata. It stores the PaymentIntent ID on the order.
6. Frontend confirms payment with Stripe (Stripe.js on web, Stripe React Native SDK on mobile). The secret key never leaves the server. The frontend only ever sees `client_secret`.
7. Stripe sends a signed webhook `payment_intent.succeeded`. The webhook is the source of truth for payment success. The frontend result is only a hint.
8. Webhook handler, idempotently:
   - Verifies the Stripe signature. Rejects unsigned requests.
   - Checks the Stripe event ID in `idempotency_keys`. If already processed, returns 200 and does nothing. A duplicate webhook must not create a second order, a second inventory commit, or a second email.
   - In one transaction: order goes to `Paid`, reservation goes to `Committed`, `on_hand` decreases by quantity, `reserved` decreases by quantity, status history row is inserted, and an `OrderPaid` outbox event is inserted in the same transaction.
9. Background outbox publisher picks up `OrderPaid`, sends the confirmation email, and pushes a SignalR order update. If email is down, the order stays `Paid` and the email retries from the outbox with backoff. After repeated failures it goes to dead-letter (log + admin view), order state unchanged, no duplicate email on retry.
10. If payment fails or the customer abandons checkout, the reservation expires after 15 minutes, stock is released, and the order goes to `Cancelled`. The customer can retry payment on the SAME order (new PaymentIntent, same order) — never a second order for a retry.

Checkout must survive: double-click, network retry, browser/app refresh after payment, webhook arriving twice, webhook arriving late, and a crash between any two steps.

### 5.6 Shopper: track an order

1. Customer opens Orders, sees a list: order number, date, total, status, and tracking link when shipped.
2. Customer opens Order Detail: items with price snapshots, shipping address, shipping method, payment status, status timeline (Placed → Paid → Processing → Shipped → Delivered, plus Cancelled / Refunded / Returned where applicable), and tracking number when available.
3. Status updates arrive via SignalR when the customer has the order open, and are always correct on refresh from the API. SignalR is a convenience, not the truth.
4. Customer can only see their own orders. No order ID guessing.

### 5.7 Shopper: return and refund request

1. From a Delivered order, customer submits a Return Request: which items, quantity, and reason.
2. Request goes to admin as `ReturnRequested`. The order is not automatically refunded or restocked.
3. Admin approves or declines with a reason. On approval, admin processes a refund through Stripe (idempotent by refund ID).
4. Refund rules: a refund never deletes the order or rewrites its totals. It adds a payment record and a status change, and writes an audit log entry.
5. Restock on return is a separate, explicit admin action with a reason. Do NOT auto-restock on refund in v1.

### 5.8 Admin: dashboard

The admin home screen is a dashboard, not just a list of links. It must show, at a glance:

- **KPIs (with date filter: today, last 7 days, last 30 days):** total revenue (Paid orders only, refunds subtracted), number of orders, average order value, number of customers (new vs returning in the period, if easy — otherwise total customers).
- **Orders by status:** counts for PendingPayment, Paid, Processing, Shipped, Delivered, Cancelled, Refunded, Returned. Tapping a count filters the order list to that status.
- **Recent orders table:** order number, customer, date, total, status, with a link to order detail. Paginated, newest first.
- **Low stock alerts:** variants where available stock is at or below a threshold (threshold is a config value, default 5 in v1). Each row links to the inventory adjust screen.
- **Top products:** top 5 products by quantity sold in the selected period.
- **Needs attention:** pending return requests count, outbox dead-letter count (if greater than 0, show it as an alert, not a quiet log line), and reservations expiring soon is NOT needed on the dashboard in v1.
- **Quick actions:** Add Product, Adjust Stock, View Orders, View Returns.

Support role sees a read-only version of order and customer data only. Support does not see revenue KPIs in v1, and cannot change stock, status, or refunds.

### 5.9 Admin: manage catalog

1. Admin opens Products, sees a list with search, category filter, and status filter (Active / Inactive / Out of Stock).
2. Admin creates a product: name, slug, description, category, images (uploaded to object storage, keys stored only), variants (SKU, price, stock initial value), and `is_active = false` by default.
3. Admin sets initial stock with a reason (audit log), then publishes: `is_active = true`. Only then can customers see and buy it.
4. Admin edits a product or variant. Price changes do NOT change already-placed orders — those keep their price snapshots.
5. Admin unpublishes a product (`is_active = false`). Existing orders are unaffected. The product disappears from customer catalog immediately.

### 5.10 Admin: manage inventory

1. Admin opens Inventory, sees variants with: product, variant, SKU, on-hand, reserved, available (on-hand minus reserved), and low-stock flag.
2. Admin adjusts stock: increase or decrease, with a required reason. Every adjustment writes an audit log entry (actor, before, after, reason).
3. An adjustment that would make available stock negative is rejected.
4. Admin never edits stock with direct SQL in production. Only through this audited endpoint.
5. Low-stock list is the same data as the dashboard alert, with a link to adjust.

### 5.11 Admin: manage orders and fulfilment

1. Admin opens Orders, filters by status, date, customer, or order number. List is paginated.
2. Admin opens Order Detail: customer, items with snapshots, address, shipping, payment record, status timeline, audit history.
3. Fulfilment in v1 is manual, not warehouse automation:
   - Paid → Processing (admin picks the order)
   - Processing → Shipped (admin adds carrier + tracking number; shipped email is sent via outbox)
   - Shipped → Delivered (admin or delivery confirmation)
4. Only legal status transitions are allowed. Illegal transitions are rejected in the domain layer AND in the API. The status machine is tested, not just hoped for.
5. Admin can override a status only with a reason, and the override is audited. An override never rewrites totals or deletes history.

### 5.12 Admin: manage customers

1. Admin opens Customers, sees a list with search by name or email.
2. Admin opens a Customer Detail: profile, order history, total spent (Paid orders minus refunds), and return/refund history.
3. This is read-only except for admin notes in a later version. In v1, do not build customer editing, banning, or impersonation.

### 5.13 Notifications (customer-facing)

- Order confirmation email: on `OrderPaid`, via outbox, exactly once per order (duplicate webhook must not duplicate it).
- Shipped email: on Shipped, via outbox, with carrier and tracking number.
- Order status update: SignalR to the customer when they have the order open.
- No SMS in v1. No marketing emails in v1. No push notifications required for checkout correctness; FCM/APNs push is a platform-only addition later, behind the frontend platform boundary.

## 6. Data in plain language (what the agents must model)

Do not invent extra tables in v1. These are the core entities, with their key fields:

- **User:** id, email, password hash (Identity), role (Customer / Support / Admin), created date. A user has many orders, one cart (after merge).
- **Category:** id, name, slug, parent category (optional, one level in v1).
- **Product:** id, name, slug, description, category, is_active, created/updated dates. A product has many variants and many images.
- **ProductVariant:** id, product, SKU (unique), price, attributes (size / colour as simple fields in v1, not a generic attribute engine).
- **ProductImage:** id, product, storage key (not a blob), sort order, alt text.
- **InventoryStock:** one row per variant — on_hand, reserved. Available is always calculated as on_hand minus reserved, never stored as a separate truth.
- **InventoryReservation:** id, variant, order, quantity, status (Active / Committed / Released / Expired), expires_at (now + 15 minutes).
- **Cart / CartItem:** cart belongs to a user or a guest session; cart items are variant + quantity. Cart is not an order and has no payment state.
- **Order:** id, order number (human-readable, unique), user, status, shipping address snapshot, shipping method, subtotal, discount, shipping cost, tax, grand total (all server-calculated), Stripe PaymentIntent ID, created/updated dates.
- **OrderItem:** id, order, variant, SKU snapshot, name snapshot, unit price snapshot, quantity, line total. Snapshots never change when catalog prices change.
- **Payment:** id, order, Stripe PaymentIntent ID, amount, status (Pending / Succeeded / Failed / Refunded / PartiallyRefunded), Stripe event IDs processed.
- **IdempotencyKey:** key (client checkout key or Stripe event ID), scope, order or event it produced, created date. This table is what makes retries safe.
- **OutboxEvent:** id, type (OrderPaid, OrderShipped, etc.), payload, status (Pending / Published / DeadLetter), attempts, next retry date, published date.
- **Shipment:** id, order, carrier, tracking number, shipped date.
- **ReturnRequest:** id, order, items, quantities, reason, status (Requested / Approved / Declined / Refunded), admin note.
- **Refund:** id, order, payment, Stripe refund ID, amount, reason, admin actor.
- **AuditLog:** id, actor, action, entity type and id, before, after, reason, date. Required for stock adjusts, status overrides, and refunds.
- **Address:** stored as a snapshot on the order, not only as a live user profile. Changing a profile address later must not rewrite a shipped order's address.

Full column types, indexes, and constraints live in `ecommerce-architecture.md` Section 7. If you add a field here, add it there too, with a migration.

## 7. State machines (only these transitions are legal)

**Order:** PendingPayment → Paid → Processing → Shipped → Delivered
- PendingPayment → Cancelled (payment failed, abandoned, or reservation expired)
- Paid → Cancelled is NOT allowed in v1 (use Refunded after approval, not a silent cancel)
- Delivered → ReturnRequested → Returned (after admin approval + refund, where applicable)
- Any order → Refunded only via the admin refund flow, with a payment record and audit entry
- Illegal transitions are rejected in the domain layer, not just hidden in the UI

**Payment:** Pending → Succeeded / Failed → Refunded / PartiallyRefunded (admin only)
- A Succeeded payment is only set from a verified, idempotent Stripe webhook, never from the frontend alone

**Reservation:** Active → Committed (on Paid) / Released (on cancel) / Expired (after 15 minutes, by the expiry job)
- An expired or released reservation never becomes Committed later

**ReturnRequest:** Requested → Approved → Refunded / Declined
- Approved does not by itself move money. The refund step does, and it is audited separately

## 8. Hard rules agents must never break

1. Never trust the client for money. Prices, totals, discounts, shipping, and tax are calculated server-side at checkout.
2. One idempotency key = at most one order. One Stripe event ID = at most one transition and one outbox event.
3. Stock is never oversold. Checkout locks inventory rows, reserves in a transaction, and commits only on Paid.
4. Postgres is the truth. Redis is cache only. Checkout never reads stock from cache.
5. Webhook is the truth for payment success. Frontend success is a hint only.
6. Side effects go through the outbox, in the same transaction as the state change. Never send email inside the order transaction, and never send it with no record if the transaction later fails.
7. No business logic in controllers. Controllers validate, call application services, and map to responses.
8. No cross-module DbSet and no cross-module SQL join in application code. A module reads another module only through its public contract or a domain event.
9. No `DateTime.UtcNow` in domain/application code. Inject `IClock` so expiry tests are deterministic.
10. No secrets in git, no Stripe keys in code or chat, no card numbers in logs. Secrets live in environment variables / secret manager only.
11. Every new public method that moves money or stock gets a test in the same slice, not "later."
12. Every admin stock adjust, status override, and refund writes an audit log entry with actor, before, after, and reason.

## 9. Screens / routes (Expo Router, one codebase)

Same screens on web, Android, and iOS. Route names are the Expo Router paths:

- `/` — Home: best sellers, categories, search
- `/products` — Product list: search, filter, sort, pagination
- `/products/[slug]` — Product detail: variants, images, stock indicator, add to cart
- `/cart` — Cart: lines, quantity edit, remove, merge result after login
- `/checkout` — Checkout: address, shipping, payment, authoritative total review
- `/orders` — Order history
- `/orders/[id]` — Order detail: items, timeline, tracking, return request
- `/login`, `/register` — Auth, with return-to-checkout after login
- `/admin` — Admin dashboard (KPIs, orders by status, recent orders, low stock, top products, needs attention)
- `/admin/products`, `/admin/products/[id]` — Catalog management
- `/admin/inventory` — Inventory list and adjust (with reason)
- `/admin/orders`, `/admin/orders/[id]` — Order management and fulfilment
- `/admin/customers`, `/admin/customers/[id]` — Customer view (read-only for Support)

Admin screens are hidden from customers in the UI AND blocked in the API by role. A customer opening an admin route directly must be redirected, not shown data.

## 10. API surface summary (v1, base `/api/v1`)

Full details, error format (Problem Details), and pagination rules are in `ecommerce-architecture.md` Section 9. Summary by area:

- Public catalog: `GET /products`, `GET /products/{slug}`, `GET /categories`
- Cart: `POST /cart/items`, `GET /cart`, `DELETE /cart/items/{variantId}`
- Checkout: `POST /checkout` (header `Idempotency-Key` required), `POST /orders/{id}/retry-payment` (same order, never a second order)
- Orders: `GET /orders`, `GET /orders/{id}`, `POST /orders/{id}/return-request`
- Webhook: `POST /webhooks/stripe` (signed, not Bearer auth)
- Admin catalog: `GET/POST /admin/products`, `PUT /admin/products/{id}`
- Admin inventory: `POST /admin/inventory/adjust` (with reason, audited)
- Admin orders: `GET /admin/orders`, `POST /admin/orders/{id}/ship`, `POST /admin/orders/{id}/refund`

All list endpoints are paginated (`page`, `pageSize`, max 100) and return a total count. All write DTOs are explicit — no `grand_total` or `status` field a client can set on an order.

## 11. Build order for agents (do not skip)

Follow `ecommerce-architecture.md` Section 12.3 exactly. In short:

- **Slice 0 — Foundation:** Solution structure, DI, Postgres + migrations, health endpoints, Serilog + correlation ID, CI green. Done when `dotnet test` passes and `/health/ready` returns 200 with Postgres up.
- **Slice 1 — Identity + Catalog (read):** Users, roles, products, variants, categories, list/detail/search with Postgres full-text. Done when public can browse/search/paginate and admin can create a product (no stock yet).
- **Slice 2 — Inventory:** Stock table, reservation, release, expiry job, available calculation, check constraint. Done when the parallel oversell test passes.
- **Slice 3 — Cart:** Guest + user cart, merge on login, server validation snapshot. Done when cart survives login merge without silent drops.
- **Slice 4 — Checkout + Payments (the core):** Idempotent checkout, reservation in transaction, Stripe PaymentIntent, signed webhook, Paid transition + inventory commit, outbox. Done when the idempotency, double-webhook, expiry, and crash-recovery tests pass.
- **Slice 5 — Orders + Fulfilment:** Order history, status timeline, admin ship, tracking, status machine enforcement. Done when illegal transitions are rejected in domain and API tests.
- **Slice 6 — Notifications + Returns/Refunds:** Outbox publisher, confirmation/shipped emails, SignalR updates, return request + admin refund flow. Done when no duplicate email is sent on retry and refund tests pass.
- **Slice 7 — Hardening:** Rate limiting, admin audit log, caching per the architecture file, slow-query pass, production deploy checklist, staging end-to-end on DigitalOcean.

Do not start Slice 4 early because it looks like the interesting part. Slices 0 to 3 are what make Slice 4 testable.

## 12. Out of scope in v1 (do not build, do not sneak in)

- Marketplace / third-party sellers
- Subscriptions, loyalty points, gift cards, discount-code engine beyond a simple server-validated code in checkout if Nathaniel explicitly adds it later (no client-trusted discounts in v1)
- ML recommendations, personalised feed, advanced search engine (Postgres full-text only in v1)
- Multi-warehouse inventory, backorders, pre-orders
- SMS notifications, marketing emails, push notifications required for checkout
- Native warehouse automation, carrier API integration beyond storing a tracking number the admin enters
- Customer editing, banning, or impersonation by admin
- Microservices, Kafka, Elasticsearch, or a separate search service — only when a metric proves the current layer is the bottleneck
- Store submission to Google Play or the App Store — builds are store-ready only in v1

## 13. How agents start (verbatim prompts)

**Rob, plan mode first, before Slice 0 code:**
> Read ecommerce-app-context.md and ecommerce-architecture.md in this repo. In plan mode only, no code: confirm the slice order, list the module boundaries for Slice 0, and list the risks if Slice 0 is built wrong. Return risks only. Do not change the stack, schema, or slice order.

**Marco, Slice 0 only:**
> Read ecommerce-app-context.md and ecommerce-architecture.md in this repo. Build Slice 0 only: solution structure (Domain / Application / Infrastructure / Api), Expo app scaffold with the screens in Section 9 stubbed (no checkout logic yet), DI wiring, Postgres migrations pipeline, health endpoints, Serilog with correlation ID. Do not build catalog, cart, or checkout yet. Return the files changed and the test command result.

**Failing test first, before Slice 4 code:**
> Write the failing test for 10 parallel checkouts for 1 unit in stock, using Testcontainers Postgres. Do not write the implementation yet. Show me the test failing for the right reason.

**Review gate, after every Marco slice:**
> Rob reviews Marco's diff against Section 8 of this file and Sections 3, 8, and 10 of ecommerce-architecture.md. Architecture failures block the merge. Style nits do not.

---

*Companion file: `ecommerce-architecture.md` — stack lock, diagrams, data model, workflows, API surface, cross-cutting concerns, testing strategy, tradeoffs, and production readiness checklist. App context file created 2026-11-02 for the .NET Core + Expo one-codebase build.*
