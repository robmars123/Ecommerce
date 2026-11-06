# Test enforcement — Rob's plan (scoped task, not a slice)

**Mode:** Plan · **Verdict:** READY · **Date:** 2026-11-03
**Requested by:** Nathaniel — "is test fully enforced in all backend frontend? if not update both md files and apply" / "let Rob do it, and make all tests applied by Marco".
**Scope:** test tooling, CI enforcement, and tests for code that already exists. Slice 1 is **not** started. No git writes.

---

## 1. Audit — what's enforced today

### Backend

| Item | State | Evidence |
|---|---|---|
| Test projects | 3 (Domain, Application, Api.IntegrationTests), xUnit v3 on VSTest (`Microsoft.NET.Test.Sdk` + `xunit.runner.visualstudio`) | `Ecommerce.slnx:7-11`, `Directory.Packages.props:12-14` |
| Tests | 16 test methods (15 `[Fact]`, 1 `[Theory]` in `CorrelationIdTests.cs:41`): Domain 1, Application 2, Integration 13 | `backend/tests/**` |
| Real Postgres | Yes — Testcontainers PG17, version asserted | `MigrationTests.cs:10-24` |
| Rule 9 (`IClock`) | Enforced by a source scan test | `ClockRuleTests.cs:31-66` |
| Domain dependency direction | Enforced by a reference test | `DomainArchitectureTests.cs:101-110` |
| CI runs tests | Yes | `.github/workflows/ci.yml:33-34` |
| CI migration drift + idempotent script | Yes | `ci.yml:36-39` |
| **Zero-test guard** | **Missing.** VSTest exits 0 when a project discovers no tests (warning only). A test project emptied by accident, or a broken discovery, reports green | no `.runsettings`, no `TreatNoTestsAsError` anywhere |
| **Coverage / results** | **Missing.** No coverage collected, no TRX uploaded; a red run in CI has no artifact to read | `ci.yml:33-34` |
| Skipped tests | Review-only (quality bar §4.2); no automated check | — |
| Lint / format | None (consistent with quality bar §3: style never blocks) | — |

### Frontend

| Item | State | Evidence |
|---|---|---|
| Test runner | **None.** No `jest`, no `test` script, no test devDependencies | `frontend/apps/store/package.json:6-13, 29-32` |
| Tests | **Zero.** Not one `*.test.ts(x)` | `components/`, `app/` |
| CI frontend job | `npm ci`, `expo install --check`, `tsc --noEmit`, `expo export` — **no test step** | `ci.yml:41-58` |
| Stack lock | Lists backend test stack only — no frontend runner was allowed | `docs/ecommerce-architecture.md` §2 "Tests" row (before this task) |
| Architecture §11 | Backend-only (tests 1–19). No frontend required tests, no frontend test-first list | §11.1–11.2 |
| Quality bar | §4 "Unit tests" = `dotnet test` only; §2/§4 Marco commands have no frontend test; §5 says nothing about the frontend | `docs/quality-bar.md:31, 77, 84, 88` (before) |
| §5.4 naming | Says tests are co-located and never under `app/`, but there was nothing to run them and no home for route tests | §5.4 Tests row |
| Client-side hard rules | Checkout idempotency key reuse, no optimistic checkout, no client totals (§5.2, rules 1/2/5) had **no way to be proven** on the client | — |
| Logic under test today | `components/ScreenStub.tsx` (title header, optional detail, children, sets Stack title), `components/StubLink.tsx` (accessible link, 44pt target, navigation), 20 route files + `_layout` + `+not-found` | `components/*.tsx`, `app/**` |

**Answer to Nathaniel: no.** Backend tests run and gate CI, but a silently empty project would pass and nothing is reported. The frontend had no runner, no tests, and no CI step at all.

---

## 2. Decisions

