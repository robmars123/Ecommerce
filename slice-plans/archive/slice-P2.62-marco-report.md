# Slice P2.62 — Marco report

Concerns: None
Bugs found: scripts/verify.sh hygiene — proof P: with the controller deleted and `var probe = 1;` appended to StripePaymentGatewayTests.cs, the step printed `awk: ... fatal: cannot open file ...AdminSettingsController.cs` AND `...StripePaymentGatewayTests.cs:122: var probe = 1;` and still `PASS hygiene` / `VERIFY PASS (hygiene)`.
Bugs fixed: S6 (skip missing paths, `if … fi` loop) + S7 (fail closed on scan error, print hits). After: `FAIL hygiene ... StripePaymentGatewayTests.cs:122: var probe = 1; | C# var found (explicit types only)`, no "cannot open". Restored from scratch copy, `cmp` identical, hygiene `PASS ... no matches`.
Bugs open: None
Tests changed: none edited or deleted. New: `backend/tests/Ecommerce.Api.IntegrationTests/Admin/AdminSettingsContractTests.cs` (T1, T2), written first, green on MVC (`Passed: 2`), unchanged afterwards.

Changed (slice P2.62):
- deleted `backend/src/Modules/Admin/Ecommerce.Admin/Api/AdminSettingsController.cs`
- new `.../Ecommerce.Admin/Api/AdminSettingsEndpoints.cs` (one route, `Admin` policy, sync handler, `SettingsResponse` moved)
- `.../Ecommerce.Admin/AdminModule.cs` (`MapAdminEndpoints`)
- `backend/src/Ecommerce.Api/Composition/ModuleComposition.cs` (chain)
- `backend/src/Ecommerce.Api/Program.cs` (`MapGroup("")` + `AddCorsByRoutePrefix` on module group and MVC)
- `scripts/verify.sh` (S6, S7 only)
- `slice-plans/backlog.md` (lines 104, 107 marked Resolved P2.62)
- new test file above

Step 4 run (inventory, II10/II11/II16, Cors, AdminInsights, T1, T2): `Passed: 32, Failed: 0`. No RawText change beyond leading `/`; inventory test unedited and green.

Mutations (each restored from scratch copy, `cmp` identical):
- M1 policy -> AdminOrSupport: red `EndpointInventoryTests.T1_every_endpoint...`, `AdminSettingsContractTests.Settings_401_and_Support_403_are_problem_json`, `AdminInsightsApiTests.Settings_without_a_token_is_401_as_a_Customer_is_403_and_as_Support_is_403` (Failed: 3)
- M2 RequireAuthorization removed: red inventory T1, Settings_401_and_Support_403, II11, AdminInsights settings (Failed: 4)
- M3 `AddCorsByRoutePrefix(moduleEndpoints)` removed: red inventory T1, `Settings_cors_is_credentialed_for_the_configured_origin_only` (Failed: 2)
- M4 `.TrimStart('/')` removed: red inventory T1, Settings_cors_is_credentialed (Failed: 2)

verify.sh: VERIFY PASS (secrets hygiene format build test drift expo-check tsc jest export) — build 0 warnings, jest 2111 passed.

Quality-bar §2: all boxes met. Validation-400 N/A (endpoint takes no input); no migration, package or doc change; no commit, tracker row not marked.
Note: Program.cs/AdminModule.cs had mixed LF/CRLF after my edits; git only warns (normalised on touch).

## Round 2
Fixed review item 1 (`scripts/verify.sh:29-31`): `cs_var_hits` is now one pipeline stage per line, `printf '%s\n'` on a single line, no stray spaces. Nothing else changed. `sed -n 28,31p | cat -A` shows `printf '%s\n' "$f"` intact.
Proof P: appended `var probe = 1;` to `StripePaymentGatewayTests.cs` -> hygiene FAIL listing `...StripePaymentGatewayTests.cs:122`, no "cannot open"; restored from scratch copy, `cmp` identical; `verify.sh hygiene` PASS.
verify.sh changed: `VERIFY PASS (secrets hygiene build test)` (build 0 warnings, tests 0 failed).
Concerns: None. Bugs open: None. Not committed.
