# Slice P2.50 — Marco report

Concerns: None
Bugs found: None
Bugs fixed: None
Bugs open: None
Tests changed: `frontend/apps/store/tests/order-fixtures.ts` — `makeOrderDetail` default `statusNote: null` (named by the plan; required because `OrderDetail.statusNote` is a required field and tsc must stay green). No existing test edited.

## Red output (test-first)
- U1-U4: `OrderStatusNotesTests.cs` — `error CS0103: The name 'OrderStatusNotes' does not exist in the current context` / `CS0246: OrderStatusNote could not be found` (types absent). Green after implementing: Orders.Tests 245 passed.
- I1-I3: `dotnet test Ecommerce.Api.IntegrationTests --filter P2_50` — `KeyNotFoundException: The given key was not present in the dictionary` (statusNote missing), Failed 3, Passed 1 (I4, 403 already carries no note: guard unchanged). Green after wiring: Orders integration filter 96 passed.
- S1, S2 red (text not rendered) before the UI change; green after (S3-S5 pass by design as negative/no-regression checks). Jest runs each file under 2 projects, so counts show doubled.

## Changed
- new `backend/src/Modules/Orders/Ecommerce.Orders/Application/OrderStatusNotes.cs` (records OrderAlert, OrderStatusNote; `For`)
- `backend/src/Modules/Orders/Ecommerce.Orders/Api/OrderResponses.cs` (`StatusNote` last param; mapping passes `OrderStatusNotes.For`)
- new `backend/tests/Ecommerce.Orders.Tests/Application/OrderStatusNotesTests.cs` (U1-U4; the Theory takes the status as a string and parses it, because `OrderStatus` is internal and xunit theories must be public)
- `backend/tests/Ecommerce.Api.IntegrationTests/Orders/OrdersReadApiTests.cs` (appended I1-I4)
- `frontend/apps/store/features/orders/types.ts`, `components/OrderDetailView.tsx` (pill note, `CancellationAlert`), new `OrderDetailView.status-note.test.tsx` (S1-S5)
- `frontend/apps/store/tests/order-fixtures.ts`, `docs/admin-dashboard-feature-tracker.md` (P2.50 Done), `AGENT_STATUS.md`

## verify.sh
VERIFY PASS (secrets hygiene format build test drift expo-check tsc jest export) — build 0 warnings; jest 260 suites / 1983 tests passed.

## Quality-bar §2
- [x] Plan's required tests written and passing (U1-U4, I1-I4, S1-S5); U1/I1 red first.
- [x] Full verify.sh green; admin integration tests unchanged and green.
- [x] Server owns the text; client never derives copy (S4).
- [x] Tokens only, no console.*, no new package, no migration.
- N/A: mutations (Low tier); money/stock/auth untouched.
