# Slice P2.74 — Review thread

## Round 1 — Rob → Marco
Verdict: PASS · verify.sh: `VERIFY PASS (secrets hygiene format build test drift expo-check tsc jest export)`; build 0 Warning(s) 0 Error(s); jest 272 suites / 2111 tests

The diff is a straight port of the controller. There is one route and one service call, and the switch matches the old controller arm for arm. Nothing under `Application/`, `Infrastructure/` or `Domain/` changed, and the new pins went green on MVC before you converted. I found no bugs and no blocking issues.

### Checklist (plan §7), checked against the code

- **Ownership:** `userId` comes only from `sub` (`ReturnsEndpoints.cs:28`). The route `id` is used only as the order id, and nothing in the body is treated as the user. The service still makes the owner check (`ReturnService.cs:53`).
- **Check order:** sub → validator → service, the same as MVC. RR2 pins that a bad body on someone else's order is 400, not 403.
- **Arms:** titles, `type` values and statuses match the deleted controller. `Invalid` is keyed by `i.Field`, and `_` maps to 404.
- **Queries:** the request runs one `RequestAsync`. The handler has no `DbContext`, store, `IClock` or loop. Queries are unchanged: 2 reads and 1 insert in one `SaveChangesAsync` on the happy path, with no N+1 because the line check runs in memory. The over-fetch in `OrderStores.GetAsync` and the tracked `GetOpenAsync` were already in the backlog (plan NOTEs, lines 151–152). They predate this slice and are out of scope.
- **Surface:** no group, no `.Accepts`, no CORS code. `ReturnCreatedResponse` was moved unchanged. Only the dead `ReturnsController` and the record left `ReturnsController.cs`. The usings that remain are still used by `AdminReturnsController` (`Authorization`, `Domain.ReturnStatus`, `FluentValidation`).
- **Tests:** `git diff --stat backend/tests` is empty, and the only test file added is `ReturnRequestContractTests.cs` (9 cases). The anonymous request bodies match the existing convention in `ReturnTests.cs:10`. `var` is not used.
- **Edge cases:** a literal `null` body hits the non-nullable `[FromBody]` and returns 400 before the handler runs (RR7). `{}` reaches the validator, so the `!` dereferences on `Items`, `VariantId`, `Quantity` and `Reason` only run after validation passes (RR4, M6).

### Mutations (I re-ran them; each broken → red → restored from scratch copy → `cmp` OK)

| # | Break | Red |
|---|---|---|
| M1 ownership | `ReturnService.cs:53` `order.UserId != userId` → `false` | `Failed: 2, Passed: 19, Total: 21` — RR1, `ReturnTests.T7_another_customers_order_is_403…` |
| M2 ownership mapping | `Forbidden` arm → 404 `Order not found` | `Failed: 2, Passed: 19, Total: 21` — RR1, T7 |
| M5 auth | drop `.RequireAuthorization()` | `Failed: 1, Passed: 9, Total: 10` — `EndpointInventoryTests.T1_every_endpoint_has_the_expected_methods_auth_and_cors` |
| M6 validation | `if (!validation.IsValid)` → `if (false)` | `Failed: 2, Passed: 19, Total: 21` — RR4, RR2 |

On M5, the anonymous 401 in RR1 stays green because the fallback policy still applies. The plan predicted this, and the inventory test is the proof. On M6, `ReturnTests.Bad_items_quantities_or_reasons_are_400` did not go red, although plan §6 said it would. RR2 and RR4 catch it, so the guard is covered. My plan's prediction was wrong; your tests are fine.

### 1. `ToErrors` now has two copies in Orders — NOTE   (`ReturnsEndpoints.cs:51`, `AdminOrderReadsEndpoints.cs`)
**What I saw:** you copied the 3-line `ToErrors` helper from Cart, as R5 told you to. Orders now holds two private copies of it, and the repo holds six.
**Why it matters:** it causes no bug today. But if someone changes how keys are lowercased in one copy, that endpoint's `errors` keys will drift from the others.
**What I want:** nothing in this slice. In P2.75, when `ControllerValidation` is deleted, keep one `internal` Orders helper and call it from all three Orders endpoint files. Added to the backlog.
**Lesson:** a copy the plan allows is fine once; by the third copy inside one module it is a helper.

**What was good:** you captured every title, message and key from the real MVC responses before converting. That is why RR3 can pin the custom 409 `type` values exactly, and they are the easiest thing to lose with `TypedResults.Problem`. RR2 also pins the check order, which a later "tidy-up" could otherwise reverse without anyone noticing.
