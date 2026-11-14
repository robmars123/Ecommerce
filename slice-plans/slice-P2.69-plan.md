# Slice P2.69 — AdminProducts (with image upload) to Minimal API

Rob, 2026-11-14 · Mode: Autonomous · Tier: **High** (auth, version-checked writes, upload limits) · Module: Catalog · Status: READY

**Read:** `slice-plans/minimal-api-migration-plan.md` §2–§4 · this plan · `docs/architecture/03-principles-hard-rules.md:40` · `06-backend-modules.md:63–69` · `13.2-decision-log.md` (grep `Minimal API migration` only) · `Catalog/Ecommerce.Catalog/Api/Admin/AdminProductsController.cs` (all) · `Api/Admin/AdminCategoriesEndpoints.cs` (the pattern) · `Api/ProductEndpoints.cs` · `Admin/Ecommerce.Admin/Api/QueryValues.cs` (to copy) · `Api/Admin/AdminValidators.cs:101–127, 155–175` · `CatalogModule.cs:64–70` · tests: `Catalog/Admin/AdminProductApiTests.cs`, `Catalog/Admin/AdminProductImageApiTests.cs`, `Catalog/ProductQueryBindingTests.cs`, `Catalog/Admin/AdminCategoryContractTests.cs:1–60` (helper pattern), `EndpointInventoryTests.cs:76–103` (EndpointDataSource pattern). Nothing else.

**Split?** No. The upload is one handler plus four conventions on one route; splitting it off would leave a half-converted controller and a second review for about 20 lines. The plan stays under the cap.

---

## 1. Decisions

