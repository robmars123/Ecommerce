## 3. Architecture principles

### 3.1 Modular monolith

One API, one database, one deployment, **hard module boundaries inside**. Each module is a vertical slice that owns its entities, services, tables, endpoints, and events. This buys most of the isolation of microservices without distributed transactions.

1. A module reads another module only through its **public contract** (interface or domain event).
2. A module owns its tables. No other module writes to them.
3. Cross-module side effects go through the **outbox** (§8.4).
4. Extraction test: a module must be movable as its two projects (`Ecommerce.<Module>` + `Ecommerce.<Module>.Contracts`, §6.2) + its tables + its contract. If it can't be, the boundary is broken.

### 3.2 SOLID, concretely

| | In this codebase |
|---|---|
| **S** | `InventoryReservationService` reserves and releases stock — nothing else. `OrderService` orchestrates; it does not own gateway code. |
| **O** | Pricing, shipping, and payments are strategies: `IPricingRule`, `IShippingRateProvider`, `IPaymentGateway`. New carrier = new class, not a new `if`. |
| **L** | Any `IPaymentGateway` (Stripe, test fake, future gateway) is substitutable without changing checkout. A fake that behaves differently from Stripe is a leaking abstraction. |
| **I** | Small interfaces: `IInventoryReader`, `IInventoryReserver`, `IOrderReader`. No god `IRepository<T>`. |
| **D** | Application depends on `IPaymentGateway`, `IEmailSender`, `IClock`, `IIdempotencyStore`. A module's `Infrastructure/` folder (or `Ecommerce.Persistence` for platform tables, the host for `IClock`) implements them. Domain depends only on `Ecommerce.SharedKernel` (§6.2). |

### 3.3 Production-ready means

- **Correct under concurrency before fast.** Oversell and double charge are bugs; slow search is tuning.
- **Idempotent wherever money or state moves.** Same request twice = one effect.
- **Observable.** Which order, user, step, and external call failed — answerable in under 5 minutes from logs + correlation ID.
- **Recoverable.** Crash, webhook retry, provider outage → the system converges by retry, expiry, or outbox, with no manual DB edits.
- **Secure by default.** §10.1.

### 3.4 Hard rules

The canonical list. Breaking any rule blocks a merge (quality bar §1). Rules 1–6 can never be waived for a release.

1. **Server owns money.** Prices, discounts, shipping, tax, and totals are calculated server-side. Write DTOs are explicit — no client-settable `grand_total`, `status`, or price.
2. **Idempotency.** One idempotency key → at most one order. One Stripe event ID → at most one transition and one outbox event.
3. **No oversell.** Lock inventory rows in ascending variant-ID order, reserve in one transaction, commit only on Paid. Available = on_hand − reserved, enforced by a DB check constraint.
4. **Postgres is truth.** Redis is cache only. Checkout never reads stock from cache. Losing Redis loses nothing.
5. **Webhook is truth for payment.** Signature verified, unsigned requests rejected. Frontend success is a hint.
6. **Side effects via outbox**, inserted in the same transaction as the state change. No external call (Stripe, email, SignalR) inside a database transaction.
7. **Thin controllers.** Validate, call an application service, map the response. No business logic.
8. **Module boundaries.** No cross-module `DbSet` and no cross-module SQL join in application code.
9. **No `DateTime.UtcNow`** in domain or application code. Inject `IClock`.
10. **No secrets** in git, code, logs, or chat. No card numbers, passwords, full JWTs, or Stripe secrets in logs. Secrets live in env vars / secret manager.
11. **Authorization per endpoint and per resource.** A customer reads only their own orders and cart. Hiding UI is not authorization.
12. **Tests ship with the slice.** Every public method that moves money or stock is tested in the same slice. Hard invariants are test-first.
13. **Audit.** Stock adjusts, status overrides, and refunds write `audit_log` with actor, before, after, and reason.
14. **Legal transitions only** (§7.3), enforced in the domain and tested.
15. **Consistency boundary** (§8.8). Checkout, payment, and inventory never read from an eventually consistent read model. No message broker in v1.
16. **No unplanned dependency.** A NuGet/npm package is added only if Rob's plan names it, its reason, and what it replaces. A new database or external service is a stack change (§2).
17. **Frontend is Expo one-codebase** (§2). Platform-specific code lives only in `lib/platform.ts`.
18. **Migrations are backward-compatible** (§10.5). Rollback = redeploy the previous image against the current schema.


