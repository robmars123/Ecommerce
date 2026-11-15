Concerns: None
Bugs found: None
Bugs fixed: None
Bugs open: None
Tests changed: none edited. Appended P1 `Replay_404_is_a_problem_and_202_has_no_body_or_location` and P2 `Replay_409_is_a_problem_with_the_busy_title_and_detail` to AdminDashboardApiTests.cs. Both were green on MVC before conversion (filter run: 48 passed), and green after (48 passed).
Changed:
- backend/src/Modules/Admin/Ecommerce.Admin/Api/AdminProjectionsEndpoints.cs (new, single route, Admin policy, Results<Accepted, ProblemHttpResult>)
- backend/src/Modules/Admin/Ecommerce.Admin/Api/AdminDashboardController.cs (deleted)
- backend/src/Modules/Admin/Ecommerce.Admin/AdminModule.cs (+1 line Map call)
- backend/tests/Ecommerce.Api.IntegrationTests/Projections/AdminDashboardApiTests.cs (P1-P2 appended)
Slice: P2.67. Tracker not marked, not committed.

Mutations (each restored from scratch copy, cmp ok):
- M1 policy -> AdminOrSupport: red `EndpointInventoryTests.T1_every_endpoint_has_the_expected_methods_auth_and_cors`, `Replay_is_401_anonymous_and_403_for_a_Customer_and_for_Support`. Failed: 2.
- M2 Busy -> Accepted: red `Replay_409_is_a_problem_with_the_busy_title_and_detail`, `Replay_while_the_projector_is_held_by_another_worker_is_409_and_changes_nothing`. Failed: 2.

verify.sh: VERIFY PASS (secrets hygiene format build test drift expo-check tsc jest export); build 0 warnings; jest 272 suites / 2111 tests passed.

Quality-bar §2: all boxes met. Server-owned money and stock untouched; the replay write path is unchanged; the one call per request is unchanged; the endpoint has Admin-only policy and tests; the diff is minimal. Docs and tracker are left to the coordinator (N/A).
