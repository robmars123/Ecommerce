# Slice 9.5 gap-fill — Review thread

## Round 1 — Rob → Marco
Verdict: PASS · verify.sh: `VERIFY PASS (secrets hygiene build test drift expo-check tsc jest export)` — build 0 warnings, jest 1103/1103

The diff is the plan, nearly line for line: one Admin-only upload action inheriting class-level auth, signature-only format detection, a size guard in the service, a port with a blank-config 503, and nothing written to Postgres. I re-ran M1–M3 myself; every guard has a test that bites.

### Mutations (re-run by me; scratch copy → break → red → restore → `cmp` identical)
- **M1 auth** — `[AllowAnonymous]` on `Upload`: IU3 `Expected: Unauthorized  Actual: OK`, Failed 1/1. Restored, cmp OK.
- **M2 signature** — `Detect` returns `Png` for any ≥ 1 byte: `ImageFormatTests` Failed 7/8; IU4 `wave`, `gif`, `svg`, `html` FAIL (4/7). Restored, cmp OK.
- **M3 size** — `Length > MaxBytes` check disabled: service unit `A_file_over_the_limit_…` FAIL (1/7); IU4 `toobig` FAIL (1/7). Restored, cmp OK. This also proves the `MaxBytes + 1` file reaches the service, so the framework backstop doesn't mask the guard.

### Checks you asked me to make
- **Secrets / real endpoints:** none. `appsettings.json` `Catalog:Images:Storage` has five `""` keys; nothing in `appsettings.Development.json`, `compose.yaml` or `.env.example`; tests use `storage.example.test` and `bucket-x`. The `secrets` step passed.
- **Blank → 503 before any network call:** `SpacesImageStorage.PutAsync` throws `ImageStorageNotConfiguredException` before touching `_client.Value`, and the client is `Lazy`, so with blank config it is never built. Unit test plus IU7 (real adapter, 10 s cap) prove it.
- **Partial config fails startup:** `IsValid` = all blank, or all set with an absolute https `ServiceUrl`; wired with `ValidateOnStart`. `CatalogImageStorageOptionsTests` covers one-alone, `http://` and non-URL values.
- **No external call inside a tx (§3.4):** the service does one `AsNoTracking().AnyAsync` read and then the put. There is no unit of work or `BeginTransaction` anywhere on this path, and no request-wide transaction filter exists in the host.
- **Signature:** JPEG `FF D8 FF`, PNG 8-byte signature, WebP `RIFF`…`WEBP`, and nothing else. SVG, `<?xml`, HTML, GIF, PDF, RIFF-WAVE and a damaged PNG are all refused, and the client's type and name are ignored (IU2 sends `text/plain` with `x.exe`). Stored `Content-Type` comes from the detected format.
- **Key from IClock:** `Guid.CreateVersion7(clock.UtcNow)`. The unit test parses the v7 timestamp and asserts it equals `AdminEnv.T0` in ms, and that `StorageKey.IsValid` holds.
- **Boundaries:** `AWSSDK.S3` 4.0.104.11 is pinned centrally and referenced only by `Ecommerce.Catalog.csproj`. All new types are `internal`, and `ArchitectureTests` 25/25 pass.
- **Changed tests:** `AdminFakes.cs` only, one additive member. No existing test was edited.

### Your deviations
1. **IU5 pins 404, not 415 — Accepted.** I read `Program.cs:131`: the anonymous `MapFallback` matches, so MVC never produces its 415 endpoint. The property that matters still holds: a non-multipart body never reaches the action and nothing is stored. The docs (§9 row, §13.2) don't promise 415, so no doc change. Backlog note added.
2. **IU8 pins 400 under TestServer — Accepted.** The plan said to observe and pin. M3 shows the service is the real guard.
3. **No ILogger in the service — Accepted.** `LayerRuleTests` forbids `Microsoft.Extensions.*` in Catalog Application, and the architecture test wins over my D13 line. Dropping the log loses observability only, not correctness. Backlog note added for a controller-level line.

### 1. Blank-config unit test name overclaims — NOTE (`SpacesImageStorageTests.cs:12`)
**What I saw:** the test is named "…before any client is created", but it only asserts the exception; it can't see `_client.IsValueCreated`.
**Why it's fine for now:** IU7 runs the real adapter with a 10 s cap, and the code throws before `_client.Value` is read. No fix needed this slice.
**Lesson:** a test name is a claim; assert what it says, or name it after what it asserts.

**What was good:** `BuildRequest` as an `internal static` means the ACL, cache header and chunk settings are tested without a network. IU1 goes all the way from upload to `PUT` with the returned key, which proves D2 (attach only through the versioned update) end to end.
