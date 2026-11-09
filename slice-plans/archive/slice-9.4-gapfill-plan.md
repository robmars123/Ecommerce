**Read:** app-context row 9.4 (line 103) · arch §3.4 (rules: thin controllers, no cross-module DbSet, `IClock`) · §6.2 · §7.2 `categories` row (07-data-model.md line 32) · §9 row `POST /admin/categories · PUT … · DELETE …` (09-api.md line 23) · §13.2 row "Category edit and delete" (grep, don't read the log) · quality-bar §1, §2, §4.
Code to read before editing: `Catalog/Api/Admin/AdminCategoriesController.cs`, `AdminRequests.cs`, `AdminValidators.cs` (`CreateCategoryRequestValidator`), `AdminResponses.cs` (`CategoryResponse`), `Catalog/Application/Admin/AdminCategoryService.cs`, `AdminPorts.cs` (`ICategoryWriteStore`), `AdminProductResults.cs` (`AdminResult<T>`, `CategoryView`), `AdminProductService.cs` (`SaveAsync` only), `Catalog/Domain/Category.cs`, `Catalog/Infrastructure/ProductWriteStore.cs` (`CategoryWriteStore`), `CatalogModule.cs`, `Ecommerce.Persistence/EfUnitOfWork.cs`, `SharedKernel/Persistence/UniqueConstraintViolationException.cs`; tests `Ecommerce.Catalog.Tests/Application/{AdminFakes,AdminCategoryAndCriteriaTests}.cs`, `Domain/CategoryTests.cs`, `Api.IntegrationTests/Catalog/Admin/{AdminSamples,AdminProductApiTests}.cs` (patterns only).

# Gap-fill 9.4 — Category edit and delete (backend API)

Deliverable: app-context **9.4**. Builder: **Marco**. Mode: Autonomous. Out of scope: 9.7 (admin categories screen), any frontend change, reparenting.

## Risk tier — Low

No money, stock, payment, idempotency or order state transition. The only auth guard is the existing class-level `[Authorize(Policy = AuthorizationPolicies.Admin)]` on `AdminCategoriesController`; the new actions **must live in that controller** so they inherit it unchanged. Review = Marco's tests + full `verify.sh` + Rob reads the diff (CLAUDE.md §5). **No mutations.** Rob still reads the shared `EfUnitOfWork` change line by line, since every module saves through it.

## 1. Decisions (recorded in §13.2 "Category edit and delete"; §9 row updated — both by Rob, done)

| # | Decision | Reason | Alternative rejected |
|---|---|---|---|
| D1 | `PUT /api/v1/admin/categories/{slug}` body `{ slug, name }` → 200 `CategoryResponse { slug, name, parentSlug }`. `DELETE /api/v1/admin/categories/{slug}` → 204, empty body | Categories expose no id anywhere (`CategoryResponse`, `GET /categories`); slug is the admin's handle, same as POST | Routing by GUID (admins can't see it; would need a new admin GET) |
| D2 | Slug and name are editable; slug stays unique (`ux_categories_slug` → 409, field `slug`). Saving the same slug is fine | Every reference is by id (`products.category_id`, `categories.parent_id`), so a slug edit moves nothing; typos must be fixable. Old `?category=` links return 200 empty per the catalog contract | Immutable slug |
| D3 | **Parent is fixed at create.** `UpdateCategoryRequest` has no `parentSlug`; a sent `parentSlug` is an unmapped JSON member → 400 (global `UnmappedMemberHandling.Disallow`, `Program.cs:38`) | Reparenting turns the one-level rule into a cross-row check-then-act race (A moves under B while C is created under A) that needs a tree lock. A fixed parent removes the race entirely | Reparenting + transaction-scoped advisory "category tree" lock on create and edit |
| D4 | Delete is **restricted**: 409 while any product (active or not) or any subcategory references the row. The guard is the existing `ON DELETE RESTRICT` FKs; **no pre-check query** | The FK is atomic: a product saved concurrently can never be orphaned. A count query is TOCTOU and would still need the FK as backstop | Cascade; "move products to X" on delete; pre-delete `COUNT` |
| D5 | New `SharedKernel.Persistence.ForeignKeyViolationException(string constraintName)` (same shape as `UniqueConstraintViolationException`); `EfUnitOfWork.Translate` maps SqlState `23503` in both the `DbUpdateException{Inner: PostgresException}` and bare `PostgresException` arms | Application code must not see EF/Npgsql types (EfUnitOfWork doc comment); the service needs the constraint name to pick the message | Catching `DbUpdateException` in Catalog (breaks the layering); `ExecuteDeleteAsync` (bypasses the unit of work and its translation) |
| D6 | Delete 409 bodies, via `AdminResult.Conflict`: `fk_products_category_id` → field `products`, "This category still has products. Move them to another category first." · `fk_categories_parent_id` → field `subcategories`, "This category has subcategories. Delete them first." Any other FK name is **rethrown** | Distinct field keys let 9.7 and the tests tell the two cases apart; swallowing unknown constraints would hide bugs | One generic 409 message |
| D7 | Races newly reachable because delete exists: a product create/update whose category is deleted mid-request → `AdminProductService.SaveAsync` catches FK `fk_products_category_id` → `Invalid.For("categorySlug", "That category does not exist.")` (existing message). A category create whose parent is deleted mid-request → `AdminCategoryService.CreateAsync` catches FK `fk_categories_parent_id` → `Invalid.For("parentSlug", "That parent category does not exist.")` (existing message) | Same answer the non-racing request gets; without it these paths return 500 | Leaving them as 500 |
| D8 | No `version` / optimistic concurrency: last write wins | Two short fields; a `version` column means a migration and a body change for no v1 consumer | Mirroring product D12 |
| D9 | Domain: `Category.Rename(string slug, string name)`; extract the slug and name checks from `Create` into one private static `Validate(slug, name)` used by both (throws `DomainException`, same messages) | One rule set; no duplicate checks | Setting properties from the service |
| D10 | Validation: `UpdateCategoryRequest(string Slug, string Name)` + `UpdateCategoryRequestValidator` with the same slug and name rules as create. Share them by moving the two rules into a small `internal static class CategoryFieldRules` used by both validators (pattern: `ProductFieldRules.AddDetailRules`) | Same 400 messages for create and edit | Copy-pasting the rules |
| D11 | Controller stays thin: one private `ToActionResult(AdminResult<CategoryView> result, Func<CategoryView, IActionResult> onOk)` holding the existing Invalid / Conflict / NotFound mapping, used by Create (201), Update (200), Delete (204) | Three consumers; Create's switch is moved, not duplicated | A third copy of the switch |
| D12 | `DeleteAsync` returns `AdminResult<CategoryView>` (Ok carries the deleted category; controller ignores it, returns 204) | Reuses the existing result type; no new unit type | A new `AdminResult` non-generic variant |