| # | Decision | Reason | Alternative rejected |
|---|---|---|---|
| P1 | New `Api/Admin/AdminProductsEndpoints.cs`. One group `app.MapGroup("api/v1/admin").RequireAuthorization(AuthorizationPolicies.Admin)` with 8 leaf routes: `products`, `products/{id:guid}`, `products/{id:guid}/preview` (GET); `products` (POST); `products/{id:guid}` (PUT); `products/{id:guid}/publish`, `products/{id:guid}/unpublish`, `products/{id:guid}/images` (POST). Controller deleted in the same change | D1/D4/D5. RawText equals the inventory rows exactly | `MapGroup("api/v1/admin/products")` + `MapGet("")` (trailing slash, D4) |
| P2 | Handlers are named `private static async` methods, mirroring the controller actions 1:1 (`List`, `Get`, `Preview`, `Create`, `Update`, `Publish`, `Unpublish`, `Upload`). Each makes **exactly one** service call, the same one the action made. Body DTOs use `[FromBody]`; services and validators use `[FromServices]` | D3. Same query count as today, provably | Lambdas |
| P3 | One private mapper `ToResult<T, TOk>(AdminResult<T> result, Func<T, TOk> onOk) where TOk : IResult` returns `Results<TOk, ValidationProblem, ProblemHttpResult>` with **explicit** arms: `Ok` → `onOk`; `NotFound` → `TypedResults.Problem(statusCode: 404, title: "Product not found")`; `Invalid` → `TypedResults.ValidationProblem(invalid.Errors)`; `Conflict` → `TypedResults.Problem(new HttpValidationProblemDetails({ [field ?? ""] = [message] }) { Status = 409, Title = "Conflict" })`; `_` → `throw new InvalidOperationException("Unhandled admin result.")` | Same arms as the controller's `Map` (it threw on unknown, it didn't 404). Generic over `T` because three views go through it (product, preview, uploaded image). The 409 keeps `errors.version`, which `ProductForm` reads for stale edits | Sharing one mapper with `AdminCategoriesEndpoints` (different 404 title, committed file, would need a title parameter: an abstraction for two callers in two files). Categories' `_ => 404` |
| P4 | `Create`: `Created($"/api/v1/admin/products/{v.Id}", v.ToResponse())` through `ToResult` | IA1 asserts the Location header (categories have none; products do) | `Created((string?)null, …)` copied from categories |
| P5 | **List query binding**: all five query values bound as `[FromQuery] string[]?`, read with `?.FirstOrDefault()`; one comment line says why. Two stages, as Admin C7: (1) `QueryValues.ReadInt` for `page`, `pageSize`, any error → `ValidationProblem` and stop; (2) `AdminProductListCriteria.Create(...)` unchanged, invalid → `ValidationProblem(parsed.Errors!)` | MVC binds the **first** value of a repeated key; minimal joins `string` as `"a,b"` (backlog 110). MVC's binding 400 came before the action, so a parse error hides the range errors | `string?` (behaviour change) |
| P6 | **Catalog `QueryValues` now.** New `Api/QueryValues.cs` (`namespace Ecommerce.Catalog.Api`), byte copy of `Admin/Api/QueryValues.cs`. Two consumers: `AdminProductsEndpoints.List` and `ProductEndpoints.List`. `ProductEndpoints` switches **all five** query params to `string[]?` + `?.FirstOrDefault()`, calls `QueryValues.ReadInt`, deletes its private `ReadInt` and `using System.Globalization`. Closes backlog line 110 for Catalog | Same rule Admin used in P2.64 C8: second consumer in one module → one helper, not two byte-identical copies. P2.60 silently changed repeated-key behaviour on the public list; this touch restores MVC's, pinned red-first (B6) | A second private `ReadInt` in Catalog; a cross-module/SharedKernel helper; fixing `ProductEndpoints` in a later slice (it is touched now) |
| P7 | **Upload route conventions**, in this order: `.Accepts<IFormFile>("multipart/form-data")` · `.WithFormOptions(multipartBodyLengthLimit: ImageFormat.MaxBytes + 1)` · `.WithMetadata(new RequestSizeLimitAttribute(ImageFormat.MaxBytes + 64 * 1024))` · `.DisableAntiforgery()` | D11. Today's numbers verbatim. `Accepts` is explicit because the parameter is `IFormFile?` (P8), and inferred accepts metadata for a nullable form parameter can be optional: a POST with **no** Content-Type would then reach the handler (bare 415) instead of today's 404 via the fallback. The explicit, non-optional metadata is added after the inferred one and wins (`GetMetadata` returns the last). `DisableAntiforgery`: a form-file parameter adds antiforgery metadata; with no antiforgery middleware the endpoint throws (500). MVC never validated antiforgery on this bearer-only API, so disabling matches today | `AddAntiforgery` + `UseAntiforgery` (new CSRF surface, tokens no client sends); `[Consumes]` attribute on a lambda |
| P8 | **Upload handler**: `Guid id, IFormFile? file, [FromServices] AdminProductImageService images, CancellationToken ct`. `file is null` → `TypedResults.ValidationProblem({ ["file"] = [<MVC's text, quoted from the step-1 baseline>] })` before the service. Then `await using Stream content = file.OpenReadStream();` and one `UploadAsync` call, as today | D9. MVC's implicit `[Required]` on `IFormFile file` returned 400 `errors.file` before the action (IU4 "none"); a missing file never reached the product-exists query, and it still won't | Non-nullable `IFormFile` (bare 400, no `errors`: IU4 red) |
| P9 | **Validators**: after conversion, if pin IP4 (omitted `variants`/`images`) goes 500, add `.Cascade(CascadeMode.Stop)` to the four `RuleFor(r => r.Variants)` / `RuleFor(r => r.Images)` rules in `CreateProductRequestValidator` and `UpdateProductRequestValidator` (before `.NotNull()`). Nothing else in the validators | D9. MVC's implicit `[Required]` stopped a null list before the action; now the validator sees it, and `.Must(v => v.Count …)` runs after a failed `NotNull` (FluentValidation's default cascade is Continue) → NullReferenceException → 500 | Making the lists nullable in the DTO; a global cascade setting (changes every validator) |
| P10 | **Delete `AdminValidationProblems.ToProblem`** (MVC helper) and its `using Microsoft.AspNetCore.Mvc` if unused; reword the `ToErrors` summary so it no longer `cref`s `ToProblem` | D8: deleted when its last caller converts. This controller is its last caller in Catalog (Identity has its own copy) | Leaving dead code |
| P11 | Unreadable JSON body on create/update: accepted 400 difference already in 13.2 (P2.68). No new pin, no code | Same as categories | Hand-reading the body |

No migrations, no `*.Contracts` change, no packages, no new DI registrations, no doc edits.

## 2. Files

