# Slice P2.10 Part A — Marco report (P2.10, P2.23, P2.22)

Concerns: None. Minor note for Rob: the badge link's accessibility label is built as `"{badge.text} orders"` (gives "4 paid orders", as J5 requires) rather than a separate field, because the plan fixes `NavBadge = { text; href? }`.
Bugs found: None
Bugs fixed: None
Bugs open: None

## Test-first red (quoted)
- Unit (U1-U4): `dotnet test Ecommerce.Admin.Tests` failed to compile before the code: `error CS0117: 'KpiCalculator' does not contain a definition for 'ChangePercent'` and `CS1061: 'DashboardPeriod' does not contain a definition for 'PreviousFrom'`.
- Integration: before the backend change `AdminDashboardApiTests` ended `Failed! - Failed: 6, Passed: 23, Total: 29` (previous revenue x3 incl. security I2 and window I3, refund-netting I4, delivered I5/I6, I1). Honest note: three of those six first failed on a stale customer token after pinning the clock (a test bug I fixed by signing the customer in again); their red for the real reason is shown by M1 and M3 below. After the fix: `Passed! - Failed: 0, Passed: 29`.

## Mutations (copy to scratchpad, break, red, restore, `cmp`)
- **M1** `PreviousTo` -> `From(now)`: U4 red (`Expected: 2026-11-11T14:05:00Z  Actual: 2026-11-12T00:00:00Z`, all 3 periods); I3 red (`Expected: "15.00" Actual: "30.00"`). Restored, `cmp` OK.
- **M2a** `Math.Abs(previous)` -> `previous`: U3 red (`Expected: 50.0 Actual: -50.0`). **M2b** `previous == 0m` guard removed: U2 red x2 (`System.DivideByZeroException`). Restored, `cmp` OK.
- **M3** controller `User.IsInRole(IdentityRoles.Admin)` -> `true`: I2 red (`Expected: Null  Actual: Object`, Support got kpis). Restored, `cmp` OK.

## Existing tests changed (no assertion removed or loosened)
- `kpi-cards.test.ts` K1: expected cards now include `change` (Revenue) and `detail` (Orders); K2 literal gains `previousRevenue`/`revenueChangePercent`, call sites gain the `outcomes` argument (K1-K5).
- `badges.test.ts` B1-B3: value shape string -> `{ text }`; fixtures set `ordersByStatus: []` because the default `Paid: 4` now adds an Orders badge.
- `AdminShell.test.tsx` B4-B6: `badges` value shape string -> `{ text }`; type import `NavBadges`.
- `DashboardView.test.tsx`: first test also asserts the pill, previous-period text and outcomes line; D2 fixture gains the two new kpi fields; Support test (J6) also asserts no `previous period` / `%`.
- Fixture `admin-dashboard-fixtures.ts`: defaults `previousRevenue 1000.00`, `revenueChangePercent 23.5`, `deliveredCount 0`.
- Three `IOrderReader`/port fakes gain `CountDeliveredAsync => throw NotSupportedException` (compile-only): `Ecommerce.Admin.Tests/CustomerQueryServiceTests.cs`, `Ecommerce.Notifications.Tests/Application/Fakes.cs`, `Ecommerce.Orders.Tests/Application/ReturnsAndRefundsTests.cs`.
- Unchanged and green (auth/validation coverage of the changed endpoint): `Dashboard_without_a_token_is_401_and_as_a_Customer_is_403`, `Dashboard_with_an_unknown_period_is_400`.

## New tests
U1-U4 in `KpiTests.cs`; I1-I6 in `AdminDashboardApiTests.cs`; J1-J2 `kpi-cards.test.ts`; J3/J6 `DashboardView.test.tsx`; J4 `badges.test.ts`; J5 x3 `AdminShell.test.tsx`.

## Changed (slice P2.10 Part A)
Backend: `Orders.Contracts/IOrderReader.cs`, `Orders/Application/OrderPorts.cs`, `OrderReader.cs`, `Infrastructure/OrderStores.cs` (private `CountEnteredAsync`), `Admin/Application/DashboardKpis.cs`, `DashboardQueryService.cs`, `Admin/Api/AdminDashboardController.cs`; tests as above plus `AdminDashboardApiTests.cs`.
Frontend: `admin-dashboard/types.ts`, `kpi-cards.ts`, `components/DashboardView.tsx`, `admin-shell/badges.ts`, `components/AdminShell.tsx`, tests/fixture as above. No migration, package or endpoint added. Prettier applied to the two TSX files (first verify run failed `format`; fixed).

## verify.sh
`VERIFY PASS (secrets hygiene format build test drift expo-check tsc jest export)` - build 0 warnings; jest 1734 passed (220 suites); drift clean; tsc clean.

## Quality-bar §2
- [x] Tests for every new money/auth rule (U1-U4, I1-I6, J1-J6), mutations red then restored.
- [x] Server owns money: percent and previous revenue computed server-side; client only formats.
- [x] Support guard inherited (kpis null), tested I2/J6.
- [x] Controllers thin (mapping only); no cross-module DbSet; `IClock` only.
- [x] verify.sh full pass.
- [x] No secrets, no git writes, no new packages.
- N/A: migrations (none), Nathaniel-drives files (none).
