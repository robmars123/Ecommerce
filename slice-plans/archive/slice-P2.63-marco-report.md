# Slice P2.63 — Marco report

Concerns: verify.sh ends `VERIFY FAIL: jest` (twice): one test, `tests/admin-phone-width.test.tsx` PW1 (one ROUTES row), "Exceeded timeout of 5000 ms". Frontend untouched by this slice; the file passes alone (22/22). Looks like a load flake in the 5 s per-test timeout under the full parallel run. I did not edit it. Rob/Nathaniel to decide (raise timeout or rerun).
S3: I11 showed MVC uses the first `q` value, so `string[]? q` + `FirstOrDefault()` as planned (comment in code says why).
Bugs found: None
Bugs fixed: None
Bugs open: None (jest timeout above is not from this change)
Tests changed: none edited. Added I11, I12 to `AdminSearchApiTests.cs` (new only).

Changed:
- new `backend/src/Modules/Admin/Ecommerce.Admin/Api/AdminSearchEndpoints.cs` (records unchanged, one `Get` handler)
- deleted `.../Api/AdminSearchController.cs`
- `.../AdminModule.cs`: `AdminSearchEndpoints.Map(app);`
- `backend/tests/Ecommerce.Api.IntegrationTests/Admin/AdminSearchApiTests.cs`: +I11, +I12
Nothing under Application/, Orders/, Catalog/.

## Step 1 (pins green on MVC, controller still present)
AdminSearchApiTests + EndpointInventoryTests + AuthorizationTests with I11/I12: `Passed! - Failed: 0, Passed: 23, Total: 23`.
## After conversion (same filter, tests unedited)
`Passed! - Failed: 0, Passed: 23, Total: 23`.

## Mutations (each restored from scratch copy, `cmp` identical, RESTORED printed)
- M1 drop RequireAuthorization: `Failed: 3` — EndpointInventoryTests.T1, AuthorizationTests.II11, AdminSearchApiTests.I2 (anonymous gets 200 not 401/403).
- M2 policy -> Admin: `Failed: 3` — T1, I6, I2 (Support gets 403).
- M3 role flag -> true: `Failed: 1` — I6 (Support sees products/SKUs).
- M4 `string.Join(",", q ?? [])`: `Failed: 1` — I11.

## verify.sh
secrets, hygiene, build, test (backend), drift, expo-check, tsc, export all PASS.
jest: `Tests: 1 failed, 2110 passed, 2111 total` (the PW1 timeout above) -> `VERIFY FAIL: jest` (both runs).

## Quality-bar §2
- Tests for new behaviour: [x] I11, I12 plus existing I1-I10, II11, inventory row.
- Mutations run and red: [x]
- No existing test edited/weakened: [x]
- verify.sh fully green: [ ] only the jest timeout flake, frontend untouched.
- No secrets, no new packages, no migration: [x]
- No `var`, controller deleted, no dead usings: [x]

## Round 2

Fixed Rob finding 1 (PW1 timeout). `frontend/apps/store/tests/admin-phone-width.test.tsx`: `renderRouter('./app')` replaced by `renderRouterWithQuery(ADMIN_ROUTES, ...)`. ADMIN_ROUTES is at module scope: `admin/_layout` plus the 11 screens, no `_layout` key. Only imports, the map and the render line changed. Assertions, ROUTES, `mockAdminApi` and the 375 px mock are unchanged. No timeout or retry added. No extra provider was needed.
- PW-M1 (AdminShell.tsx:194 `width >= sidebarBreakpoint` -> `true`): `Tests: 22 failed, 22 total` (11 rows x 2 jest projects). Restored from scratch copy, `cmp` RESTORED.
- Full jest x2: `Tests: 2111 passed, 2111 total` both runs.
- verify.sh: `VERIFY PASS (secrets hygiene format build test drift expo-check tsc jest export)`.