| # | Decision | Reason |
|---|---|---|
| D1 | **Frontend runner: Jest + `jest-expo` + `@testing-library/react-native` (RNTL) + `test-renderer` + `@react-native/jest-preset` + `@types/jest`**, devDependencies only. Recorded as an **owner-approved §2 amendment** (architecture §2, §13.1, §13.2). Replaces nothing. | Expo's official path; `jest-expo` 58.0.x is pinned by SDK 58 (`bundledNativeModules.json`). Registry check: `jest-expo@58.0.13` peers `jest ^29‖^30` and **requires** `@react-native/jest-preset ^0.88.1` (RN 0.88 split the preset out). RNTL 14.1.0 **requires** `test-renderer ^1` (replaces deprecated `react-test-renderer`). `expo-router/testing-library` requires RNTL `>= 14`. `@types/jest` types the globals and the matcher augmentations in `expo-router/testing-library/expect.d.ts`. |
| D2 | **No MSW, no Detox/Maestro, no Playwright** in v1. Network is mocked with `jest.fn()` / `jest.spyOn(globalThis, 'fetch')`. | Rule 16: no package without need. Device/browser E2E needs infrastructure a solo Windows dev can't run per PR (architecture §13.0). |
| D3 | **Route-level tests live in top-level `tests/`**: the route-table smoke test is `tests/routes.test.tsx`; future route-specific tests mirror the path under `tests/app/`. Component/logic tests stay co-located. | Expo Router turns every file in `app/` into a route, so co-location is impossible for routes. Documented in §5.4. |
| D4 | **Jest config in `package.json` `"jest"` key**, not a `jest.config.js`. Preset `jest-expo` (iOS default). Add `moduleNameMapper` for `@/`. | One less file. `@/` alias is resolved by Metro, not by Jest. |
| D5 | **Per-platform Jest projects deferred** to the slice that adds `lib/platform.native.ts` / `.web.ts` (switch to `jest-expo/ios`, `/android`, `/web` projects in that slice). | No platform split exists yet; running every test 3× today buys nothing. Written into §11.3 so it isn't forgotten. |
| D6 | **tsconfig `"types": ["jest"]`** set explicitly. | TypeScript 6 changed the default for `types`; being explicit makes Jest globals resolve deterministically, and `tsc --noEmit` keeps type-checking tests. |
| D7 | **Backend zero-test guard:** `backend/tests/test.runsettings` with `TreatNoTestsAsError=true`, wired by `backend/tests/Directory.Build.props` (`RunSettingsFilePath`), which imports the root props. | Built into VSTest; no package. Applies to `dotnet test` locally and in CI identically. |
| D8 | **Backend coverage:** `--collect "Code Coverage;Format=cobertura"` (Microsoft.CodeCoverage, already transitive via `Microsoft.NET.Test.Sdk`) + `--logger trx`, uploaded as CI artifacts. **No % gate.** | Quality bar §5: coverage proves nothing about oversell. Visibility only. No new package (coverlet not needed). |
| D9 | **Frontend coverage:** `npm test -- --ci --coverage`, lcov uploaded as artifact. **No % gate. No `--passWithNoTests`** (Jest fails on zero tests by default). | Same as D8; zero-test guard for free. |
| D10 | **CI hygiene step** in the frontend job: fail on any `*.test.ts(x)` under `app/` and on focused tests (`it/test/describe.only`, `fit`, `fdescribe`). | A focused test makes Jest skip everything else and still report green. A test in `app/` becomes a route. |
| D11 | **No lint/format gate** (no ESLint, no `dotnet format --verify-no-changes`). Revisit at Slice 7 with the warnings-as-errors switch. | Quality bar §3: style never blocks; ESLint would be a new package for a non-blocking concern. `tsc --strict` and `dotnet build` remain the static gates. |
| D12 | **Frontend required tests F1–F8** added to architecture §11.3, one per slice that owns the behavior; **F4, F5, F6 test-first** (client side of rules 1, 2, 5). F1 is due now (retro for Slice 0). | Gives the frontend the same "required test per slice" contract the backend has. |
| D13 | **Retro tests must be proven meaningful:** for each new test file, Marco breaks the code under test once (temporarily), shows the failure, and restores it. | These tests are written after the code; a mutation check is the substitute for "shown failing first". |
| D14 | **CLAUDE.md not edited by Rob.** Its Commands block needs one line (below, §7); Nathaniel applies it. | Agents may not change CLAUDE.md on an agent's instruction. |
| D15 | Quality bar §4 keeps its numbering; frontend tests are part of item 2. | Slice plans cite §4.3–§4.6 by number. |

**Doc edits made by Rob (this task):** architecture §2 (Frontend tests row), §5.4 (Tests row), §11.3 (new), §11.4 (new), §13.1 (Jest row), §13.2 (4 rows), §14 (F1–F8), footer · quality bar §2, §3, §4, §5 · README (Tests block).

