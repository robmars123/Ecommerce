# Slice P2.66 — Review thread

## Round 1 — Rob → Marco
Verdict: PASS · verify.sh: `VERIFY PASS (secrets hygiene format build test drift expo-check tsc jest export)` — build 0 warnings; jest 272 suites / 2111 tests

This is a clean transport conversion. The new file follows the plan line by line (I1–I10), the mapping and CSV bytes match the deleted controller, and nothing under `Application/`, `Infrastructure/`, Contracts, migrations or packages moved. I found no blocking issues and no bugs.

### What I checked, with evidence

1. **Policy and group (I1).** `AdminInsightsEndpoints.cs:24` builds its own group from `app` with `AuthorizationPolicies.Admin`. It does not hang off Dashboard's `AdminOrSupport` group, so Support cannot get in through nested policies. RawText matches inventory lines 23 and 25. Registered once, after Dashboard (`AdminModule.cs:35`), and the controller is deleted in the same change, so the route cannot match twice.
2. **Your pins test MVC, and they are not tuned to pass.** I put the deleted controller back from `HEAD`, moved your endpoints file aside, removed the `Map` line and ran `AdminInsightsApiTests|EndpointInventoryTests` on MVC: **Passed 16/16**. Then I restored the working tree from scratch copies (`cmp` identical). Your fix to the first two reds (mint the admin token after `PinNoon`) is the same order the existing `Trend_7d…` and `Export_7d…` facts already use. It fixes a token that had expired under the moved clock. It does not loosen any expectation. Every pin asserts exact values: status, `period`, filename, raw `Content-Type`, `FileNameStar`, first byte `b`, the problem `type`/`title`/`traceId`/text, and the sorted property names.
3. **Mutations, re-run by me** (each restored from a scratch copy, `cmp` OK):
   - M1 group policy → `AdminOrSupport`: **Failed 4/16**: `T1_every_endpoint_has_the_expected_methods_auth_and_cors`, `Export_for_Support_is_403_with_no_amount_and_no_csv`, `Trend_without_a_token_…_Support_is_403_with_no_amount`, `Export_without_a_token_…_Support_is_403`.
   - M2 `FirstOrDefault` → `string.Join(",", …)`: **Failed 1/16**: `Period_blank_whitespace_and_repeated_keys_follow_MVC_on_trend_and_export`.
   - M3 `"text/csv; charset=utf-8"` → `"text/csv"`: **Failed 1/16**: `Export_headers_are_exact_and_the_body_has_no_BOM`.
4. **Queries per request.** Each handler makes exactly one `await trend.GetAsync(parsed, ct)`. The file has no `DbContext`, no loop and no second service call. SQL is unchanged by construction: 8 round-trips per request (6 health + 2 narrow projections), as the plan counted. The health N+1 and the in-memory bucketing are already backlog NOTEs from the plan. They are pre-existing and out of scope here.
5. **Over-fetching and projection.** The handler maps only fields that `RevenueTrendView` already carries. The records are byte-identical to the controller's, and P5 pins every property name.
6. **Clock.** The only clock read in the file is the export filename (`clock.UtcNow`, line 54), which is the controller's behaviour. There is no `DateTime.UtcNow`. The double clock read is already backlog NOTE 3.
7. **Edge cases.** A bad period returns 400 before any query runs. A missing, blank or whitespace `period` returns 200 `today` (MVC parity, pinned). With a repeated key the first value wins (pinned both ways on both routes). `string[]?` with no key gives `null` → `FirstOrDefault` returns `null` → `today`.
8. **Readability.** The file is 60 lines with two small private helpers, each used twice. It has one comment, and that comment explains the only non-obvious choice (`string[]`). The code has no cleverness and no dead code. Each abstraction has two consumers.
9. **Changed or deleted tests.** None were edited or deleted. You appended P1–P5 only, and I count 16 facts in the filter both before and after the change.

### Notes
None new. The plan's three NOTEs are already in `slice-plans/backlog.md` (lines 124–126).

**What was good:** You fixed the pin, not the code, when the pins went red on MVC, and you reported it. That is the order the plan asks for. `ReadPeriod` keeps the whitespace→null rule in one line next to the comment that explains it, so the next reader has no need to open P2.65.
