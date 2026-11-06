## 9. API surface (v1)

Base `/api/v1`. Errors are Problem Details (`application/problem+json`). Lists are paginated (`page`, `pageSize` ≤ 100) with a total count. Auth is Bearer, except public reads and the Stripe webhook (signature). Breaking changes go to `/api/v2`; v1 is never silently changed.

| Endpoint | Role | Notes |
|---|---|---|
| `POST /auth/register` · `login` · `refresh` · `logout` · `password-reset` · `password-reset/confirm` | Public / Customer | Refresh tokens rotate; reuse revokes the family |
| `GET /auth/me` | Any authenticated | Caller's own `{ id, email, displayName, role }` |
| `GET /products?search=&category=&sort=&page=&pageSize=` | Public | Active products only |
| `GET /products/{slug}` | Public | Variants, images, stock indicator. Inactive → 404 (admin preview via admin API) |
| `GET /categories` | Public | |
| `GET /cart` · `POST /cart/items` · `PUT /cart/items/{variantId}` · `DELETE /cart/items/{variantId}` | Customer | Server-validated prices. `PUT` sets the quantity; 0 removes. Cart contract below |
| `POST /cart/merge` | Customer | `Idempotency-Key` required. Guest lines in; merge result out |
| `GET /shipping-methods` | Customer | Configured flat rates (§7.4): `{ items: [{ code, name, price, estimatedDays }] }`, unpaginated (bounded config list) |
| `POST /checkout` | Customer | `Idempotency-Key` required. Inline address + method code. Returns order + `client_secret`. Checkout contract below |
| `POST /orders/{id}/retry-payment` | Owner | `Idempotency-Key` required. `PendingPayment` and reservation unexpired only (else 409). Same order, new intent (§8.2 rule 11) |
| `GET /orders` · `GET /orders/{id}` | Owner | History, detail, timeline, tracking. Orders contract below |
| `POST /orders/{id}/return-request` | Owner | `Delivered` only |
| `POST /webhooks/stripe` | Stripe | Signature verified, event-idempotent |
| `GET /admin/dashboard?period=` | Admin | Projected KPIs with as-of time |
| `GET/POST /admin/products` · `GET/PUT /admin/products/{id}` · `GET /admin/products/{id}/preview` · `POST /admin/products/{id}/images` · `POST /admin/products/{id}/publish` · `…/unpublish` | Admin | `PUT` carries `version` (optimistic, stale → 409). `images` upload waits for the bucket decision (§2); until then image keys go in the create/update body |
| `POST /admin/categories` | Admin | One level of nesting; duplicate slug → 409 |
| `GET /admin/inventory` · `POST /admin/inventory/adjust` | Admin | Adjust requires a reason; audited |
| `GET /admin/orders` · `GET /admin/orders/{id}` | Admin, Support | |
| `POST /admin/orders/{id}/process` · `ship` · `deliver` · `refund` · `status-override` | Admin | Audited. Override requires a reason. `ship` body `{ carrierCode, trackingNumber }`. `refund`: refund contract below |
| `GET /admin/returns` · `POST /admin/returns/{id}/approve` · `decline` | Admin | Reason required |
| `GET /admin/customers` · `GET /admin/customers/{id}` | Admin, Support | Read-only. Support sees no revenue KPIs |

**Catalog read contract (Slice 1a):**
- **Visible** = `is_active` and ≥ 1 active variant; otherwise list-excluded and detail 404 (also 404 for a malformed slug).
- `page` ≥ 1 (default 1), `pageSize` 1–100 (default 20); out of range → 400, never clamped. Past the last page → 200, empty `items`, real `totalCount`. Response `{ items, page, pageSize, totalCount, totalPages }`.
- `sort` = `relevance | price-asc | price-desc | newest`; default `relevance` with `search`, else `newest`; `relevance` without `search` → 400. Every sort ends in a unique tie-breaker (`id`). Product price = minimum **active** variant price.
- `search` trimmed, ≤ 100 chars, `websearch_to_tsquery('english')` over the weighted generated `search_vector` (name A, description B).
- `category` = slug; matches the category and its direct children; unknown slug → 200 empty.
- `GET /categories` → `{ items: [{ slug, name, parentSlug }] }`, unpaginated (bounded one-level set — exception to the pagination rule).
- Slugs: `^[a-z0-9]+(?:-[a-z0-9]+)*$`, ≤ 100 chars, unique, not case-folded.
- Money on the wire: `{ "amount": "24.50", "currency": "USD" }` — amount is a decimal **string**; clients format only. `stockStatus` = `Unknown | InStock | Low | Out`.