---

## 3. Build plan for Marco

Work from `D:\Daddy\Career\Projects\2027\Ecommerce`. No git writes. No changes in `backend/src` or `app/`.

### 3.1 Backend — zero-test guard and coverage

**New `backend/tests/test.runsettings`:**

```xml
<?xml version="1.0" encoding="utf-8"?>
<RunSettings>
  <RunConfiguration>
    <TreatNoTestsAsError>true</TreatNoTestsAsError>
  </RunConfiguration>
</RunSettings>
```

**New `backend/tests/Directory.Build.props`** (must import the root one, or `net10.0`/`Nullable`/`ImplicitUsings` are lost):

```xml
<Project>
  <Import Project="$([MSBuild]::GetPathOfFileAbove('Directory.Build.props', '$(MSBuildThisFileDirectory)../'))" />
  <PropertyGroup>
    <RunSettingsFilePath>$(MSBuildThisFileDirectory)test.runsettings</RunSettingsFilePath>
  </PropertyGroup>
</Project>
```

`Directory.Packages.props` is located independently — leave it alone. No package changes.

**Acceptance (paste output):**

1. `dotnet build` — 0 errors.
2. `dotnet test` — all tests pass (16 methods; the theory expands, so the reported count is higher — paste the real count). Docker running.
3. Guard proof: `dotnet test Ecommerce.slnx --no-build --filter "FullyQualifiedName~ZzNoSuchTest"` → **non-zero exit**, error that no tests ran. Paste the exit code (`echo $?`).
4. Coverage proof: `dotnet test Ecommerce.slnx --no-build --collect "Code Coverage;Format=cobertura" --logger trx --results-directory artifacts/test-results` → passes; list the produced `*.cobertura.xml` and `*.trx` files. `artifacts/` and `coverage/` are already ignored by the root `.gitignore` (lines 50, 77) — no change needed there.

### 3.2 Frontend — install

```bash
cd frontend/apps/store
npx expo install jest-expo jest @types/jest @testing-library/react-native test-renderer @react-native/jest-preset -- --save-dev
```

Required resolution (verify in `package.json` + lockfile; fix with an explicit `@version` if not):

| Package | Must be | Why |
|---|---|---|
| `jest-expo` | `58.0.x` (SDK pin, `~58.0.13`) | `expo install --check` enforces it |
| `jest` | `30.x` | jest-expo peer `^29 ‖ ^30`; latest major |
| `@types/jest` | `30.x` | match Jest major |
| `@testing-library/react-native` | `14.x` | `expo-router/testing-library` requires `>= 14` |
| `test-renderer` | `1.x` | RNTL 14 required peer |
| `@react-native/jest-preset` | **`0.88.1`** exactly | must match `react-native 0.88.1`; jest-expo peer `^0.88.1` |

Then make all six **exact** versions in `package.json` (no `^`/`~`), matching the house style of the existing dependencies, and run `npm install` so the lockfile agrees. `npx expo install --check` must pass. No other packages.

### 3.3 Frontend — config

**`package.json`** — add script and `jest` key:

```json
"scripts": {
  "test": "jest"
},
"jest": {
  "preset": "jest-expo",
  "moduleNameMapper": { "^@/(.*)$": "<rootDir>/$1" },
  "testPathIgnorePatterns": ["/node_modules/", "<rootDir>/dist/", "<rootDir>/.expo/"],
  "collectCoverageFrom": [
    "app/**/*.{ts,tsx}",
    "components/**/*.{ts,tsx}",
    "features/**/*.{ts,tsx}",
    "lib/**/*.{ts,tsx}",
    "!**/*.test.{ts,tsx}"
  ],
  "coverageReporters": ["text-summary", "lcov"]
}
```

(Keep the existing scripts; add `test` beside them.) Do **not** add `passWithNoTests`, `bail`, or coverage thresholds. `coverage/` is already ignored by the root `.gitignore:77`; no change needed.

**`tsconfig.json`** — add `"types": ["jest"]` to `compilerOptions`. `include` already covers `tests/**` and `components/**/*.test.tsx`.

### 3.4 Frontend — tests to write (real, behavioral; RNTL 14 APIs are async — `await render`, `await fireEvent.press`)