No migration, no package, no new project reference, no frontend.

## 2. Files and contracts

### SharedKernel / Persistence
- **New** `backend/src/Ecommerce.SharedKernel/Persistence/ForeignKeyViolationException.cs` — `public sealed class ForeignKeyViolationException(string constraintName, Exception? inner = null) : Exception(...)`, `ConstraintName` property, doc comment "A foreign key rejected a write or delete".
- `backend/src/Ecommerce.Persistence/EfUnitOfWork.cs` — `private const string ForeignKeyViolation = "23503";` + two `Translate` arms mirroring the unique ones. Nothing else in the file changes.

### Catalog domain
- `Domain/Category.cs` — `Rename(string slug, string name)` + private static `Validate` (D9). `Create` keeps its self-parent check.

### Catalog application
- `Application/Admin/AdminPorts.cs` — `ICategoryWriteStore` gains:
  - `Task<Category?> GetForUpdateAsync(string slug, CancellationToken ct);` (tracked)
  - `Task<string?> FindSlugByIdAsync(Guid id, CancellationToken ct);` (`AsNoTracking`, for the response `parentSlug`)
  - `void Remove(Category category);`
- `Application/Admin/AdminProductResults.cs` — add `internal sealed record UpdateCategoryCommand(string CurrentSlug, string Slug, string Name);` beside `CreateCategoryCommand` (put it wherever `CreateCategoryCommand` lives).
- `Application/Admin/AdminCategoryService.cs`:
  - `public const string ParentForeignKey = "fk_categories_parent_id";` `public const string ProductsForeignKey = "fk_products_category_id";`
  - `UpdateAsync(UpdateCategoryCommand, ct)`: trim inputs → `GetForUpdateAsync(currentSlug)` null → `NotFound` → `Rename` (DomainException → `Invalid.For("category", msg)`, as Create) → save (unique slug → `Conflict("slug", …)`, existing message) → `Ok(new CategoryView(slug, name, parentId is null ? null : await FindSlugByIdAsync(parentId)))`.
  - `DeleteAsync(string slug, ct)`: `GetForUpdateAsync` null → `NotFound` → `Remove` → save; catch `ForeignKeyViolationException` `when` name is one of the two constants → `Conflict` per D6. Others propagate.
  - `CreateAsync`: add the D7 catch (`ParentForeignKey` → `Invalid.For("parentSlug", …)`).
