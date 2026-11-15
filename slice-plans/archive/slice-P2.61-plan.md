# Slice P2.61 — Images endpoint to Minimal API (plan, Rob)

Tracker row: P2.61 · Images endpoint to Minimal API · Catalog. Tier **Low** (anonymous read-only GET): Marco's tests + full `verify.sh`, Rob reads the diff, no mutations.

**Read:** `slice-plans/minimal-api-migration-plan.md` §2.1 (D1, D3, D4, D8), §3, §4 standard steps · this plan · `Catalog/Api/ImagesController.cs` · `Catalog/Api/ProductEndpoints.cs` (pattern) · `Catalog/CatalogModule.cs:64–69` · `Application/ImageFileService.cs` · `tests/Ecommerce.Api.IntegrationTests/Catalog/ImageFileApiTests.cs` · `EndpointInventoryTests.cs:38` · `Identity/AuthorizationTests.cs:93`. Nothing else.

(`Catalog/` = `backend/src/Modules/Catalog/Ecommerce.Catalog/`.)

## 1. Decisions

| Decision | Reason | Alternative rejected |
|---|---|---|
| `app.MapGet("api/v1/images/{**key}", Get).AllowAnonymous()` — no group | D4 single-route form; RawText is exactly today's `api/v1/images/{**key}` (inventory line 38, II11 line 93) | `MapGroup("api/v1/images").MapGet("{**key}")` (needless group); `"/{**key}"`, `{*key}` (changes RawText → inventory red; `*` also escapes `/` in generated links) |
| Handler parameter is **`string? key`** | The catch-all matches an empty tail: `/api/v1/images` and `/api/v1/images/` reach the handler with no value. MVC bound `null` → `StorageKey.IsValid(null)` false → 404. A non-nullable `string key` in a minimal API is a *required* parameter → bare **400** before the handler runs. Behaviour change; this is the trailing-slash catch-all gotcha | `string key` |
| Handler returns `Results<FileContentHttpResult, ProblemHttpResult>`; 404 = `TypedResults.Problem(statusCode: StatusCodes.Status404NotFound)` (no custom title) | Same as P2.60's explicit 404. Defaults give `type` rfc9110 §15.5.5 + `title` "Not Found", same as `[ApiController]` `NotFound()` today; `traceId`/`correlationId` through `IProblemDetailsService`. Pinned in step 1 | `TypedResults.NotFound()` (body then depends on `UseStatusCodePages` running — implicit) |
| 200 = `TypedResults.File(image.Content, image.ContentType, lastModified: image.CreatedAt, entityTag: new EntityTagHeaderValue($"\"{Convert.ToHexStringLower(image.Sha256)}\""))` | Same inputs as MVC `File(...)`; same file-result helper does `If-None-Match` → 304 empty body and `If-Modified-Since`. Range processing stays off (MVC default `false` = minimal default `false`) | Writing bytes to the response by hand |
| `Cache-Control` and `X-Content-Type-Options` set on an `HttpResponse response` parameter **after** the null check, exact strings copied from the controller | A 404 must not carry `immutable, max-age=31536000` (a CDN would cache the miss for a year) | Endpoint filter / setting headers before the lookup |
| Service unchanged: handler calls `[FromServices] ImageFileService images` → `images.GetAsync(key, ct)` once | `StorageKey.IsValid` runs inside the service before storage; bad and unknown keys both come back `null` → same 404 | Injecting `IImageStorage` in the handler (bypasses `IsValid`) |
| Read path unchanged | `PostgresImageStorage.GetAsync` is already one query: `WHERE storage_key = @key` on the PK (`pk_image_blobs`), `LIMIT 1`, `AsNoTracking`, projecting only the 4 columns the response needs (`content_type, content, sha256, created_at`). No N+1, no over-fetch | — |
| File name `Api/ImageEndpoints.cs` | D1, singular like `CategoryEndpoints`/`ProductEndpoints` | `ImagesEndpoints` |

## 2. Files

| File | Change |
|---|---|
| `Catalog/Api/ImageEndpoints.cs` | **new** — `internal static class ImageEndpoints`, `public static void Map(IEndpointRouteBuilder app)`, one `private static async Task<Results<FileContentHttpResult, ProblemHttpResult>> Get(string? key, HttpResponse response, [FromServices] ImageFileService images, CancellationToken ct)`. Keep the controller's `<summary>` (public, read-only, immutable bytes) |
| `Catalog/Api/ImagesController.cs` | **delete** (same change — both mapped → `AmbiguousMatchException`) |
| `Catalog/CatalogModule.cs` | add `ImageEndpoints.Map(app);` after `ProductEndpoints.Map(app);` |
| `tests/.../Catalog/ImageFileApiTests.cs` | **append one new test** (step 1); IF1–IF3 untouched |
| `docs/admin-dashboard-feature-tracker.md:80` | P2.61 → Done |

No migrations, no `*.Contracts`, no packages, no service/storage edits, no doc edits beyond the tracker.

## 3. Build order

1. **Baseline on MVC** (before touching source): `dotnet test backend/tests/Ecommerce.Api.IntegrationTests -c Release --filter "FullyQualifiedName~ImageFileApiTests|FullyQualifiedName~EndpointInventoryTests|FullyQualifiedName~AuthorizationTests"` → quote counts.
2. **Pin (green on MVC, quote it):** add to `ImageFileApiTests` a `[Theory]` `Missing_invalid_or_unknown_image_key_is_a_404_problem` over `"/api/v1/images"`, `"/api/v1/images/"`, `"/api/v1/images/products/nope.png"` (valid shape, unknown key). Assert per path: 404, `application/problem+json`, `type` = `https://tools.ietf.org/html/rfc9110#section-15.5.5`, `title` = `Not Found`, non-empty `traceId`, `correlationId` echoed, and **no** `Cache-Control` containing `immutable`. If either empty-tail path is not 404 on MVC, stop and report the real status — do not adapt the assertion.
3. **Convert:** add `ImageEndpoints.cs`, register in `CatalogModule`, delete the controller.
4. **Prove `string?` matters (one-off, not a mutation):** temporarily make it `string key`, run the new theory, quote the red (expect 400 on the empty-tail rows), restore, `cmp` against a scratch copy.
5. Step-1 filter again → all green, IF1/IF2/IF3 and inventory unedited. Then full `bash scripts/verify.sh 2>&1 | tail -n 30`.