Import rendering helpers from `expo-router/testing-library` (`renderRouter`, `screen`, `fireEvent`), which re-exports RNTL and registers `toHavePathname`. Query by role and label, never by test ID or snapshot.

**A. `components/ScreenStub.test.tsx`** — render inside a router because `ScreenStub` uses `Stack.Screen`. Use a header-less layout so only the stub's own heading exists:

```tsx
const layout = () => <Stack screenOptions={{ headerShown: false }} />;
await renderRouter({ _layout: layout, index: () => <ScreenStub title="Cart" /> }, { initialUrl: '/' });
```

1. renders the title as an accessible header (`getByRole('header', { name: 'Cart' })`) and the "Not built yet." note.
2. renders `detail` when given, and renders no detail text when omitted (`queryByText(...)` is null).
3. renders `children` (e.g. a `<Text>child</Text>`).

**B. `components/StubLink.test.tsx`** — routes `{ _layout: header-less Stack, index: () => <StubLink href="/cart" label="Cart" />, cart: () => <Text>Cart screen</Text> }`:

1. exposes an accessible link named by its label (`getByRole('link', { name: 'Cart' })`).
2. meets the 44pt touch target (`toHaveStyle({ minHeight: 44 })`) — quality bar §6 accessibility.
3. pressing it navigates: `await fireEvent.press(link)` → `expect(screen).toHavePathname('/cart')` and "Cart screen" is visible.

**C. `tests/routes.test.tsx`** — real app directory, real `RootLayout` (`renderRouter('./app', { initialUrl })`; Jest runs with cwd = `frontend/apps/store`):

1. **Route table is complete:** read `app/` recursively with `fs`, drop `_layout.tsx` and `+not-found.tsx`, and assert the sorted file list equals the sorted `file` column of the table below. Adding or renaming a route without updating the table fails.
2. **`it.each` over the table:** renders at `url`, `expect(screen).toHavePathname(url)`, at least one `header` named `title` exists (use `getAllByRole` — the native-stack header may also render the title), and "Not built yet." is visible.
3. **Unknown URL** `/does-not-exist` renders the "Not found" header and "This screen does not exist."
4. **Not-found recovery:** pressing the "Go home" link lands on `/` and shows the "Home" header.

| file | url | title |
|---|---|---|
| `index.tsx` | `/` | Home |
| `products/index.tsx` | `/products` | Products |
| `products/[slug].tsx` | `/products/blue-shirt` | Product |
| `cart.tsx` | `/cart` | Cart |
| `checkout.tsx` | `/checkout` | Checkout |
| `orders/index.tsx` | `/orders` | Orders |
| `orders/[id].tsx` | `/orders/42` | Order |
| `login.tsx` | `/login` | Log in |
| `register.tsx` | `/register` | Register |
| `reset-password.tsx` | `/reset-password` | Reset password |
| `admin/index.tsx` | `/admin` | Admin dashboard |
| `admin/products/index.tsx` | `/admin/products` | Admin · Products |
| `admin/products/[id].tsx` | `/admin/products/7` | Admin · Product |
| `admin/inventory.tsx` | `/admin/inventory` | Admin · Inventory |
| `admin/orders/index.tsx` | `/admin/orders` | Admin · Orders |
| `admin/orders/[id].tsx` | `/admin/orders/9` | Admin · Order |
| `admin/returns.tsx` | `/admin/returns` | Admin · Returns |
| `admin/customers/index.tsx` | `/admin/customers` | Admin · Customers |
| `admin/customers/[id].tsx` | `/admin/customers/3` | Admin · Customer |

(Titles copied from the current route files; use `/` separators in the `file` column and normalize `path.sep` when comparing, so the test passes on Windows and Linux.)

This is architecture §11.3 **F1**.

**Mutation proof (D13) — do each once, paste the failing output, then restore:**
- `StubLink.tsx` `minHeight: 44` → `40` ⇒ B2 fails.
- `ScreenStub.tsx` remove `accessibilityRole="header"` ⇒ A1 fails.
- rename `app/admin/returns.tsx` → `app/admin/return.tsx` (temporarily) ⇒ C1 fails.

Never commit a broken state; restore and rerun green.

### 3.5 CI — `.github/workflows/ci.yml`

**Backend job** — replace the test step and add an upload:

```yaml
      - name: Test (unit + Testcontainers integration, coverage)
        run: dotnet test Ecommerce.slnx --no-build -c Release --collect "Code Coverage;Format=cobertura" --logger trx --results-directory artifacts/test-results
      - name: Upload backend test results
        if: always()
        uses: actions/upload-artifact@v4
        with:
          name: backend-test-results
          path: artifacts/test-results
```

**Frontend job** — after `npx tsc --noEmit`, before `npx expo export`:

```yaml
      - name: Test hygiene (no tests under app/, no focused tests)
        run: |
          if git ls-files -- app | grep -E '\.test\.(ts|tsx)$'; then
            echo "::error::Test files under app/ become routes (architecture §5.4)"; exit 1
          fi
          if git grep -nE '(^|[^[:alnum:]_.])((it|test|describe)\.only|fit|fdescribe)\(' -- '*.test.ts' '*.test.tsx'; then
            echo "::error::Focused test found — it silently skips every other test"; exit 1
          fi
      - name: Unit + component tests
        run: npm test -- --ci --coverage
      - name: Upload frontend coverage
        if: always()
        uses: actions/upload-artifact@v4
        with:
          name: frontend-coverage
          path: frontend/apps/store/coverage
```

(`working-directory` defaults to `frontend/apps/store`, so `git ls-files -- app` and the pathspecs are relative to it; the upload `path` is relative to the workspace root.) Use `actions/upload-artifact@v4` or the latest existing major — do not invent a version.