| File | Change |
|---|---|
| `backend/src/Modules/Catalog/Ecommerce.Catalog/Api/Admin/AdminProductsEndpoints.cs` | **new**: P1–P5, P7, P8 |
| `…/Api/Admin/AdminProductsController.cs` | **delete** |
| `…/Api/QueryValues.cs` | **new**: P6 |
| `…/Api/ProductEndpoints.cs` | P6 only: five params → `string[]?`, `FirstOrDefault`, `QueryValues.ReadInt`, delete `ReadInt` + `System.Globalization` using. Nothing else |
| `…/Api/Admin/AdminValidators.cs` | P10; P9 only if IP4 goes 500 |
| `…/CatalogModule.cs` | one line: `AdminProductsEndpoints.Map(app);` after `AdminCategoriesEndpoints.Map(app);` |
| `backend/tests/Ecommerce.Api.IntegrationTests/Catalog/Admin/AdminProductContractTests.cs` | **new**: IP1–IP7 |
| `…/Catalog/ProductQueryBindingTests.cs` | **add** B6 only; B1–B5 unedited |

### Handler contracts (the reviewer checks each)

| Handler | Signature (besides `CancellationToken ct`) | Returns | Calls (exactly) |
|---|---|---|---|
| `List` | `string[]? search, category, status, page, pageSize` (`[FromQuery]`), `AdminProductService products` | `Results<Ok<AdminProductListResponse>, ValidationProblem>` | `products.ListAsync` ×1 (2 SQL: count + page) |
| `Get` | `Guid id`, service | `Results<Ok<AdminProductResponse>, ValidationProblem, ProblemHttpResult>` | `GetAsync` ×1 |
| `Preview` | `Guid id`, service | `…Ok<AdminPreviewResponse>…` | `PreviewAsync` ×1 |
| `Create` | `[FromBody] CreateProductRequest request`, `IValidator<CreateProductRequest>`, service | `Results<Created<AdminProductResponse>, ValidationProblem, ProblemHttpResult>` | validate → `ValidationProblem(validation.ToErrors())`; command built exactly as today; `CreateAsync` ×1 |
| `Update` | `Guid id`, `[FromBody] UpdateProductRequest request`, `IValidator<UpdateProductRequest>`, service | `…Ok<AdminProductResponse>…` | validate; command with `request.Version` as today; `UpdateAsync` ×1 |
| `Publish` / `Unpublish` | `Guid id`, service | `…Ok<AdminProductResponse>…` | `PublishAsync` / `UnpublishAsync` ×1 |
| `Upload` | `Guid id`, `IFormFile? file`, `AdminProductImageService images` | `…Ok<UploadedImageResponse>…` | null check (P8); `UploadAsync` ×1 |

The command-building lines (`VariantInput`, `ImageInput`, `MoneyParsing.Parse`) move verbatim. No `var`, no anonymous types.

## 3. Build order

