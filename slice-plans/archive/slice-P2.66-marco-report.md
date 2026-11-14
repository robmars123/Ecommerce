# Slice P2.66 — AdminInsights to Minimal API (Marco report)

Concerns: None
Bugs found: None
Bugs fixed: None
Bugs open: None
Tests changed: none edited. Appended P1-P5 (5 facts) to `AdminInsightsApiTests.cs` under the P2.66 header; existing tests untouched.

## Flow
1. Pins P1-P5 written on MVC. First run: 2 red because of my pin code (admin token minted before `PinNoon` moved the clock, so it expired: 401). Fixed the pins (mint token after `PinNoon`), not the code. Then `AdminInsightsApiTests` + `EndpointInventoryTests`: **Passed 16/16 on MVC**.
2. Converted: new `AdminInsightsEndpoints.cs` (own group from `app`, policy `Admin`; `Trend`, `Export`, `ReadPeriod`, `InvalidPeriod`), deleted `AdminInsightsController.cs`, one line in `AdminModule.MapAdminEndpoints`. Whitespace period = 200 `today` on MVC (P1), so `ReadPeriod` maps blank to null as planned.
3. Same filter after conversion: **Passed 16/16**, existing tests unedited.

## Mutations (restored from scratch copy, `cmp` identical each time)
- M1 group policy -> `AdminOrSupport`: Failed 4/16: `T1_every_endpoint_has_the_expected_methods_auth_and_cors`, `Export_for_Support_is_403_with_no_amount_and_no_csv`, `Trend_without_a_token_is_401_as_a_Customer_is_403_and_as_Support_is_403_with_no_amount`, `Export_without_a_token_is_401_as_a_Customer_is_403_and_as_Support_is_403`.
- M2 `period?.FirstOrDefault()` -> `string.Join(",", period ?? [])`: Failed 1/16: `Period_blank_whitespace_and_repeated_keys_follow_MVC_on_trend_and_export` (P1).
- M3 content type -> `"text/csv"`: Failed 1/16: `Export_headers_are_exact_and_the_body_has_no_BOM` (P3).

## Changed
- `backend/src/Modules/Admin/Ecommerce.Admin/Api/AdminInsightsEndpoints.cs` (new)
- `backend/src/Modules/Admin/Ecommerce.Admin/Api/AdminInsightsController.cs` (deleted)
- `backend/src/Modules/Admin/Ecommerce.Admin/AdminModule.cs` (+1 line)
- `backend/tests/Ecommerce.Api.IntegrationTests/Projections/AdminInsightsApiTests.cs` (P1-P5 appended)
Not committed; tracker not marked.

## verify.sh
`VERIFY PASS (secrets hygiene format build test drift expo-check tsc jest export)` — build 0 warnings; jest 272 suites / 2111 tests passed.

## Quality-bar §2
- [x] Tests for the slice's behaviour (P1-P5 plus existing) pass
- [x] verify.sh green
- [x] No secrets, no new packages, no migration (drift clean)
- [x] Rules: thin handlers, one service call each, `IClock` only for the filename, no cross-module access
- [x] Mutations M1-M3 red then restored
- N/A: frontend, docs, tracker (not requested)