**Auth and admin contract (Slice 1b):**
- **Every endpoint requires authentication** (fallback policy) unless it is explicitly `[AllowAnonymous]`. A test pins the full anonymous list.
- Status codes: no or invalid token → 401, wrong role → 403. Both are Problem Details.
- **Unknown JSON request fields → 400** (strict binding; no mass assignment).
- Token responses are `{ accessToken, accessTokenExpiresAt, refreshToken, refreshTokenExpiresAt, user }` with `Cache-Control: no-store`.
- **Web transport:** with the request header `X-Auth-Transport: cookie`, the refresh token travels only in the `ecommerce_rt` cookie (`HttpOnly; Secure; SameSite=Strict; Path=/api/v1/auth`), and `refreshToken` in the body is `null`. Without the header, the server reads and writes only the body.
- **Errors that don't reveal accounts:** login failures return one generic 401, and `password-reset` always returns 202. Register returns 409 for an existing email (accepted, §13.2).
- Admin write money uses the same `{ amount: string, currency }` shape. The server stores exactly the decimal sent and computes nothing from client input.

**Cart contract (Slice 3):**
- `POST /cart/items` `{ variantId, quantity }` adds to the line; `PUT /cart/items/{variantId}` `{ quantity }` sets it (0–cap; 0 removes; a line not in the cart → 404, `PUT` never creates). Bounds, stock and rejection rules: §8.1. Codes: invalid → 400, over available → 409 (Problem Details with `available`), success → 200 with the cart. `DELETE` of an absent line → 204 (idempotent).
- Cart response `{ items: [{ variantId, productSlug, productName, variantName, imageKey, quantity, unitPrice, lineTotal, stockStatus, issues: [PriceChanged | ExceedsStock | Unavailable] }], subtotal }` — `unitPrice` is the current Catalog price; `subtotal` is display-only (checkout recomputes).
- `POST /cart/merge` `{ lines: [{ variantId, quantity }] }` (≤ 100 lines) → `{ cart, results: [{ variantId, outcome: Kept | Capped | Removed, quantity, reason }] }`. Same key → same response.

**Checkout contract (Slice 4):**
- Body `{ shippingAddress: { fullName, line1, line2?, city, region?, postalCode, country }, shippingMethodCode }`. Strings trimmed; `fullName`, `line1`, `city` 1–100 chars, `line2`/`region` ≤ 100, `postalCode` 1–20, `country` in `Shipping:Countries`. No price, total, tax, shipping amount or status field (strict binding → 400).
- Response `{ order: { id, orderNumber, status, items, subtotal, discountTotal, shippingTotal, taxTotal, grandTotal, shippingMethod: { code, name }, shippingAddress }, clientSecret }`. Out-of-stock or changed lines → 409 listing each `variantId` and why.
- `retry-payment` → `{ orderId, attempt, clientSecret }`; not `PendingPayment` or reservation expired → 409 (`reservation-expired` tells the client to check out again).
- The client confirms with the Stripe publishable key from config (`EXPO_PUBLIC_STRIPE_PUBLISHABLE_KEY`, placeholder — open decision §2) through `lib/platform` (§13.2: Stripe client SDKs).

**Orders contract (Slice 5):**
- `GET /orders?page=&pageSize=` — the caller's orders only, newest first (`created_at DESC, id`): `{ items: [{ id, orderNumber, createdAt, status, grandTotal, trackingUrl }], page, pageSize, totalCount, totalPages }`. `trackingUrl` is null until shipped.
- `GET /orders/{id}` — items (snapshots), totals, shipping address and method snapshot, `paymentStatus` (latest attempt), `timeline: [{ fromStatus, toStatus, at, reason? }]` from `order_status_history` ascending (internal actor IDs never exposed), `shipments: [{ carrierName, trackingNumber, trackingUrl, shippedAt, deliveredAt }]`, `refunds: [{ amount, status, createdAt }]`. Another customer's order → 403 (test 7).

**Refund contract (Slice 6):** `POST /admin/orders/{id}/refund` with `Idempotency-Key`, body `{ amount: { amount, currency }, reason }` (reason 1–500 chars; currency = the order's). → 202 `{ refundId, amount, status: Pending, refundedTotal, remaining }`. Over the ceiling or illegal state → 409; same key, different body → 409. Rules: §8.5.