**Hygiene proof (local, Git Bash, from `frontend/apps/store`; use `git grep --untracked` / `ls` since the files aren't tracked):** temporarily add `it.only(` to one test ⇒ the grep matches; remove it. Paste both.

### 3.6 Acceptance — Marco pastes real output of all of these

```
dotnet build
dotnet test                                            # all passed, real count
dotnet test Ecommerce.slnx --no-build --filter "FullyQualifiedName~ZzNoSuchTest"; echo $?   # non-zero
cd frontend/apps/store && npx expo install --check
cd frontend/apps/store && npx tsc --noEmit
cd frontend/apps/store && npm test -- --ci --coverage   # A(3) + B(3) + C(1 + 19 + 2) = 28 passed, 0 skipped
cd frontend/apps/store && npx expo export --platform web
```

Plus the D13 mutation outputs and the hygiene proof. Quality bar §2 self-check, including the new frontend item.

---

## 4. Risks

| Risk | Mitigation |
|---|---|
| `renderRouter('./app')` header duplication: the native-stack header may render the title too, making `getByRole('header')` ambiguous | Component tests use `headerShown: false`; the route test uses `getAllByRole` + pathname + body note |
| RNTL 14 is async-by-default; sync-style `render()` would leave unresolved promises and act warnings | Plan mandates `await render`/`await renderRouter`/`await fireEvent.press`; act warnings in output are a review finding |
| `@react-native/jest-preset` drifting from `react-native` | Pinned exactly to 0.88.1; bump together on SDK upgrades (`expo install --fix`) |
| `TreatNoTestsAsError` interacts with `--filter` in local dev (filtered run matching nothing now errors) | Intended. Document in Marco's report |
| `"types": ["jest"]` hides some ambient type the app relied on | `tsc --noEmit` must pass; if it fails, add the missing type package name to `types` with the reason — never remove `jest` |
| Coverage collection slows CI integration tests | Visibility only; drop `--collect` before ever weakening a test |
| CLAUDE.md Commands block stays stale until Nathaniel edits it | Exact text in §7 |

## 5. Out of scope

Slice 1 work, `lib/`/`features/` creation, ESLint/Prettier, E2E, coverage thresholds, changes to `backend/src` or `app/`.

## 6. Review focus (Rob, after Marco)

Packages exactly as D1 and nothing else · lockfile consistent and `expo install --check` green · no `passWithNoTests`/thresholds/`.only`/`.skip` · tests assert behavior by role/label · D13 failures pasted · CI YAML valid and the hygiene regex not self-matching · `backend/tests/Directory.Build.props` imports the root props · guard proof exit code non-zero.

## 7. For Nathaniel — CLAUDE.md Commands block (Rob did not edit CLAUDE.md)

```
dotnet build
dotnet test                           # needs Docker running (Testcontainers)
cd frontend/apps/store && npx tsc --noEmit
cd frontend/apps/store && npm test -- --ci
cd frontend/apps/store && npx expo export --platform web
```

and in the Final report template: `Commands: dotnet build ✔ · dotnet test ✔ [n passed] · tsc ✔ · npm test ✔ [n passed] · expo export ✔`.

---

## Marco build

**Date:** 2026-11-03 · **Status:** done, all commands green.

**Packages (devDependencies, exact, via `npx expo install ... -- --save-dev`, then pinned):** `jest` 30.5.2 · `jest-expo` 58.0.13 · `@types/jest` 30.0.0 · `@testing-library/react-native` 14.1.0 · `test-renderer` 1.3.1 · `@react-native/jest-preset` 0.88.1. Nothing else added.

**Files changed:** `backend/tests/test.runsettings` (new) · `backend/tests/Directory.Build.props` (new, imports root props) · `.github/workflows/ci.yml` · `frontend/apps/store/package.json` (+lockfile; `test` script, `jest` key, 6 devDeps) · `frontend/apps/store/tsconfig.json` (`types: ["jest","node"]`) · `components/ScreenStub.test.tsx`, `components/StubLink.test.tsx`, `tests/routes.test.tsx` (new).

**Deviations:**
- tsconfig `types` also lists `"node"` (routes test uses `node:fs`/`node:path`). `@types/node` 26.6.6 is already installed transitively (via jest); no new package. Allowed by plan risk row 5.
- 29 tests, not 28: ScreenStub detail "given" and "omitted" are two tests (A = 4, B = 3, C = 22).
- tsconfig reformatted by JSON rewrite (same content plus `types`).

**Proof (abridged real output):**
- `dotnet build`: Build succeeded, 0 Warning(s), 0 Error(s).
- `dotnet test`: Passed 1 (Domain) + 2 (Application) + 15 (Integration) = 18, Failed 0, Skipped 0.
- Guard: `dotnet test Ecommerce.slnx --no-build --filter "FullyQualifiedName~ZzNoSuchTest"` printed "No test matches the given testcase filter ..." for all 3 assemblies, `EXIT=1`.
- Coverage: produced `artifacts/test-results/*.trx` (3) and `*.cobertura.xml` (3).
- `npx expo install --check`: "Dependencies are up to date". `npx tsc --noEmit`: exit 0.
- `npm test -- --ci --coverage`: Test Suites 3 passed; Tests 29 passed; coverage 100% statements/lines of components; no act() or console warnings in output.
- `expo export --platform web --output-dir <scratchpad>/export`: exported OK.
- Hygiene: clean tree, git grep exit 1 (no match); with `it.only(` added, matched `components/ScreenStub.test.tsx:10`, exit 0; restored, exit 1. A `app/x.test.tsx` matched the app/ check, then removed.
- D13 mutations: (1) `minHeight: 44`->`40`: StubLink 1 failed (toHaveStyle, Expected/Received diff), 2 passed. (2) removed `accessibilityRole="header"`: ScreenStub 1 failed "Unable to find an element with role: header, name: Cart", 3 passed. (3) renamed `app/admin/returns.tsx` -> `return.tsx`: routes 2 failed (table diff `-"admin/returns.tsx"` / `+"admin/return.tsx"`, plus the returns render case), 20 passed. All restored; re-run green 29/29.

---

## Rob review

**Mode:** Review · **Verdict:** BLOCKED · **Date:** 2026-11-03

### Blocking failures

1. **`frontend/apps/store/package.json:17,23,24` — test tooling is also in runtime `dependencies`.** `@types/jest` `30.0.0`, `jest` `~30.5.2`, and `jest-expo` `~58.0.13` are listed under `dependencies` as well as `devDependencies` (lines 35, 37, 38). The lockfile root (`package-lock.json` `packages[""].dependencies`) has the same entries. This breaks plan D1 ("devDependencies only") and §3.2 ("exact versions, no `^`/`~`"). npm also gives no guarantee which entry wins when a name is listed twice. **Fix:** delete the three entries from `dependencies`, keep the exact pins in `devDependencies`, then run `npm install` so the lockfile root agrees. Re-run `npx expo install --check`, `npx tsc --noEmit`, and `npm test -- --ci --coverage`, and confirm `packages[""].dependencies` in the lockfile no longer lists jest, jest-expo, or @types/jest.
2. **`.github/workflows/ci.yml:71` — the focused-test guard misses `.only.each`.** The regex requires `(` straight after `.only`, `fit`, or `fdescribe`. So `it.only.each([...])(...)`, `test.only.each`, `describe.only.each`, and `fit.each` all pass the check, and Jest still focuses them and skips everything else. I checked this locally: `test.only.each(` does not match. The regex came from my plan, so this is a defect in my plan that Marco copied faithfully. Since it's a gap in the gate itself, it blocks. **Fix:** change the trailing `\(` to `[(.]`. The pattern becomes `'(^|[^[:alnum:]_.])((it|test|describe)\.only|fit|fdescribe)[(.]'`. Prove it: from `frontend/apps/store`, pipe `it.only(`, `fit(`, `describe.only(`, `test.only.each(`, `fit.each(` (all must match) and `profit(`, `it.each(`, `foo.fit(` (none may match) through `grep -nE`, and run `git grep --untracked` on the clean tree (exit 1).

### Bugs found
- Both items above.

### Rulings on Marco's deviations
- **tsconfig `types: ["jest","node"]`: accepted.** `tests/routes.test.tsx` uses `node:fs` and `node:path`, and plan risk row 5 allows this. Non-blocking: `@types/node` (26.6.6) comes in only transitively. If a future jest bump drops it, `tsc` fails loudly, which is safe. Pin it as a direct devDependency only if that happens, and record it as a decision.
- **29 tests instead of 28: accepted.** Splitting the "detail given" and "detail omitted" cases is better. The omitted case also asserts that exactly 2 text nodes render.
- **tsconfig reformat: accepted.** The content is equivalent and only `types` was added.

### Verified (by reading and re-running)
- `backend/tests/test.runsettings` and `backend/tests/Directory.Build.props` match the plan, and the props file imports the root props. The guard causes the failure: with the filter matching nothing, exit is **1**. The control run with `-- RunConfiguration.TreatNoTestsAsError=false` exits **0**.
- CI YAML parses (js-yaml). Step order is correct: build → test+coverage → upload (`if: always()`) → ef tools; npm ci → expo check → tsc → hygiene → jest → upload (`if: always()`) → export. Working directories and artifact paths are correct: the backend path is relative to the repo root, and the frontend upload path is `frontend/apps/store/coverage` (workspace-relative). `actions/upload-artifact@v4` is a real major. The hygiene greps only search tracked `*.test.ts(x)`, so they can't match `ci.yml` itself or `node_modules` (ignored and untracked).
- Tests are behavioral. They query by role and label, with no test IDs and no snapshots. RNTL APIs are awaited, and there are no act() or console warnings in the output. The route-table test reads `app/` from disk, so it is not tautological. The D13 mutation claims fit the code.
- `app/` is unchanged. The mtimes are the original 07:49 stamps, no `*.test.*` or `return.tsx` is left, and `components/*.tsx` are restored (`minHeight: 44`, `accessibilityRole="header"` present).
- No `passWithNoTests`, thresholds, `bail`, `.only`, or `.skip`.

### Non-blocking notes
- `ci.yml` pins `actions/checkout@v7`, `actions/setup-node@v7`, and `actions/setup-dotnet@v6`. These are from Slice 0 and out of scope here. Check that these majors exist the first time CI runs on GitHub.
- The `ScreenStub` "omitted" test asserts `getAllByText(/.+/)` has length 2. Any future extra text in `ScreenStub` will break it. That is intended, but update it when the stub changes.
- The focused-test guard only scans `*.test.ts(x)`. Jest's default `testMatch` also picks up `*.spec.*` and `__tests__/`. House naming is `.test.`, so keep it that way.

### Commands (Rob, real output)
- `dotnet build`: Build succeeded, 0 Warning(s), 0 Error(s).
- `dotnet test`: Domain 1, Application 2, Integration 15 passed; 0 failed, 0 skipped; exit 0.
- Guard: `dotnet test Ecommerce.slnx --no-build --filter "FullyQualifiedName~ZzNoSuchTest"` printed "No test matches the given testcase filter" for each of the 3 assemblies, **exit 1**. Control with the guard off: exit 0.
- `npx expo install --check`: "Dependencies are up to date", exit 0.
- `npx tsc --noEmit`: exit 0.
- `npm test -- --ci --coverage`: Test Suites 3 passed; Tests 29 passed, 29 total; coverage 100% statements, branches, functions, and lines; 0 act/warn/console lines.
- `npx expo export --platform web --output-dir <scratchpad>/export`: exported, exit 0.
- Cleanup: removed the `frontend/apps/store/coverage/` I generated (gitignored, root `.gitignore:77`).

## Marco fix round 1

1. `frontend/apps/store/package.json`: removed `@types/jest`, `jest`, `jest-expo` from `dependencies`; exact-pinned devDependencies kept. `npm install` run; lockfile root `dependencies` has no jest entries, `devDependencies` has jest 30.5.2, jest-expo 58.0.13, @types/jest 30.0.0. `expo install --check`: "Dependencies are up to date". tsc exit 0. `npm test -- --ci --coverage`: 3 suites, 29 tests passed, 100% coverage.
2. `.github/workflows/ci.yml`: guard trailing `\(` changed to `[(.]`. grep -E proof: matches `it.only(`, `fit(`, `describe.only(`, `test.only.each(`, `fit.each(`; no match on `profit(`, `it.each(`, `foo.fit(`. `git -c safe.directory=* grep --untracked` on the clean tree exits 1.
3. dotnet build: 0 warnings, 0 errors. dotnet test: 1 + 2 + 15 passed, 0 failed. expo export --platform web (to scratchpad): exported.

## Rob re-review

**Mode:** Review · **Verdict:** PASS · **Date:** 2026-11-03 · **After:** Marco fix round 1

### Blocking items from the last review
1. **Runtime dependencies: fixed.** `package.json` `dependencies` lists only the 12 app packages. `devDependencies` pins `jest` 30.5.2, `jest-expo` 58.0.13, and `@types/jest` 30.0.0 exactly, with no `^` or `~`. In the lockfile, `packages[""].dependencies` matches, with no jest, jest-expo, or @types/jest, and `packages[""].devDependencies` matches the manifest. `node_modules/jest` is flagged `devOptional` rather than `dev`. That is correct npm behavior, not a defect: `npm explain jest` shows an optional peer path (expo-router to RNTL to jest), and `--omit=dev` still leaves it out.
2. **Focused-test regex: fixed.** `ci.yml:71` ends in `[(.]`. I piped each string through `grep -E` myself. These match: `it.only(`, `fit(`, `describe.only(`, `test.only.each(`, `fit.each(`, `fdescribe(`, `it.only.each([1])(`. These do not: `profit(`, `it.each(`, `foo.fit(`, `test.each(`, `fitness(`, `describe.skip(`. `git grep --untracked` on the clean tree exits **1**.

### Scope check
Files modified in fix round 1 by mtime: `package.json`, `package-lock.json`, and `.github/workflows/ci.yml` only. `app/` still has the original 07:49 stamps, with no `*.test.*` or `return.tsx`. `components/*.tsx` still have `minHeight: 44` and `accessibilityRole="header"`.

### Bugs found
None.

### Non-blocking notes
- `package.json:22` reads `"react":"19.3.0"` with no space after the colon. It is valid JSON, so it's cosmetic. Fix it the next time this file is touched.
- The `Updated` stamps in `AGENT_STATUS.md` (13:05/13:30) don't match the machine's local clock (about 08:1x CST). Use local time, as the file's rule says.
- Earlier notes still apply: check the checkout/setup-node/setup-dotnet action majors on the first GitHub run, and keep `.test.` naming because the guard doesn't scan `*.spec.*` or `__tests__/`.

### Commands (Rob, real output)
- `dotnet build`: 0 Warning(s), 0 Error(s).
- `dotnet test`: Domain 1, Application 2, Integration 15 passed; 0 failed, 0 skipped; exit 0.
- Guard: with `--filter "FullyQualifiedName~ZzNoSuchTest"`, "No test matches" printed for all 3 assemblies, **exit 1**. Control with `TreatNoTestsAsError=false`: exit 0.
- `npx expo install --check`: "Dependencies are up to date", exit 0.
- `npx tsc --noEmit`: exit 0.
- `npm test -- --ci --coverage`: Test Suites 3 passed; Tests 29 passed, 29 total; coverage 100% statements (30/30), branches (2/2), functions (23/23), and lines (30/30); no act/warn/console lines; exit 0.
- `npx expo export --platform web --output-dir <scratchpad>/export-rr`: exported, exit 0.
- Cleanup: removed the generated `frontend/apps/store/coverage/` (gitignored).
