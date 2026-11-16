# Slice P2.83 — Query values in one `[AsParameters]` record per endpoint (D16)

**Read:** `slice-plans/minimal-api-migration-plan.md` §2.1 rows D3, D9, D15, D16 (grep `^| D`) and §3 first paragraph · this plan · the six handlers in the table below (only the `List`/`RecentOrders` method and its `Map`) · nothing else.

Tracker row: P2.83 (`docs/admin-dashboard-feature-tracker.md`). Modules: Catalog, Admin, Inventory, Orders.
**Risk tier: Low** (CLAUDE.md §5). Read-only GET endpoints, no money, stock, auth or state change; the handler bodies and every 400 text stay as they are. Marco's tests + full `verify.sh`; Rob reads the diff; no mutations.

## 1. Scope — handlers with 2+ query values

Found with `grep -rn FromQuery backend/src/Modules --include=*Endpoints.cs` (16 endpoint files; 6 handlers qualify).

| # | Handler (file : method) | Query values today | New record (new file, same folder and namespace) |
|---|---|---|---|
| 1 | `Catalog/Ecommerce.Catalog/Api/ProductEndpoints.cs` : `List` | search, category, sort, page, pageSize | `Api/ProductListQuery.cs` → `ProductListQuery` |
| 2 | `Catalog/Ecommerce.Catalog/Api/Admin/AdminProductsEndpoints.cs` : `List` | search, category, status, page, pageSize | `Api/Admin/AdminProductListQuery.cs` → `AdminProductListQuery` |
| 3 | `Admin/Ecommerce.Admin/Api/AdminCustomersEndpoints.cs` : `List` | search, page, pageSize | `Api/AdminCustomerListQuery.cs` → `AdminCustomerListQuery` |
| 4 | `Admin/Ecommerce.Admin/Api/AdminDashboardEndpoints.cs` : `RecentOrders` | page, pageSize | `Api/RecentOrdersQuery.cs` → `RecentOrdersQuery` |
| 5 | `Inventory/Ecommerce.Inventory/Api/AdminInventoryEndpoints.cs` : `List` | page, pageSize | `Api/InventoryListQuery.cs` → `InventoryListQuery` |
| 6 | `Orders/Ecommerce.Orders/Api/AdminOrderReadsEndpoints.cs` : `List` | page, pageSize, status, from, to, customer, orderNumber | `Api/AdminOrderListRawQuery.cs` → `AdminOrderListRawQuery` |

**Out of scope (one query value, stays a plain parameter per D16):** `AdminDashboardEndpoints` summary handler (`period`), `AdminInsightsEndpoints` both handlers (`period`), `AdminSearchEndpoints` (`q`). No other endpoint file binds query values. Not touched: Cart, Identity, Returns, Projections, Settings, Categories, Images.

## 2. Decisions

| Decision | Reason | Alternative rejected |
|---|---|---|
| Record shape: `internal sealed record <Name>(...)` **positional**, one member per query value, each `[FromQuery] string[]? <PascalName>`, in the order the handler has them today | D16 + D9: same `string[]?` text, so repeated keys still read the first value and garbage numbers/dates still go through `QueryValues.ReadInt/ReadDate` into the same `errors` body | `int?`/`DateOnly?` members (bare 400, D9); init-only property records (longer for no gain) |
| Keep `[FromQuery]` on every member | Explicit source, as D3 does for `[FromServices]`. Without it a GET's array member is inferred, and if it were ever inferred as body the route would get `IAcceptsMetadata` and the D15 convention would turn a GET into a JSON-body route (CT4 would not catch that, it only checks `*/*`) | Relying on inference |
| PascalCase member names (`Search`, `PageSize`, `OrderNumber`), no `Name =` | ASP.NET Core's query collection is case-insensitive, so `?pageSize=` still binds `PageSize`; there is no OpenAPI/ApiExplorer in the host, so parameter names are not published anywhere. The error keys come from the string literals in the handler (`"page"`, `"Page"`, …), which do not change | `[FromQuery(Name = "pageSize")]` on every member (noise); camelCase positional members (breaks C# naming) |
| Handler parameter: `[AsParameters] <Name> query`; services, `ClaimsPrincipal`, `CancellationToken` stay as separate parameters | D16 is about query values only; `[FromServices]` in the record would hide dependencies | Putting services/`ct` in the record |
| Orders record is `AdminOrderListRawQuery`, not `AdminOrderListQuery` | `AdminOrderListQuery` already exists (`Api/OrderRequests.cs:90`) as the typed, validated query the handler builds; renaming it is churn outside the row. "Raw" says "text before parsing" | Renaming the existing record; nesting the new record in the endpoints class |
| One file per record, beside its endpoints file | D16 says "next to the endpoints file"; matches `QueryValues.cs`/`OrderRequests.cs` style of small `Api/` files | Records at the bottom of the endpoints file |
| The `// Query values arrive as string[] …` comment moves from the handler to the record (one line, kept in substance); the handler's "bad numbers are reported alone…" comment stays in the handler | The record is now where the `string[]` choice is visible | Duplicating the comment in both places |
| No test edits, no new tests | Pure binding refactor; the existing pins in §5 already cover repeated keys, garbage numbers/dates, blanks and defaults on all six routes. Migration plan §3: existing tests pass unedited | Adding duplicate pins |

