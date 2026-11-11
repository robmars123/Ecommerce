## 12. Working with Claude Code

### 12.1 Ownership

| Owner | Work |
|---|---|
| Nathaniel | Owns the stack lock (§2) and scope. Reads each final report and Rob's review. Commits |
| Rob (architect agent) | Slice plans, plan-mode challenges, boundary and invariant reviews, keeps this file current |
| Marco (developer agent) | Implements one planned slice and fixes review findings |
| Nobody, without a test first | Checkout, reservation, webhook handling, refunds, projectors |

Session workflow, prompts, and deny rules: `CLAUDE.md`. Gates: `docs/quality-bar.md`.

### 12.2 Process rules

1. **Autonomous loop, no approvals** (`CLAUDE.md`): Rob plans → Marco builds → Rob reviews → fix (at most 3 rounds) → final report → agents AVAILABLE.
2. One slice per request, in §12.3 order. Every slice starts with a Rob plan; Orders, Inventory, Payments, and 6b get the full plan-mode challenge.
3. **No agent commits.** Changes stay in the working tree for Nathaniel.
4. A slice reports PASS only on a real PASS. Otherwise it reports `BLOCKED` with reasons.

### 12.3 Slice plan

| Slice | Scope | Done when |
|---|---|---|
| 0 Foundation | Solution + Expo scaffold (screens stubbed), DI, Postgres + migrations, health, Serilog + correlation ID, CI | `dotnet test` green; `/health/ready` 200 with Postgres up; web export builds |
| 1 Identity + Catalog | Auth, roles, products, variants, categories, list/detail/search (full-text), admin create. **Split into 1a → 1b** (below) | Public browse/search/paginate; admin creates a product (no stock) |
| 1a Catalog browse | Catalog module (products, variants, categories, image metadata), `GET /products`, `/products/{slug}`, `/categories`, Postgres full-text search, stock indicator via `IInventoryReader` stub (`Unknown`), Development-only idempotent seeder; Expo Home (search + categories), `/products`, `/products/[slug]` on TanStack Query | Public browse/search/filter/sort/paginate end to end; inactive invisible by direct link; F2 (correlation ID + ProblemDetails part) |
| 1b Identity + admin create | Auth (register/login/refresh rotation/logout/reset), roles, admin product create/edit/publish, admin preview, bearer in `api-client` | Admin creates a product (no stock); F2 (bearer part), F7 |
| 2 Inventory | Stock, reservation, release, expiry worker, check constraint, audited adjust, real `IInventoryReader` replacing `UnknownInventoryReader` (feature 2 stock indicator) | Tests 1, 10, 20; `Inventory:LowStockThreshold` (§7.4) in config; product list and detail show `InStock` / `Low` / `Out` on web (no `Unknown`); the stub is deleted |
| 3 Cart | User cart, guest device cart (Zustand `persist` + AsyncStorage, §13.2), edit quantity (`PUT /cart/items/{variantId}`), remove, idempotent merge, validation snapshot; `/cart` screen; add-to-cart on detail | Feature 3 end to end: guest device cart survives restart, server cart, merge on login with result shown, edit quantity, remove. Tests 21, 22; F3, F9. Merge never drops silently |
| 4 Checkout + Payments | Idempotent checkout, inline address, `GET /shipping-methods` + flat-rate shipping and tax (§7.4), Stripe intent, Stripe client SDKs behind `lib/platform` (§13.2), signed webhook, Paid + commit, outbox insert, retry payment, late-success refund; `/checkout` screen | Feature 4 (address, shipping method, server total, Stripe payment on web and in Expo Go) and feature 5 (webhook-confirmed, retry on the same order). Tests 2, 3, 4, 5, 6, 8, 19, 23, 24, 25; F4, F5, F6, F10, F11 |
| 5 Orders + Fulfillment | `GET /orders`, `GET /orders/{id}` (history, detail, timeline, tracking), admin process/ship (carrier + tracking)/deliver, status machine, overrides; `/orders`, `/orders/[id]` screens | Feature 6: history, detail, status timeline, tracking number + link, on web. Tests 7, 29, 30; F12 |
| 6 Notifications + Returns | Outbox consumers (email, SignalR), return requests, admin refunds (`POST /admin/orders/{id}/refund`, `RefundRequested` consumer, refund webhook) | Feature 5 admin refunds: ceiling, idempotency, partial → `PartiallyRefunded`, full → `Refunded`/`Returned`, audited. Tests 9, 26, 27, 28; F8, F13; no duplicate email on retry |
| 6b Eventual consistency | Dashboard + search projectors, checkpoints, gaps, replay, fulfillment saga | Tests 11–18 |
| 7 Hardening | Rate limits, caching (§10.2), slow-query pass, staging deploy | §14 green; staging end-to-end works |
| P2 Admin UI (Phase 2) | Rows P2.1–P2.47 in `docs/admin-dashboard-feature-tracker.md`: admin shell and theme, dashboard to the target screenshot, existing admin screens restyled into the shell, admin search, dashboard export, revenue trend, previous-period revenue, Analytics and Settings screens (app context §4.14). Built as gap-fill row groups | Every P2 row Done; Support 403 on Admin-only P2 endpoints and no revenue rendered; existing admin tests unchanged; phone width and both themes pass |

**Do not skip to Slice 4.** Slices 0–3 make it testable.

**Features 1–6 gate (app context §3):** a slice's Rob review BLOCKS if any feature or test named in its Done-when row is missing. Features 1–2 (browse, detail) are Slice 1a, plus the real stock indicator in Slice 2.

**Carry-ins from Slice 1b** (`slice-plans/slice-1b-plan.md`):
- **Slice 2:** the admin bootstrap writes `audit_log` (done in Slice 2); publish does **not** require a stock row (§13.2); the "Out of Stock" admin filter moves with the `/admin/inventory` screen, which no row schedules yet (Nathaniel).
- **Slice 3:** `IUserReader` / `ICurrentUser` contracts: not added — Cart reads the caller from the `sub` claim like Orders, and no module needs another user's data yet (Slice 3 plan D-row).
- **Slice 6:** password-reset delivery moves to the outbox (`PasswordResetRequested`, with the consumer minting the token). A real email provider must never be wired directly into the 1b `IPasswordResetLinkSender` seam.
- **Slice 7:** rate limits on register and reset too; retention cleanup for consumed and expired refresh and reset tokens; measure login p95 under load.
- **Domain decision:** the web origin and API must be the same site for the `SameSite=Strict` refresh cookie (§13.2).

**Slice 1 split (Rob, 2026-11-03, owner-requested):** 1a → 1b → 2; 1b must PASS before Slice 2. Browse is entirely public (§9), so it has no auth dependency; auth (rotation, reuse detection, lockout) deserves its own review. **Deferred within v1:** "best sellers" (on the product list, the first screen since Slice 5) and detail "related products" (app context §4.1.1, §4.2.4) need Paid order data — scheduled for Slice 6b, from the projected read model (§13.2). Plan: `slice-plans/archive/slice-1a-plan.md`.


