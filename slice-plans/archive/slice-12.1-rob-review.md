# Slice 12.1 — Review thread

## Round 1 — Rob → Marco
Verdict: PASS · verify.sh: `VERIFY PASS (secrets hygiene build test drift expo-check tsc jest export)` — build 0 errors / 0 warnings, IntegrationTests 495, Architecture 25, jest 1205

The diff matches the plan: D1–D11, the three contracts, I1–I8 and U1–U2, and nothing extra. The Admin module reads Identity, Orders and Payments only through their `*.Contracts`, with no csproj change and no architecture test touched. Both mutations went red when I ran them myself.

### Checks against the brief

- **§3.4.** Each SQL query stays inside one module's tables. Orders reads `orders`, `order_status_history` and `return_requests`. Payments reads `refunds` and `payments`. Identity reads `users`. Admin puts the results together in memory (`CustomerQueryService`), so there is no cross-module `DbSet` or join. The endpoints are read-only, so there is no transaction and no external call. The controller only validates, calls the service and maps the result. No `var`, and the slice never reads the clock.
- **Support guard (D9).** You decide it in the controller from `User.IsInRole(IdentityRoles.Admin)`, and the service returns `null` when the caller is not Admin. I2 checks that the property is there and is `null`, and that every other field matches the Admin response.
- **Total-spent definition (D8).** `PaidTotal` sums orders that have a `Paid` history row. `ActorSucceededTotal` is `Succeeded AND actor_id IS NOT NULL`. I checked this against the dashboard. `PaymentWebhookService` emits `RefundSucceeded` only when `ActorId is not null`, and the saga refund carries `payload.ActorId`, so both use the same revenue rule. I3 covers it with pending, failed, automatic, never-paid and other-customer rows.
- **Wildcard escape.** `\`, `%` and `_` are escaped and `ILike` is called with the `\` escape. I5 checks a literal `_` and `%`.
- **Page bounds.** `page < 1`, `pageSize` outside 1–100 and `search` over 100 characters all return 400 (I5, I6). A page past the last one returns 200 with an empty list (I4).
- **Staff id → 404.** `FindCustomerAsync` filters on `Role == Customer`. I7 covers an unknown id, an Admin's id and a malformed id.
- **Bounded detail.** The detail makes a fixed number of queries: 1 to Identity, 5 to Orders and 4 to Payments, whatever the customer's order count. Each `recent*` list is `Take(10)`, and the counts are `COUNT`s. Only `All` (id and number per order) grows with the customer's order count, and the plan accepted that as a risk.

### Mutations (I ran them; each file restored from a scratch copy, `cmp` identical)
- MU1: controller passes `GetAsync(id, true, ct)` → I2 red: `Expected: Null / Actual: Object`, `Failed: 1, Passed: 9`. Restored, `cmp` identical.
- MU2: `PaymentStore.ListRefundsForOrdersAsync` without `&& r.ActorId != null` → I3 red: `Expected: "110.00" / Actual: "10.00"`, `Failed: 1, Passed: 9`. Restored, `cmp` identical.

### Changed tests
These four existing test files changed: Identity.Tests `Fakes.cs`, Notifications.Tests `Fakes.cs`, Orders.Tests `ReturnsAndRefundsTests.cs` and Payments.Tests `RefundTests.cs`. Each change only adds `NotSupportedException` stubs for the new port and contract members. I read the `-U0` diff: it adds lines only and touches no assertion. **Confirmed.**

### 1. Very large `page` overflows the offset — NOTE   (`Identity/Infrastructure/Repositories.cs:69`)
**What I saw:** `(page - 1) * pageSize` is `int` arithmetic. With `page=30000000&pageSize=100` it wraps to a negative number, Postgres rejects the negative OFFSET, and the client gets a 500, not a 400 or an empty page.
**Why it's not blocking:** Every paged list already uses this pattern (Orders `OrderStores.cs:57,114,171`, Catalog `ProductReader.cs:58`), and the §9 contract sets no upper limit on `page`. It is only a wrong status code: no data leaks and nothing is written. The fix belongs in one change across the whole API, not in this slice alone.
**Lesson:** compute paging offsets in `long`, or reject them before they reach SQL.
→ `backlog.md`.

**What was good:** You built I3 from every kind of row that could skew the total: pending, failed, automatic, never-paid and another customer's. That is why MU2 fails cleanly. I2 compares the whole Admin and Support responses, so any future revenue field that leaks to Support also fails the test.