No packages, no migrations, no `*.Contracts` changes, no DI changes, no decision-log row (D16 already records the decision).

## 3. Contract per file

Example (row 1, the shape every row follows):

```csharp
namespace Ecommerce.Catalog.Api;

// Query values arrive as string[] so a repeated key reads its first value, and page/pageSize as text so a bad value gets the same errors body as the other 400s (D9).
internal sealed record ProductListQuery(
    [FromQuery] string[]? Search,
    [FromQuery] string[]? Category,
    [FromQuery] string[]? Sort,
    [FromQuery] string[]? Page,
    [FromQuery] string[]? PageSize);
```

Handler signature becomes `List([AsParameters] ProductListQuery query, [FromServices] CatalogBrowseService catalog, CancellationToken ct)`; the body reads `query.Page`, `query.Search?.FirstOrDefault()`, etc. Every other line of the body is unchanged (same error keys, same order of checks, same calls).

Per row, the body change is only the renames `page` → `query.Page` etc.:
- Row 2 (`AdminProductsEndpoints.List`): `Search, Category, Status, Page, PageSize`.
- Row 3 (`AdminCustomersEndpoints.List`): `Search, Page, PageSize`.
- Row 4 (`AdminDashboardEndpoints.RecentOrders`): `Page, PageSize`. Signature currently on one line; write it one parameter per line like the others.
- Row 5 (`AdminInventoryEndpoints.List`): `Page, PageSize`. The local `size` variable keeps its name.
- Row 6 (`AdminOrderReadsEndpoints.List`): `Page, PageSize, Status, From, To, Customer, OrderNumber`. Error keys stay PascalCase `"Page"`, `"PageSize"`, `"From"`, `"To"` exactly as today; `FirstText(query.Status)` etc. The local `AdminOrderListQuery query = new …` would clash with the parameter name: name the parameter `raw` (`[AsParameters] AdminOrderListRawQuery raw`) and leave the local `query` as it is, so the validator/service code below does not change.

Usings: each endpoints file already imports `Microsoft.AspNetCore.Http` (for `AsParametersAttribute`) and `Microsoft.AspNetCore.Mvc` (`FromQuery`). If `FromQuery` is no longer used in an endpoints file, keep `Microsoft.AspNetCore.Mvc` only if `[FromServices]` still needs it (it does in all six). Each new record file needs `using Microsoft.AspNetCore.Mvc;`. No `var` (IDE0008).

## 4. Build order

1. Baseline: `dotnet test backend/tests/Ecommerce.Api.IntegrationTests -c Release --filter "FullyQualifiedName~ProductQueryBindingTests|FullyQualifiedName~CatalogValidationTests|FullyQualifiedName~AdminCustomersApiTests|FullyQualifiedName~AdminProductContractTests|FullyQualifiedName~AdminInventoryContractTests|FullyQualifiedName~AdminOrderReadsContractTests|FullyQualifiedName~AdminDashboardApiTests|FullyQualifiedName~HttpContractTests|FullyQualifiedName~ContentTypeContractTests" | tail -n 20` — record the passed count (green before you start; this is a refactor, so there is no red step).
2. Row 1 (storefront products, the D16 motivating example) → re-run the filter.
3. Rows 2–6, one at a time; build after each.
4. Re-run the same filter: same passed count, zero failed, zero test files touched (`git diff --stat backend/tests` must be empty).
5. Tracker: set P2.83 to `Done`.
6. Full `bash scripts/verify.sh 2>&1 | tail -n 30`; quote the `VERIFY` line in `slice-P2.83-marco-report.md`.