1. **Baseline on MVC** (no production edits). Run `AdminProductApiTests`, `AdminProductImageApiTests`, `ProductQueryBindingTests`, `EndpointInventoryTests`; quote counts. Also record IU4 "none"'s exact `errors.file[0]` text (for P8).
2. **Step-1 pins**, `AdminProductContractTests.cs` (§4 IP1–IP7). All green on MVC, quoted. If IP6 can't read a value on MVC (an attribute that doesn't expose the metadata interface), write that one assertion in step 4 instead and say so; don't change the expected numbers.
3. **B6 red-first**: add B6 to `ProductQueryBindingTests`; quote the red (today's minimal list joins `"2,abc"` → 400).
4. **Convert**: `QueryValues.cs`; `ProductEndpoints` (P6) → B6 green, B1–B5 green; `AdminProductsEndpoints.cs`; register in `CatalogModule`; delete the controller; P10. Run the four suites + the new file.
5. **P9 only if needed**: if IP4 is 500, quote it, add the cascade, quote green.
6. Full `bash scripts/verify.sh` (one run at a time), `| tail -n 30`, quote `VERIFY`.
7. Mutations (§5), each: scratch copy → break → quote red → restore → `cmp`.

Stop and report (no improvised fix, no test edit) if: any existing test changes status, IU8 answers anything but 400, IP7 changes status, or RawText differs from the inventory.

## 4. Required tests

Existing, **unedited**, all green: IA1–IA7 (`AdminProductApiTests`), IU1–IU6, IU8, IU9 (`AdminProductImageApiTests`), B1–B5, `EndpointInventoryTests.T1`, II10/II11 (`Identity/AuthorizationTests.cs`), `CatalogValidationTests`.

New, in `AdminProductContractTests.cs` (step 1, green on MVC unless marked). Use a local `AssertProblem(status, type)` like `AdminCategoryContractTests` (problem+json, `type`, `traceId`):

| # | Test | Asserts |
|---|---|---|
| IP1 | stale `PUT` | 409, problem+json, type `…rfc9110#section-15.5.10`, title `Conflict`, `errors.version[0]` non-empty, `traceId` |
| IP2 | unknown id on `GET`, `GET …/preview`, `PUT` (valid body), `POST …/publish`, `POST …/unpublish` | each 404 problem, type `…section-15.5.5`, title `Product not found` |
| IP3 | create **and** update with `price.amount = "abc"` | 400, title `One or more validation errors occurred.`, key `variants[0].price.amount` (camelCase) |
| IP4 | create without `variants`; update without `images` | 400, problem+json, `errors` present (D9; may go red after convert → P9) |
| IP5 | list: `?page=abc` → 400 `errors.page[0]` = `The value 'abc' is not valid.`; `?page=abc&pageSize=500` → only `page`; `?status=active&status=bogus` → 200, only active rows; `?page=1&page=abc` → 200 | P5 two stages + first value |
| IP6 | metadata of `POST api/v1/admin/products/{id:guid}/images` from `EndpointDataSource` (as inventory T1): `IRequestSizeLimitMetadata.MaxRequestBodySize == 5308416`; `IFormOptionsMetadata.MultipartBodyLengthLimit == 5242881`; `IAcceptsMetadata` content types `["multipart/form-data"]`, `IsOptional == false`; `GetMetadata<IAntiforgeryMetadata>()?.RequiresValidation != true` | P7. Literal numbers, not `ImageFormat` |
| IP7 | upload `POST` with an admin token and **no** Content-Type (empty body) | today's status (expected 404, quote it), `image_blobs` count 0 |

`ProductQueryBindingTests`: **B6** (test-first, red today) — `/api/v1/products?page=2&page=abc` → 200, `page == 2`.

Write the tests so each runs once per product (create via `AdminSamples.CreateProductAsync`), dispose responses with `using`.

## 5. Mutations (High: one per guard)

| # | Break | Expect red |
|---|---|---|
| M1 | group policy `Admin` → `AdminOrSupport` | inventory T1 + IU3 (support 403) |
| M2 | drop `.WithMetadata(new RequestSizeLimitAttribute(…))` | IP6 |
| M3 | in `ToResult`, `Conflict` arm → `TypedResults.ValidationProblem(...)` (400) | IP1 + IA2 |
| M4 | `Create`: delete the `if (!validation.IsValid)` return | IA1 bad prices (500) + IP3 |
| M5 | drop `.DisableAntiforgery()` | IU1 (500) + IP6 |
| M6 | drop `.Accepts<IFormFile>(…)` | IP7 and/or IP6 `IsOptional` (quote which) |

If M6 stays green (the inferred metadata is already non-optional), quote it and **delete** the `.Accepts` line: it would be dead code; IP6/IP7 still guard the behaviour.

## 6. Review checklist (what Rob checks)

- **Queries per request**: one service call per handler (table §2). List = 2 SQL (count + page, one join for category, variant counts/cheapest in the same statement); no per-row image or category lookup. Upload = 1 exists-query + 1 storage write.
- **One SaveChanges** per write: unchanged, inside the services (`AdminProductService.SaveAsync`); handlers never touch the `DbContext`.
- **Over-fetching**: handler adds none; existing detail-query width is a backlog NOTE, not this slice.
- 409 shape and `errors.version`; Location header on create; 404 title; upload conventions in P7 with today's numbers; `file` null check before the service.
- `ProductEndpoints` diff limited to P6. `AdminValidators` diff limited to P10 (+ P9 if quoted red). No other file touched.
- Changed tests: only the B6 addition and the new contract file.

## 7. Risks

- **IU8 status** (7 MB body): the 400 came from MVC's form-limit path under TestServer. If minimal answers 413 or 500, stop and report; do not edit IU8.
- **Accepts precedence** (P7): if the explicit metadata doesn't win, IP6/IP7 catch it. Stop; no improvised fix.
- **Repeated-key restore on the public list** (P6) is a behaviour change on an anonymous route; B6 is its only proof, and B1–B5 must stay green.
- **Validator cascade** (P9) also changes the *message set* for a null list (one error instead of an exception); IP4 pins status only, which matches what MVC promised.
- Pre-existing over-fetch and edge cases are in the backlog (P2.69 plan notes); out of scope.
