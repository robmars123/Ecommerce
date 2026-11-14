# Slice P2.72 — Review thread

## Round 1 — Rob → Marco
Verdict: PASS · verify.sh: `VERIFY PASS (secrets hygiene format build test drift expo-check tsc jest export)` — build 0 warnings, Api.IntegrationTests 635/635, jest 272 suites / 2111 tests.

A clean, faithful port. `CartEndpoints.cs` matches the deleted controller line for line on ownership, validation order, outcome mapping and messages. It adds nothing, and nothing outside the five planned files changed.

### What I checked (high risk, quality-bar §3)

- **Boundaries:** `CartEndpoints` is `internal static`; `CartModule.MapCartEndpoints` is the only public addition. `git diff --stat` shows nothing under `Application/`, `Infrastructure/`, `Domain/`, Persistence, `*.Contracts`, migrations or packages. ArchitectureTests are green.
- **Thin handler / one service call:** each handler makes exactly one call (`GetAsync`, `AddAsync`, `SetAsync`, `RemoveAsync`, `MergeAsync`). No `DbContext`, transaction, reader, or loop beyond the request-to-`MergeLine` projection, which is the same `Select` the controller had.
- **N+1 / round-trips / over-fetching:** none added. Query counts are the same as plan §1 because the services are untouched. The existing double read of the written variant is already in the backlog (plan NOTE). It is not this slice's work.
- **Money:** `CartEndpoints.cs` has no `decimal`, no arithmetic and no formatting. Money reaches the wire only through `ToResponse()` and `MoneyResponse.From` (unchanged). CC7 checks that the amounts are strings and pins the empty-cart body raw.
- **Ownership:** every handler passes `UserId(user)` from `sub`. No route or body carries an owner. `{variantId}` is a catalog id. CC2 proves B cannot change A's line: PUT gives 404, DELETE is a no-op, and A keeps 2.
- **Problem bodies:** 404 and 409 are plain `ProblemDetails` with no `errors`. The `available` extension is kept. A service `Invalid` result becomes a `ValidationProblem` keyed by `result.Field`. The merge key is checked before the body. `ToErrors` is the same as Inventory's.
- **D4/D15:** the group is built on `app` (`api/v1`), with no `MapGet("")`, no `.Accepts` and no CORS code. The inventory rows stay `auth | cart`.
- **Tests:** no existing test was edited (`git status` shows only the new `CartContractTests.cs` under tests). CC1–CC7 cover every row of plan §5 and assert that the cart is unchanged after each rejected write. Pinning MVC's `'Variant Id' must not be empty.` as it is was the right call (backlog item already filed).
- **Readability:** the handlers are short and named, the switch expression is easy to scan, and there is no `var` and no anonymous type. Nothing clever.

### Mutations I re-ran (break → red → `cp` from scratch → `cmp` equal)

| # | Break | Red (filter `.Cart.`, 42 tests) |
|---|---|---|
| M1 ownership | `UserId` returns a fixed Guid | Failed 3: `T21_one_users_writes_never_appear…`, `CC2_…`, `T22_the_same_key_…another_user_is_409…` |
| M3 idempotency | key check made unreachable | Failed 2: `CC5_…`, `T22_a_missing_or_overlong_key…` |
| M4 idempotency | Conflict arm returns 200 with an empty `MergeResponse` | Failed 2: `T22_the_same_key_…`, `CC5_…` |
| M5 stock | `Insufficient` without `available` | Failed 2: `CC1_…`, `T21_a_quantity_above_available…` |
| M7 money | `decimal? UnitPrice` on `AddCartItemRequest` | Failed 1: `T21_a_body_carrying_a_price_is_400…` |

Each one matches your report. All restored, and `cmp` printed nothing.

### Findings

None blocking. No new NOTEs. The three plan NOTEs (view re-read, `VariantId` `WithMessage`, the Set quantity message) are already in `backlog.md`.

**What was good:** You captured every message from the real MVC response before converting, and you fixed your own wrong guess in CC3 against MVC, not against the new code. That is exactly how a behaviour pin should be made. CC6 also proves the merge key is still free after an unreadable body, an edge case that is easy to miss.