- `Application/Admin/AdminProductService.cs` — `SaveAsync`: add one catch arm per D7. Reference the constant as `AdminCategoryService.ProductsForeignKey` (same module, internal).

### Catalog infrastructure
- `Infrastructure/ProductWriteStore.cs` (`CategoryWriteStore`) — implement the three members. `GetForUpdateAsync` = tracked `FirstOrDefaultAsync(c => c.Slug == slug)`.

### Catalog API
- `Api/Admin/AdminRequests.cs` — `internal sealed record UpdateCategoryRequest(string Slug, string Name);`
- `Api/Admin/AdminValidators.cs` — `CategoryFieldRules` + `UpdateCategoryRequestValidator` (D10); `CreateCategoryRequestValidator` uses the shared rules (messages unchanged).
- `Api/Admin/AdminCategoriesController.cs` — inject `IValidator<UpdateCategoryRequest>`; `[HttpPut("{slug}")] Update(string slug, UpdateCategoryRequest request, ct)`, `[HttpDelete("{slug}")] Delete(string slug, ct)`; `ToActionResult` helper (D11). No logic beyond validate → call → map.
- `CatalogModule.cs` — register `IValidator<UpdateCategoryRequest>, UpdateCategoryRequestValidator`.

### Docs (Rob, done)
- `docs/architecture/09-api.md` line 23 — PUT/DELETE added.
- `docs/architecture/13.2-decision-log.md` — row "Category edit and delete".
- App-context 9.4 status/slice cell: coordinator after PASS (Marco does not edit docs).

## 3. Build order

1. **Test-first (quote red):** integration IC8 and IC9 (§4) — before any production change they fail (no DELETE route → 405). Then add the exception + `EfUnitOfWork` arms + service/controller delete path → green. Also run them once with the D6 catch removed and quote the 500 (proves the translation is what makes it 409); restore from a scratch copy, `cmp`.
2. Unit tests for `ForeignKeyViolationException` mapping in the service (fake store/unit of work) red → green.
3. Domain `Rename` + `Validate` extraction; domain tests.
4. Update path (store, service, validator, controller), its unit + integration tests.
5. D7 catches + their unit tests.
6. `bash scripts/verify.sh 2>&1 | tail -n 30`; quote the `VERIFY` line in `slice-9.4-gapfill-marco-report.md`.

## 4. Required tests

No §11.2 numbered or F test belongs to 9.4 (no §12.3 row names it). These are the slice's own tests; all are required for PASS.

