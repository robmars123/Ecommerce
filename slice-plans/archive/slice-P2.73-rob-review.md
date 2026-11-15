# Slice P2.73 — Review thread

## Round 1 — Rob → Marco
Verdict: PASS · verify.sh: `VERIFY PASS (secrets hygiene format build test drift expo-check tsc jest export)` — build 0 warnings, integration 651/651, jest 2111/2111

A faithful, small conversion. The handler is the controller body with explicit parsing in front of it; no query code moved, and every pin was green on MVC before the switch.

### What I checked

- **Scope:** `git diff --stat` touches only `ReturnsController.cs` (the class removed, nothing else), `OrdersModule.cs`, `ModuleComposition.cs`, plus the two new `Api/` files and the pin file. Nothing under `Application/`, `Infrastructure/`, `Domain/`, `*.Contracts` or `OrderResponses.cs`.
- **Queries:** each route awaits exactly one service method (`ListForAdminAsync` / `GetForAdminAsync`), with no loop and no second service. The mapping is still `result.ToResponse()` / `view.ToResponse()`, so the round-trip counts are the plan §6 baseline. No new N+1 or over-fetch. The existing ones are backlog N1–N5.
- **Boundaries:** the new file uses only `Ecommerce.Orders.*` and `Ecommerce.Identity.Contracts`. The class is `internal`. Only `MapOrdersEndpoints` is public. ArchitectureTests are green.
- **Parsing parity:** `QueryValues.cs` matches the Admin copy line for line, plus `ReadDate`. Your capture showed the messages are the same, so no label parameter was needed. You were right not to add one. Overflow (`?page=99999999999`) gives the same "not valid" message on both stacks. The hex forms (`0x2`) are the divergence already accepted in backlog P2.64.
- **Edge cases:** a blank value becomes null in both `FirstText` and `ReadInt`/`ReadDate`. A malformed id (`/admin/orders/abc`) still fails the `{id:guid}` constraint and returns 404. The validator still sees the untrimmed `orderNumber`/`customer`, and the trim happens after validation, as on MVC.
- **Tests:** no existing test was edited or deleted. P1–P9 (16 cases) are new.

### Mutations (re-run by me; broken → red → restored from scratch copy → `cmp` clean)

| M | Break | Red |
|---|---|---|
| M1 | group policy `AdminOrSupport` → `Admin` | `Failed: 4, Passed: 2`: `EndpointInventoryTests.T1…`, `AdminOrderListApiTests.Anonymous_is_401…`, `AdminOrderDetailTests.The_admin_detail_is_open_to_Admin_and_Support_only_and_unknown_is_404`, `AdminOrderReadsContractTests.P8…` |
| M2 | `.RequireAuthorization(...)` removed | `Failed: 3, Passed: 1`: the inventory test, the list 401/403 test, the detail Admin/Support test |

While M1 was applied I also ran `RefundTests.P2_52_admin_order_detail_embeds_the_payment_card` (`RefundTests.cs:418`). It **passes** under M1. That is expected, because it reads with the Admin token, so it is not a guard for this mutation.

### Your concerns

**C1. Wrong file for the M1 test.** Neither the plan nor the hand-off was quite right. The test is `The_admin_detail_is_open_to_Admin_and_Support_only_and_unknown_is_404`, in **class `AdminOrderDetailTests`**, which lives in `Notifications/HubAndResetTests.cs:162`. `RefundTests.cs:418` is the P2.52 payment-card test. The detail test goes red under M1 and M2 (above), so the mutation is covered. The class/file mismatch is now a backlog NOTE.

**C2. `?page=abc&page=1` quotes `'abc'` where MVC quoted `'abc,1'`.** **Accepted.** It is the same 400 on the same `Page` key. Quoting the joined value would mean joining the values in the error path while the success path uses the first value, which only adds code. I recorded it in the 13.2 "Minimal API migration" row. No pin needed.

**C3. P9 coverage with null colour and image.** **Sufficient.** P9's job is parity: it checks that the admin `order` JSON equals the customer JSON byte for byte. The catalog colour and image on the **admin** route are already pinned by `OrdersReadApiTests.P2_51_admin_order_detail_embeds_the_item_colour_and_image` (`:456`), and on the customer route by the other P2.51 tests. P2.52 payment is covered by `RefundTests.cs:418` and by P9's `payment` assert.

### Readability

The handler is about 40 lines, top to bottom: parse, validate, filter, one call. It has no helper layers, and its two comments explain the only non-obvious choices (`string[]`, PascalCase binding keys). `ReadInt`/`ReadDate` repeat the same six lines. Leave that: one generic parser would be harder to read than two plain ones.

### NOTEs (to `backlog.md`)

1. Move `AdminOrderDetailTests` out of `HubAndResetTests.cs` into its own file (C1).
2. The repeated-key message difference is accepted and unpinned (C2).

**What was good:** You captured the MVC behaviour first, including the two accepted date forms (` 2026-06-10`, `6/10/2026`), and turned the capture into asserts instead of guessing. P8 compares the full Support body against the Admin body on both routes, so a future role branch in either direction fails.
