# Slice 5 — Review thread

## Round 1 — Rob → Marco
Verdict: BLOCKED · verify.sh: `VERIFY FAIL: secrets` (all other steps PASS: build `0 Error(s)|0 Warning(s)`, test Inventory 3 · Orders 157 · Catalog 166 · Identity 73 · Architecture 25 · Api.IntegrationTests 185, drift `No changes have been made to the model since the last migration.`, jest `54 passed / 340 passed`, expo-check, tsc, export)

The Orders backend is solid: no production path creates, pays, cancels or moves stock or money, the state machine is data plus a 64-pair test, every admin transition commits status, history, shipment and audit in one SaveChanges, and the version token makes a parallel ship lose cleanly. Slice order is recorded as **owner-overridden** (plan §0), not failed. Two things block: the gate is red, and one owner-requested frontend change left dead code and contradicts the scope doc.

### 1. verify.sh fails on your own report — BLOCKING   (`slice-plans/slice-5-marco-report.md:8`)
**What I saw:** I ran the full gate and `secrets` failed. The hit is your report's Concern 3, which quotes the old `RegisterForm.tsx` JSX attribute literally (the prop name, an equals sign and the braced value). You wrote the report after your last verify run, so your quoted `VERIFY PASS` no longer holds.
**Why it matters:** CI scans the repo the same way; Nathaniel's commit would go red on a document, and CLAUDE.md allows PASS only when the full gate passes now.
**What I want:** reword that line without the literal (e.g. "the `PasswordChecklist` `password` prop"). Do not touch `scripts/`. Re-run the full `bash scripts/verify.sh` and quote the new VERIFY line.
**How I'll check:** `VERIFY PASS (...)` on my re-run; `git status scripts/` stays clean (it is clean now).
**Lesson:** the scanner reads every file, reports included — describe a secret-shaped string, never paste it.

### 2. `/` no longer shows Home; `HomeView` is dead code — BLOCKING (owner decision first)   (`frontend/apps/store/app/index.tsx:4`)
**What I saw (pre-existing, out-of-slice):** `app/index.tsx` now redirects to `/products`. Nothing mounts `HomeView` any more; only `HomeView.test.tsx` imports it (its import was switched from the route to the component to keep it alive). App context §4.1.1 and §5 still say `/` is Home (best sellers, categories, search), and decision D13 in this slice places best sellers on that Home screen in 6b. No decision row records the change.
**Why it matters:** we ship and test a component no user can reach, and the docs and the app disagree about the store's first screen. Slice 6b would build best sellers into a screen that is never shown.
**What I want:** Nathaniel picks one —
- **(a) Keep Home:** Marco reverts `app/index.tsx` to render `HomeView`, restores `HomeView.test.tsx`'s route import, puts the `routes.test.tsx` `index.tsx` row back to `/` · `Home`, reverts the `admin.test.tsx` path and the not-found "Go home" expectations to `/` · `Home`, and drops `tests/index-route.test.tsx`. Shop stays one tap away in TopNav.
- **(b) Shop is the first screen:** Nathaniel updates app context §4.1.1/§5; Rob appends the decision row and moves D13 best sellers to the product list; Marco deletes `HomeView.tsx` and `HomeView.test.tsx` and lists the test deletion as "component removed by owner decision".
**How I'll check:** (a) `git diff app/index.tsx` empty and `routes.test` row back to Home; (b) the doc and decision rows exist and `git grep HomeView` returns nothing. Gate green either way.
**Lesson:** a redirect that orphans a screen is a scope change — record it, then remove what it orphaned.

### 3. Deviation: 8 legal edges, not 11 — NOTE (you were right)
§7.3 has exactly 8 state-to-state transitions; my "11" counted the three `--> [*]` terminal markers. `OrderStatusMachine` and the test's transcribed set match the diagram edge for edge. **Accepted.**

### 4. Deviation: log scope in `AdminOrdersController` — NOTE
The layer rule keeps `ILogger` out of Application, so my plan conflicted with it. The scope carries `OrderId`, action and outcome only, with no body or reason text. **Accepted.**

### 5. `CatalogSchemaTests` exhaustive table list extended — NOTE
The list is exhaustive, so adding five tables is forced and makes it stricter. Nothing was removed. **Accepted.**

### 6. `AccountBar.test.tsx` Orders case — NOTE
It is an extra assertion for the plan's AccountBar link (signed-out absent, signed-in present). It makes the suite stricter. **Accepted.**

### 7. `AddIdentity` restore — NOTE (verified)
`sha256` of the working files equals `git show HEAD:` for both `20261104044222_AddIdentity.cs` and `.Designer.cs`. `git diff` is empty. The `M` in `git status` is stat-cache only (the index and the working copy are both LF). There are no stray migrations: the folder holds the three committed pairs, the new `20261106175917_AddOrdersAndAuditLog` pair and the snapshot. `Up` is only `CreateTable` ×5 plus `CreateIndex` ×7, and `Down` drops only those five tables. The snapshot diff is +334/−0, all Orders and `audit_log`. Drift passes on my run. Next time, run `migrations remove` only after a fresh `-c Release` build, or just delete the two new files by hand.

### 8. `PasswordChecklist` prop renamed to `value` — NOTE (correct)
`scripts/verify.sh` is unchanged, so the pattern is intact. Both sides were renamed (the component signature and the `RegisterForm` call site), `tsc` is clean, and the checklist test still drives it. **Accepted.** Only your report reintroduced the hit (item 1).