## 5. Tests that prove binding did not change (all existing, all unedited)

All in `backend/tests/Ecommerce.Api.IntegrationTests/`.

| Row | Garbage number/date → same `errors` body | Repeated key → first value | Blank → default / other |
|---|---|---|---|
| 1 Products | `Catalog/CatalogValidationTests.Invalid_query_returns_400_problem_json_naming_the_field` (`page=abc`, `pageSize=abc`); `Catalog/ProductQueryBindingTests.B2`, `B3` (parse error hides range error); `HttpContractTests` (`page=abc`) | `ProductQueryBindingTests.B6_a_repeated_page_key_reads_the_first_value` | `B1`, `B1b`, `B4`; `CatalogValidationTests.Search_longer_than_100_characters_returns_400`, `Boundary_values_are_accepted` |
| 2 Admin products | `Catalog/Admin/AdminProductContractTests.IP5_list_query_parses_in_two_stages_and_reads_the_first_repeated_value` (`page=abc`, `page=abc&pageSize=500`) | same IP5 (`status=active&status=bogus`, `page=1&page=abc`) | — |
| 3 Admin customers | `Admin/AdminCustomersApiTests.I9_bad_paging_values_are_400_with_the_binding_message_only` | `I11_repeated_keys_use_the_first_value` (`search=ann&search=bob`, `page=2&page=abc`) | `I10`, `I12` |
| 4 Recent orders | `Projections/AdminDashboardApiTests.Recent_orders_bad_numbers_are_400_with_the_binding_message_only` | `Recent_orders_repeated_keys_use_the_first_value` | `Recent_orders_range_errors_are_collected_with_exact_texts_and_shape` |
| 5 Inventory | `Inventory/AdminInventoryContractTests.IV6_a_bad_page_is_a_400_with_the_binding_message` | `IV7_the_first_value_of_a_repeated_page_wins_and_blank_values_use_the_defaults` | IV7 |
| 6 Admin orders | `Orders/AdminOrderReadsContractTests.P1_a_bad_number_or_date_is_a_400_with_one_PascalCase_binding_error`, `P2_a_binding_error_is_returned_alone_without_the_validator_errors` | `P4_a_repeated_key_uses_its_first_value` (`status`, `customer`, `page`) | `P3_blank_query_values_are_ignored` |

Cross-cutting: `ContentTypeContractTests.CT4` (body-route set unchanged — a GET that gained `IAcceptsMetadata` would show up as a new body route; Marco reports the `withBody` count is unchanged if the test prints it, otherwise CT1–CT4 green is enough); the P2.59 policy inventory test and `Ecommerce.ArchitectureTests` (new records are `internal`, live under `Api/`, no new `Map*` calls). The 401/403 tests on these routes stay green unchanged.

## 6. Commit scope (for the step-5 Marco)

Slice files: the 6 endpoint files, the 6 new record files, `docs/admin-dashboard-feature-tracker.md` (already carries the uncommitted P2.83 row plus the new **H** section with P2.84/P2.85 Todo rows — Nathaniel's edit, commit as is), `slice-plans/minimal-api-migration-plan.md` (uncommitted D16 row), the P2.74 archive move (`slice-plans/slice-P2.74-*.md` deletions + `slice-plans/archive/slice-P2.74-*.md`), and this slice's plan/report/review. Body line: `P2.83 | Query values bound through one [AsParameters] record per endpoint on the six converted list endpoints; binding and 400 bodies unchanged | Catalog, Admin, Inventory, Orders`.

## 7. Risks

- **Silent binding change** (the real risk): a member that stops binding returns `null` → defaults → 200 instead of 400/first-value. Covered by the §5 pins on every row; any red there means the record is wrong, never the test.
- **Inference as body**: avoided by explicit `[FromQuery]` on every member (§2). If a GET list starts answering 415, this is the cause.
- **Name clash in Orders**: handled by `AdminOrderListRawQuery` + parameter `raw` (§3).
- **Scope creep**: no renames of locals, no reformatting of untouched code, no shared base record or generic paging record across modules (each module owns its own record; a cross-module type would break §6.2 boundaries anyway).
