# Slice P2.65 — Marco report (AdminDashboard to Minimal API)

Concerns: None. N1 showed MVC returns 200 `today` for `?period=%20`, so C5 (whitespace to null) applies as planned. No pin disagreed with the plan.
Bugs found: None
Bugs fixed: None
Bugs open: None
Tests changed: none edited. Appended N1–N7 to `Projections/AdminDashboardApiTests.cs` (7 new tests, all existing tests unedited).

## Order followed
1. Baseline and N1–N7 on MVC: AdminDashboard, AdminInsights, AdminCustomers, EndpointInventory and Authorization tests: `Passed! Failed: 0, Passed: 67, Total: 67` (N1–N7 included, green on MVC before any conversion).
2. `QueryValues.cs` plus Customers switch, then conversion. Same suites: `Passed! Failed: 0, Passed: 67`. AdminCustomers I1–I13 green, unedited.

## Changed
- new `backend/src/Modules/Admin/Ecommerce.Admin/Api/AdminDashboardEndpoints.cs` (records moved unchanged, `Map`, `Get`, `RecentOrders`; one `GetAsync` and one `GetRecentAsync` call; no dates computed)
- new `.../Api/QueryValues.cs` (`ReadInt`, body of the Customers copy with the first-value line)
- `.../Api/AdminCustomersEndpoints.cs`: two call sites, private `ReadInt` deleted, `using System.Globalization` dropped
- `.../Api/AdminDashboardController.cs`: records and `AdminDashboardController` deleted, usings trimmed; `AdminProjectionsController` untouched
- `.../AdminModule.cs`: `AdminDashboardEndpoints.Map(app);`
- `backend/tests/Ecommerce.Api.IntegrationTests/Projections/AdminDashboardApiTests.cs`: N1–N7 appended
- Catalog's copy untouched. No Application/Infrastructure/Contracts/migration/package/doc change.

## Mutations (scratch copy `scratchpad/orig.cs`, `cmp` after each restore: identical)
- M1 `IsInRole` to `false`: RED, 8 failed, including `Dashboard_for_an_Admin_has_the_servers_KPIs_and_for_Support_has_kpis_null`, `Support_never_receives_the_previous_revenue_while_Admin_does`.
- M2 `IsInRole` to `true`: RED, 4 failed: `Support_never_receives_the_previous_revenue_while_Admin_does`, `Support_gets_the_same_low_stock_count_as_Admin_and_kpis_null`, `Dashboard_for_an_Admin_has…`, `Response_property_names_are_unchanged`.
- M3 policy `AdminOrSupport` to `Admin`: RED, 8 failed, including `Recent_orders_without_a_token_is_401_and_as_a_Customer_is_403_and_Support_may_read` and `EndpointInventoryTests.T1_every_endpoint_has_the_expected_methods_auth_and_cors`.
- M4 `FirstOrDefault()` to `string.Join(",", period ?? [])`: RED, 1 failed: `Dashboard_period_blank_whitespace_and_repeated_keys_follow_MVC`.

## verify.sh
```
PASS build 12s 0 Error(s)|0 Warning(s)
PASS test 113s (all projects)
PASS drift · expo-check · tsc
PASS jest Test Suites: 272 passed | Tests: 2111 passed
PASS export
VERIFY PASS (secrets hygiene format build test drift expo-check tsc jest export)
```

## Quality-bar §2
- [x] Tests for new behaviour (N1–N7) and every guard (M1–M4 red).
- [x] Hard rules §3.4: server computes money (mapping verbatim), no `DateTime`, thin handlers, no cross-module DbSet.
- [x] No `var`, no anonymous types, plain code.
- [x] Full `verify.sh` green.
- [x] No secrets, no new packages, no existing test edited.
- N/A: migrations, frontend, docs (pure transport conversion).
