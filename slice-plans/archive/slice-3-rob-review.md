# Slice 3 — Review thread

## Round 1 — Rob → Marco
Verdict: BLOCKED (one gate item only) · verify.sh:
- **Release:** `VERIFY FAIL: build test drift`. The only cause is MSB3027 / MSB3021: "The file is locked by: .NET Host (51052)", on `Ecommerce.Api/bin/Release/net10.0/*.dll`. secrets, hygiene, expo-check, tsc, jest (`61 passed / 374 passed`) and export all passed in that run.
- **Debug:** `VERIFY_CONFIGURATION=Debug` gives `VERIFY PASS (secrets hygiene build test drift expo-check tsc jest export)`, with build `0 Error(s)|0 Warning(s)`, Inventory 19, Cart 19, and drift clean.
- **Release gate pending: PID 51052.**

Scope: I compared against `slice-3-baseline.txt`, and 94 paths are new or changed. `git diff --cached` is empty. I found no git write. The cart is built the way the plan asked:
- Every write takes the user's cart row `FOR UPDATE` first.
- Prices come only from Catalog, and strict JSON rejects any price field.
- The merge looks up the idempotency key under that lock, never lowers a server line, and the device cart clears only on a 200.

I found no code bug.

### 1. Release gate not run — BLOCKING (gate only)   (`scripts/verify.sh`, build step)
**What I saw:** your smoke API, PID 51052, still holds the Release DLLs. You were right not to kill it, and I did not either.
**Why it matters:** quality bar §3 needs the full gate in the configuration the Docker image and CI use. Debug proves the code. It does not prove the Release build.
**What I want:** no code change. Once Nathaniel stops PID 51052, the coordinator re-runs `bash scripts/verify.sh` (Release) and quotes the `VERIFY` line.
**How I'll check:** a `VERIFY PASS (…)` line from a Release run.
**Lesson:** stop every process you start before you report. A smoke server should be started so that you can stop it yourself.

### 2. Required tests are complete — NOTE (Confirmed)
- **Test 21:** 13 integration cases cover every plan bullet, including strict-JSON 400s for `unitPrice` and `price`. **Test 22:** 12 cases cover every D6 branch, plus a byte-identical replay that still holds after an edit (`first.Text == third.Text`), 409 for a different body or user, 400s, and a parallel same-key test.
- **F9:** 6 store tests and 6 hook tests (rehydrate, edit drops the key, same key on retry, a 409 drops the key, one request in flight).
- **F3:** CartView 6, AddToCart 5, MergeResultNotice 5.
- **Red outputs:** they are credible. Before the controller existed, 28 cases went NotFound. The F9 stubs threw or did nothing.

### 3. Mutation checks — NOTE (Confirmed)
All seven name the red test. Your rewrite of #1 is sound:
- On a first write, `INSERT … ON CONFLICT (user_id)` already makes the second transaction wait on the uncommitted unique-index entry, so the creation race serialises itself even without `FOR UPDATE`. That explains why your first version stayed green.
- With the cart pre-created, the four adds race on the line itself. Without the lock you get either a unique violation (non-200) or a lost update (final ≠ 8), and both fail the test.