## 4. Required tests

- Existing, **unedited**: IF1 (bytes, `image/png`, `max-age=31536000` + `immutable`, ETag present, `nosniff`, 4 bad/unknown keys → 404), IF2 (`If-None-Match` → 304, empty body), IF3, `EndpointInventoryTests` (`GET api/v1/images/{**key} | - | anon | -`), II11 anonymous list.
- New (step 2, green on MVC first): the 404-shape / empty-tail theory above.
- No §11.2 numbered test is owned by this row.

## 5. What Rob will check in the diff

- Route string byte-for-byte `api/v1/images/{**key}`; `.AllowAnonymous()`; no group; no `RequireAuthorization`.
- `key` is `string?`.
- Exactly one `images.GetAsync` call; nothing else touches the DB (no `IImageStorage`, no `DbContext`, no second lookup for the 304 path — the ETag comes from the same row).
- Headers set only on the found path, same literal strings; `ETag` built exactly as before (quoted lowercase hex SHA-256); `lastModified` passed; no `enableRangeProcessing: true`; no `fileDownloadName` (would add `Content-Disposition: attachment`).
- `ImagesController.cs` gone; no leftover `using Microsoft.AspNetCore.Mvc` beyond `[FromServices]`; no `var`; no lambdas for the handler (D3).
- New test is additive; IF1–IF3 diff is empty.

## 6. Risks

| Risk | Guard |
|---|---|
| Empty catch-all → 400 instead of 404 | `string? key` + step-2 theory + step-4 red |
| RawText drift (`/{**key}`, `{*key}`, group + `""`) | Inventory line 38 + II11 |
| 404 body shape drift (`[ApiController]` problem vs bare 404) | Step-2 theory asserts type/title/ids |
| 404 cached as immutable | Headers after null check; theory asserts no `immutable` on 404 |
| 304 path loses ETag/headers | IF2 unedited |
| Invalid key reaches storage | Service unchanged; handler only calls `ImageFileService` (diff check) |

## Amendment 1 — empty tail is 400 on MVC (Rob, 2026-11-14)

Marco's step-2 run on MVC: `/api/v1/images` and `/api/v1/images/` → **400** (`[ApiController]` treats non-nullable `string key` as required; binding fails before the action). Unknown key → 404 as planned. My §1 row 2 premise ("MVC bound null → 404") was wrong.

**Decision: (a) keep today's 400.** Reason: the slice is "behaviour unchanged" (migration plan D1) and the empty tail has no client. Rejected (b) 404: a behaviour change not in the tracker row. This supersedes §1 row 2, §3 steps 2 and 4, §4 "New", and §6 row 1. Everything else stands.

**Pin (step 2, replaces Marco's single theory — split it in two):**
1. `Unknown_image_key_is_a_404_problem` — `[Fact]` on `/api/v1/images/products/nope.png`, the assertions Marco already wrote (404, problem+json, rfc9110 §15.5.5, `Not Found`, traceId, correlationId, no `immutable`).
2. `Empty_image_key_is_a_400_validation_problem` — `[Theory]` over `"/api/v1/images"`, `"/api/v1/images/"`. **First capture on MVC**: dump status, `Content-Type`, `Cache-Control` and the raw body once (temporary `ITestOutputHelper` line, removed before commit) and quote it in the report. Then assert exactly what MVC sends; expected (confirm against the dump, do not guess past it):
   - status 400; media type `application/problem+json`;
   - `type` = `https://tools.ietf.org/html/rfc9110#section-15.5.1`; `title` = `One or more validation errors occurred.`; `status` = 400;
   - `errors` has exactly one property, `key`, whose array is exactly the MVC message (expected `The key field is required.`);
   - non-empty `traceId`; `correlationId` echoed **if** MVC emits it (assert whatever the dump shows — present-and-equal or absent);
   - no `Cache-Control` containing `immutable`.
   Green on MVC, quoted, before step 3. If the dump differs from the expected list, the dump wins; note the difference in the report.

**Code (replaces §1 row 2):** keep the handler parameter **`string? key`** so the binder never fails (a minimal-API required-param failure writes its own 400 — no `errors` member, different title — and would turn the pin red). First line of the handler:
`if (string.IsNullOrEmpty(key)) return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["key"] = ["The key field is required."] });`
(use the exact message from the dump). `TypedResults.ValidationProblem` defaults to the same `type`/`title` as MVC and writes through `IProblemDetailsService`, so traceId/correlationId come from the same customisation as P2.60's 404. Return type becomes `Results<FileContentHttpResult, ValidationProblem, ProblemHttpResult>`. The check sits before the service call and before headers are set. Plain `if`, no filter, no helper.

**Step 4 (replaces):** temporarily delete the `IsNullOrEmpty` branch, run the 400 theory, quote the red (expected: empty-tail rows go 404 via the service's `IsValid(null)` — or 400 with a different body; quote whatever it is), restore from scratch copy, `cmp`.

**Rob will check:** the 400 theory's assertions equal the quoted MVC dump; IF1–IF3 diff still empty; the 404 fact unchanged from Marco's assertions.
