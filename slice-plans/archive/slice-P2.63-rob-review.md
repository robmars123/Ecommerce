# Slice P2.63 — Review thread

## Round 1 — Rob → Marco
Verdict: BLOCKED · verify.sh: not re-run (no PASS this round). Your two runs ended `VERIFY FAIL: jest`. My full jest run gives the same result: `Tests: 1 failed, 2110 passed, 2111 total` (PW1, "Exceeded timeout of 5000 ms" at `admin-phone-width.test.tsx:89`).

The backend conversion is correct and minimal. It is one handler of about 15 lines, it calls `SearchAsync` once, and the records are unchanged. The controller is deleted in the same change, and nothing changed under `Application/`, `Orders/` or `Catalog/`. I re-ran M1–M4 and got the same reds you reported. The only blocker is the jest gate. It counts as part of this slice because no slice passes on a red `verify.sh`.

### 1. PW1 times out under full-suite load — BLOCKING   (`frontend/apps/store/tests/admin-phone-width.test.tsx:92`)
**What I saw:** PW1 renders the whole app tree with `renderRouter('./app', { initialUrl: url })` for each of the 11 ROUTES rows. It passes alone (22/22). In the full parallel run, one row goes over Jest's 5 s timeout. That happened in both of your verify runs and in my run.
**Why it matters:** `renderRouter('./app')` makes expo-router require and build every route file in `app/`, including all store screens, the root `TopNav` and the cart merge effect, on every row. That cold cost grows with the app, so this is not a one-off. It is the same failure as backlog P2.6 R1 (`tests/app/admin.test.tsx` F7b). A red jest blocks the commit for every slice from now on, whatever the slice touches.
**What I want:**
- Switch PW1 to `renderRouterWithQuery` from `tests/render-with-query.tsx`. That is the pattern `tests/index-route.test.tsx` uses. Build one route map at module scope containing `'admin/_layout'` (the default export of `@/app/admin/_layout`) and the 11 screens the ROUTES rows hit: `admin/index`, `admin/orders/index`, `admin/orders/[id]`, `admin/returns`, `admin/products/index`, `admin/products/new`, `admin/products/[id]`, `admin/categories`, `admin/inventory`, `admin/customers/index`, `admin/customers/[id]`. Import each screen's default export. Nothing else goes in the map.
- Do not pass a `_layout` key. The helper's root layout supplies the `QueryClientProvider`, and a `_layout` in your map would replace it.
- Keep every assertion exactly as it is: the header title, `Open menu`, no `Admin navigation`, the `Dark mode` switch, and the paged `Previous`/`Next`. Keep the ROUTES table, `mockAdminApi` and the 375 px mock unchanged.
- Do not raise any timeout, do not add `jest.retryTimes`, and do not add a per-test timeout argument.
- If an assertion fails only because a root provider is missing (for example `ThemeRoot` for the dark-mode switch), wrap only `admin/_layout` in that provider inside the test. Say which provider in your reply.
**How I'll check:**
1. Diff of the test file: only the import and render lines change. The assertion block is byte-identical (`git diff` shows no `-`/`+` lines inside the `it.each` body except line 92).
2. Mutation PW-M1: in `features/admin-shell/components/AdminShell.tsx:194`, change `width >= sidebarBreakpoint` to `true`, so the sidebar always shows. Expected: all 11 PW1 rows go red on `Open menu` / `Admin navigation`. Restore from a scratch copy and run `cmp`. Quote the red output.
3. Two full `npx jest` runs (or one full `verify.sh` plus one jest), both green. Quote the `Tests:` lines.
**Lesson:** a test pays for every route it loads. Mount only the routes it asserts on, and never fix a slow test by giving it more time.

### 2. Repeated-key binding pin — NOTE (already in backlog)
I11 and S3 are right: MVC took the first `q`, and `string[]? q` + `FirstOrDefault()` keeps that. M4 proves the pin works. The P2.60 Products follow-up is already logged in the backlog.

### 3. Other cold `renderRouter('./app')` tests — NOTE (→ backlog)
`tests/admin-shell-data.test.tsx`, `tests/app/admin-shell.test.tsx`, `tests/app/admin.test.tsx` and `tests/routes.test.tsx` still render the whole app. Each one can hit the same timeout. `routes.test.tsx` may need the real tree, so check it before changing it. Not part of this fix round.

**What was good:** the handler reads straight through: validate, one service call, map. The comment on `string[]? q` explains the binding difference in one line. You pinned I11 and I12 on MVC before converting, so the conversion was proven to keep the old behaviour, not assumed. Also good: the jest failure was reported honestly and not "fixed" with a timeout.

### Checked, no finding
- **Queries:** one `SearchAsync` per request. Admin = 3 queries, Support = 1 (I6). No loops, no extra service calls, and the projections are unchanged (no diff under `Application/`).
- **Limits:** `q` length (2–100) is validated before any query (I3, I12).
- **Auth:** `AdminOrSupport` policy; the inventory row and II11 are green. The role flag comes from `ClaimsPrincipal` as before.
- **Boundaries:** no csproj change, no new reference, the type is `internal`, no `var`, every `using` is used.
- **Tests:** I1–I10 unedited. I11 and I12 are additions only.
- **Mutations (my runs, restored, `cmp` RESTORED each time):**
  - M1: T1, II11, I2 red.
  - M2: T1, I2, I6 red.
  - M3: I6 red.
  - M4: I11 red.

## Round 2 — Rob → Marco
Verdict: PASS · verify.sh: VERIFY PASS (secrets hygiene format build test drift expo-check tsc jest export); jest 272 suites, 2111/2111.

### 1. PW1 jest timeout — Confirmed
I read `git diff` on `admin-phone-width.test.tsx`. Only three things changed: the imports, the module-scope `ADMIN_ROUTES` map, and the render line (`renderRouterWithQuery(ADMIN_ROUTES, ...)`). The `ROUTES` table, the assertions, and the mocks are untouched, so no invariant is weakened. The map covers all 11 routes plus `_layout`, so the test still mounts the real screens in the real shell, and no timeout was raised.

Mutation PW-M1 (`AdminShell.tsx:194` `width >= sidebarBreakpoint` -> `true`): `Tests: 22 failed, 8 passed, 30 total` (11 rows x 2 jest projects). I restored it from a scratch copy and `cmp` printed RESTORED.

The NOTE on the cold `renderRouter('./app')` in 4 other tests is already in `backlog.md`.

**What was good:** the fix changed only how the route tree is built, not what is asserted.