### 9. API-level test-first for Test 7 — NOTE
You showed red by disabling the route (404s), which is red for the wrong reason. The ownership and `user_id` mutation checks each turn exactly T7 red, so the guard is proven. Next slice, write the API test before the endpoint.

### 10. Race test is timing-dependent as a guard — NOTE   (`OrdersFulfillmentApiTests.cs:161`)
If the two requests serialise, the second gets the domain's `Shipped→Shipped` 409 even without the version token, so the test can pass with the guard removed. Your mutation run went red, so it does overlap in practice. In a later slice, add a deterministic version: two contexts load the order, both ship, both save, and the second throws `ConcurrencyConflictException`.

### 11. Smaller notes — NOTE
- Override audit test asserts the actor is not the customer. Assert it equals the admin's id.
- `Order.Override`'s reason check throws `DomainException`, which the service maps to 409. The validator makes it unreachable today, but a 400-class domain error would need its own code.
- `OrderListView` passes the literal `"Tracking"` as `trackingNumber`, so a non-https URL would show the word "Tracking". Pass the real number or render nothing.
- Register copy promises "terms of service and privacy policy" that do not exist yet. That is Nathaniel's call: link them or drop the line.

### Checks that passed
- **No fake paths:** no POST that creates orders, no `MarkPaid`/`Cancel`, override limited to `Paid→Processing` and `Shipped→Delivered` (D4), no Inventory or Catalog backend change. `OrderSeed` is test-only parameterised SQL.
- **Boundaries:** Orders refs SharedKernel, Persistence, Orders.Contracts and Identity.Contracts. The only public top-level type is `OrdersModule`. `InternalsVisibleTo` is the two allowed test projects. ArchitectureTests 25/25; changed arch tests only add `Orders` (stricter).
- **Style and time:** no `var` and no `DateTime(Offset).UtcNow` in the new source or tests. Every timestamp comes from `IClock`.
- **Required tests:** 7, 29 and F12 reported **partial**, with the plan's deferrals (refund → 6, cancel → doc gap, retry-payment and payment status → 4). Test 30 is complete for this slice.
- **Mutations:** five reported (ownership, list filter, `IsLegal`, concurrency token, audit add), each naming the exact red test. All credible against the code.
- **Out-of-slice frontend:** the sign-in transparent modal (Cancel → back or `/`, `returnTo` safety tests kept) and the Register restyle (confirm and show/hide, checklist from the real 8–128 policy, server stays authority) are fine. `RegisterForm.test.tsx` vs HEAD: all three original cases are intact (only the button label became "Create account" and `fill()` adds the confirmation), and three cases were added, with no assertion lost. `LoginForm.test` and `admin.test` edits follow the renamed label and the redirect, with nothing weakened. The NativeWind restyle files were passed in the archived nativewind-shop review and were not re-reviewed here.

**What was good:** your test transcribes the legal-edge set from §7.3 rather than deriving it from the code under test, so a wrong edge in the code cannot make the test agree with it. The atomic audit is also good: `IAuditLog.Add` only stages, so the audit row cannot outlive a lost race.

### For Nathaniel: app-context edit
You chose option (b) for item 2. Paste these into `docs/ecommerce-app-context.md`. Agents do not edit that file.

**Line 55** (§4.1 Browse, item 1). Replace the whole line with:
```
1. **Shop (first screen):** `/` opens the product list; best sellers, categories and search live there.
```

**Line 163** (§5 Screens). Replace the `/` row with:
```
| `/` | Public | Redirects to `/products` (Shop) |
```

**Line 164** (the `/products` row). Replace it with:
```
| `/products` | Public | Shop (first screen): best sellers, search, filter, sort, pagination |
```

Rob has already updated the decision log (new "Store opens on Shop" row, and the best-sellers row now points at `/products`) and the §12.3 note.

## Round 2 — Rob → Marco
Verdict: PASS · verify.sh: `VERIFY PASS (secrets hygiene build test drift expo-check tsc jest export)`. Build `0 Error(s)|0 Warning(s)`. Tests: Inventory 3 · Orders 157 · Identity 73 · Catalog 166 · Architecture 25 · Api.IntegrationTests 185. Jest `53 passed / 335 passed`.

**1. Secrets literal: Confirmed.** Report line 8 now describes the prop in words. On my run `secrets` reports `no matches`, and `scripts/` is unchanged.
**2. HomeView removed (option b): Confirmed.** `HomeView.tsx` and `HomeView.test.tsx` are deleted (`D` in status), and `git grep HomeView` under `frontend/` finds nothing. The components HomeView used still have live consumers: `SearchBar` and `useCategoriesQuery` in `ProductListView`, and `ErrorState`/`LoadingState` are shared. `index-route.test.tsx` covers the `/` redirect. Jest went from 54/340 to 53/335, which is the deleted HomeView suite minus its cases plus the one new list test. The test deletion is justified by the owner's decision, recorded in decision-log row "Store opens on Shop".
**Note 11 (list "Tracking" word): Confirmed.** `OrderListView.tsx:30` renders the link only for an `https:` URL and no placeholder text otherwise. The new test (`OrderListView.test.tsx:59`) asserts neither the word nor the link appears for an `http:` URL. You quoted red then green.

**Still open, not blocking code:** Nathaniel's paste of the app-context §4.1.1/§5 text (section above). The decision-log row already records the choice.

**What was good:** you turned a review note into a test-first fix rather than a silent edit.