### 4. Your deviations — NOTE
- **Replay re-maps the stored application result.** Within one build, the output is byte-identical: mapping is deterministic, `decimal` keeps its value through JSON, and both the test and the smoke run (462 = 462 bytes) show equal bytes. Across a deploy that renames a `MergeResult`/`CartView` member, System.Text.Json fills the missing member with a default, so a replay within the 24 h window could come back with blanks. That is not a correctness loss, since quantities are not re-applied. Accepted for Slice 3. For Slice 4: either store the HTTP body, or treat these records as wire contracts that change only additively.
- **Notice in the Stack header (every screen except `/admin`):** accepted. An admin who signs in straight into `/admin` sees the result when they next open the store.
- **Empty `carts` row after a rejected add or set:** accepted. It does not change any read (GET shows an empty cart) and it is the same lock row the next write needs.
- **Additions beyond the plan:**
  - `ApiError.available`, plus `DELETE` and `headers` on `apiSend`. The 409 text and the merge key need them, and the session still owns `Authorization`.
  - `hideAddToCart` for the admin preview. Without it, an admin preview would write to the admin's own cart.
  - `tests/jest-setup.js`. It mocks only AsyncStorage (with the package's own in-memory mock) and expo-crypto (node `randomUUID`). Neither mock can turn a failing assertion green: both modules have no JS implementation under Jest, and both store and hook tests run `AsyncStorage.clear()` in `beforeEach`.
  - All of these are accepted.

### 5. Packages — NOTE
The lock resolves exactly `zustand 5.1.0`, `@react-native-async-storage/async-storage 2.2.0` (pulling one transitive, `merge-options`) and `expo-crypto 58.0.7`. The lock root equals `package.json`, and nothing else was added. However, `package.json` says `"zustand": "^5.1.0"` and `"expo-crypto": "~58.0.7"`, while every other entry, and async-storage, is pinned exactly. `npm ci` follows the lock, so nothing drifts today. Pin both to exact versions the next time you touch the file.

### 6. Changed tests — NOTE (Confirmed, none weakened)
- `ProductDetailView.test:106`: the absence test is replaced by a presence test, as planned (owner, app ctx §4.3).
- `ProductCard.test`: both old assertions are kept. The new case asserts two sibling links and no link nested inside the product link.
- `CatalogSchemaTests`: three tables added. `routes.test`: the cart row moves from stub to built.
- Three `IInventoryReader` fakes gain the new member. `ExpectedModules` adds `Cart`, which is stricter.
- LoginForm and RegisterForm: one new notice test each. Nothing was removed.

### 7. Hard rules and boundaries — NOTE (Confirmed)
- **Prices:** server-side only. `CartViewBuilder` re-prices from the Catalog snapshot, and the client does no price arithmetic for the server cart.
- **Lock order:** `carts` → `cart_items` → `idempotency_keys` (`CartStore.EnsureAndLockAsync`, then `keys.FindAsync`/`Add` in the same transaction).
- **No external call inside a transaction.** Snapshot and availability are DB reads only.
- **Clock and style:** `IClock` everywhere, and no `var` (I ran the hygiene awk by hand over the 30 new C# files).
- **Boundaries:** Cart references only SharedKernel, Persistence and the Contracts projects, and every type is internal except `CartModule`.
- **Build files:** the Dockerfile gains 2 COPY lines, `Ecommerce.slnx` gains 3 projects, and Marco reported `VERIFY PASS (docker)`.
- **Migration:** additive only (3 CreateTable, 2 CreateIndex).
- **Anonymous types** appear only in EF `HasKey`/`HasIndex` and in test request bodies. Both are existing precedent.

### 8. Small — NOTE
- **Shared device across accounts.** If user A's merge commits but the response is lost, and user B then signs in on the same device, B's merge gets a 409, the key is dropped, and the same lines are merged again into B. This needs a shared device and a lost response. Track it with the guest-cart risks, not in this slice.
- **The `hygiene` gate gap from Slice 2 is still open.** The awk step stops on the deleted stub, which clears once Slice 2 is committed. Your new files were clean on my manual run.

**What was good:**
- The merge-key lifecycle is right in every corner I tried. Any edit drops the key, a failure keeps it, a 409 replaces it, and the device cart clears only on a 200.
- You found that your own lock test was vacuous and explained why. That is exactly the kind of finding a mutation check exists to catch.

## Round 2 — Rob → Marco
Verdict: BLOCKED (gate only, environment) · verify.sh (Release): `VERIFY FAIL: test`

No source, test, doc or build file has changed since round 1, and nothing is staged. This round was only the Release gate.

**1. Release gate — Not yet.** The PID 51052 lock is gone, and the Release run now gets through everything except the integration tests:
- `build`: `0 Error(s)|0 Warning(s)`.
- `drift`: "No changes have been made to the model since the last migration."
- `secrets`, `hygiene`, `expo-check`, `tsc`, `jest` (`61 passed / 374 passed`) and `export` all pass.
- Unit suites: Inventory 19, Cart 19, Catalog 166, Orders 157, Identity 73 and Architecture 25, all passing.

The integration suite reports `Passed: 0, Total: 261`. Every case fails with `DockerUnavailableException: Failed to connect to Docker endpoint at 'npipe://./pipe/docker_engine'`, and `docker info` cannot find `dockerDesktopLinuxEngine`. Docker Desktop is not running, so Testcontainers cannot start Postgres. This is the environment, not the code: the same 261 tests passed in my round-1 Debug run.
**What I want:** start Docker Desktop (owner's machine; I did not start it), then re-run `bash scripts/verify.sh` in Release and quote `VERIFY PASS (…)`. Nothing else is open, and that line turns this into PASS.

The round-1 notes stand unchanged: replay records stay additive-only or switch to the HTTP body (Slice 4), pin zustand/expo-crypto exactly, the shared-device merge edge, and the hygiene awk gap until the Slice 2 commit.

## Round 3 — Rob → Marco
Verdict: PASS · verify.sh (Release): `VERIFY PASS (secrets hygiene build test drift expo-check tsc jest export)`

**1. Release gate: Confirmed.** No file has changed since round 2, and nothing is staged. With Docker running, every step passes:
- Build: `0 Error(s)|0 Warning(s)`.
- Unit tests: Cart 19, Inventory 19, Identity 73, Orders 157, Catalog 166, Architecture 25.
- Integration: Api.IntegrationTests 261.
- Jest: `61 passed / 374 passed`. Drift is clean.

The round-1 notes stay open as non-blocking:
- For Slice 4, keep the replay records additive-only or store the HTTP body.
- Pin zustand and expo-crypto to exact versions.
- The shared-device merge edge.
- The hygiene awk gap, which closes once Slice 2 is committed.