### Integration — **new** `backend/tests/Ecommerce.Api.IntegrationTests/Catalog/Admin/AdminCategoryApiTests.cs`
Use `IdentityTestEnv.SendAsync(HttpMethod.Put|Delete, …)`, `AdminSamples.AdminTokenAsync`, `AdminSamples.CreateCategoryAsync`, `AdminSamples.CreateProductAsync`. Add a `CategoryUrl(slug)` helper to `AdminSamples` only if two or more tests use it.
- **IC1 happy PUT** — create `shirts` + a product in it; `PUT /shirts {slug:"tops", name:"Tops"}` → 200 `{slug:"tops", name:"Tops", parentSlug:null}`; `GET /api/v1/categories` lists `tops`, not `shirts`; `GET /products?category=tops` returns the product; `?category=shirts` → 200 empty.
- **IC2 PUT child** — child `linen` under `shirts`; rename child → 200 with `parentSlug:"shirts"`; parent unchanged.
- **IC3 auth** — PUT and DELETE: no token → 401; Customer token → 403; Support token → 403 (Admin-only policy). Row unchanged after each.
- **IC4 400** — PUT: invalid slug (`"Bad Slug"`), empty name, name of 101 chars, body containing `parentSlug` (unmapped member). Errors keyed by field. Row unchanged.
- **IC5 404** — PUT and DELETE on an unknown slug → 404 ProblemDetails.
- **IC6 409 PUT** — `shirts` and `pants`; `PUT /pants {slug:"shirts"}` → 409, error key `slug`; both rows unchanged.
- **IC7 happy DELETE** — empty category → 204; gone from `GET /categories`; second DELETE → 404.
- **IC8 ★ DELETE with products** — category with one **inactive** product (unpublish it, or create with `isActive:false`) → 409, error key `products`; category still listed; product still readable via `GET /admin/products/{id}`.
- **IC9 ★ DELETE with subcategory** — parent with child → 409, key `subcategories`; DELETE child → 204; DELETE parent → 204.
- **IC10 create still works** — existing POST happy + duplicate 409 still green after the controller refactor (re-run of the existing create tests is enough if they exist in `AdminProductApiTests`; otherwise add one POST happy + one 409 here).

### Unit — `backend/tests/Ecommerce.Catalog.Tests`
- `Domain/CategoryTests.cs`: `Rename` sets slug and name; invalid slug → `DomainException`; blank / 101-char name → `DomainException`; `Create` messages unchanged.
- `Application/AdminCategoryAndCriteriaTests.cs` (extend the fake category store with the three new members; use `AdminFakeUnitOfWork.FailOnSave`):
  - Update unknown → `NotFound`; update ok → `Ok` with the parent's slug; unique `ux_categories_slug` on save → `Conflict("slug")`.
  - Delete unknown → `NotFound`; FK `fk_products_category_id` → `Conflict("products")`; FK `fk_categories_parent_id` → `Conflict("subcategories")`; FK with another name → exception propagates (`Assert.ThrowsAsync`).
  - Create with FK `fk_categories_parent_id` on save → `Invalid` keyed `parentSlug`.
- `Application/AdminProductServiceTests.cs`: create and update with FK `fk_products_category_id` on save → `Invalid` keyed `categorySlug`.
- Validator tests beside existing validator tests (if none exist for categories, put them in `AdminCategoryAndCriteriaTests.cs`): `UpdateCategoryRequestValidator` accepts a valid body; rejects bad slug, blank name, 101-char name with the create messages.

### Mutations
None (Low tier, §5).

## 5. Risks

- **Shared `EfUnitOfWork` change.** Any module whose save hits an FK violation now gets `ForeignKeyViolationException` instead of `DbUpdateException`. Grep shows no production code catches `DbUpdateException`/`PostgresException`; tests that assert raw `DbUpdateException` call `env.Db.SaveChangesAsync` directly, not the unit of work, so they are unaffected. Do not change those tests.
- **Constraint names are the contract.** D6/D7 key on `fk_products_category_id` and `fk_categories_parent_id` from `ProductConfiguration`/`CategoryConfiguration`. Use the constants; IC8/IC9 hit the real names.
- **Dev seeder** inserts categories missing by slug, so renaming or deleting a seeded category in a dev DB re-creates it on the next start. Development-only; accepted, no change.
- **Old category links** (`?category=old-slug`) return 200 empty after a rename — documented in D2/§13.2; no redirect table in v1.
- **Unmapped `parentSlug` → 400** relies on the global JSON setting; IC4 pins it so a future relaxation is caught.
