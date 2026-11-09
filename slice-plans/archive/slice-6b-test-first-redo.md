# Slice 6b test-first redo (AdminDashboardApiTests role/400)
Method: stub only the guard, run the test (Release, real Postgres), restore from scratch copy, `cmp` OK, green re-run (31/31 Projections tests). C = Admin/Api/AdminDashboardController.cs.

| Test | Guard (file:line) | Red line | Green |
|---|---|---|---|
| Dashboard 401 (l.17) | C:50 `[Authorize(AdminOrSupport)]` -> `[AllowAnonymous]` | Expected Unauthorized, Actual OK | pass |
| Recent orders 401 (l.116) | C:50 same attribute (shared) | Expected Unauthorized, Actual OK | pass |
| Replay 401 (l.164) | C:110 `[Authorize(Admin)]` -> `[AllowAnonymous]` | Expected Unauthorized, Actual Accepted | pass |
| Unknown period 400 (l.55) | Admin/Application/DashboardKpis.cs:12 `_ => null` -> `Today` | Expected BadRequest, Actual OK (3 cases) | pass |
| Bad page 400 (l.151) | C:87 `page is < 1` stubbed | Expected BadRequest, Actual InternalServerError (page=0 case) | pass |
| Bad pageSize 400 (l.151) | C:91 `pageSize` range stubbed | Expected BadRequest, Actual OK (2 cases) | pass |
| Replay unknown 404 (l.175) | C:119 NotFound arm -> `Accepted()` | Expected NotFound, Actual Accepted | pass |
| Replay held 409 (l.184) | Persistence/Projections/ProjectionServices.cs:34 try-lock always true | Expected Conflict, Actual Accepted | pass |

All eight went red when their guard was stubbed; every file restored and `cmp` identical. No test changed.
