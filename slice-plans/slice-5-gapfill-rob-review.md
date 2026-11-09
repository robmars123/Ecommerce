# Slice 5 gap-fill — Review thread

## Round 1 — Rob → Marco
Verdict: BLOCKED · verify.sh: not run (BLOCKED round; it runs in the PASS round, CLAUDE.md §5)

The backend half is clean and matches the plan. The two-read customer lookup keeps rule 8, the store query is shared with the dashboard, and the auth guard holds under M1. One Low-tier frontend bug blocks: `OrderActions` keeps showing a confirm form for an action the new status no longer allows.

### 1. Ship/override form stays open after success — BLOCKING   (`frontend/apps/store/features/admin-orders/components/OrderActions.tsx:41,56-79,91-103`)
**What I saw:** You open the ship or override form with `setOpen(kind)`, and nothing closes it after the mutation succeeds. `useOrderActionMutation` only invalidates on settle, and the component is not remounted when the refetched `detail` arrives.
**Why it matters:** An admin ships a Processing order. The refetch brings back `Shipped`, so `actionsFor` offers Deliver / Override to Delivered, but `open === 'ship'` still renders the carrier and tracking fields and **Confirm ship** with the old values. That is a Ship action on a Shipped order, which D9 rules out. A second press gets a 409 and shows an error after a successful ship. The same happens after "Override to Processing": the override form stays open on a Processing order. The server still guards the transition, so no data goes wrong, but the screen offers an action the plan says it must not.
**What I want:** When an action succeeds, close the form and clear `carrier`, `tracking`, `reason` and `problems` (for example, a mutate-level `onSuccess`). Keep D10: no local status change. A failed call keeps the form open with its values so the admin can correct them.
**How I'll check:** Add a Jest test in `OrderActions.test.tsx` modelled on the "refetched server status" test: a Processing order, ship with valid fields, then the server reports `Shipped`. Assert `Confirm ship` and the `Carrier code` field are gone, and `Deliver` is shown. Add a second test: a 409 on ship keeps the form and its values. Quote the first test red against the current code, then green.
**Lesson:** Inline forms belong to the state that opened them. When the server moves the state, close the form.

### Points you raised
1. **`ModelState.Clear()` before `InvalidProblem`. Accepted.** `[ApiController]` is on the class and nothing in `src` sets `SuppressModelStateInvalidFilter` or a custom `InvalidModelStateResponseFactory`. A binding failure such as `from=2026-13-01` is answered 400 by the filter before `List` runs, and your Date test pins that. When `List` runs, `ModelState` holds only valid bound values, so `Clear()` drops nothing but the PascalCase keys. The `status` test (`errors.status` present) proves the camelCase key.
2. **Changed tests. Confirmed, none weakened.** `routes.test.tsx` moves `/admin/orders` from stub to built (`Orders`, `true`), the same treatment as `Returns` and `Order`, and its mock now answers the list GET. `admin.test.tsx` changes only the header text; the Support-reaches-orders assertion is unchanged. The three fakes only gain `NotSupportedException` members for the new interface members, and no unit test path calls them. `ArchitectureTests` are not in the diff.
3. **`GET /admin/orders/{id}` after the rename. Confirmed.** The class route `api/v1/admin/orders` plus `[HttpGet("{id:guid}")]` gives the same path. The policy is still the class-level `AdminOrSupport` and covers both actions. Your report lists the detail and refund integration suites as green. I'll see them again in the full gate.

### Mutation M1 (re-run by me)
I changed `ReturnsController.cs:138` from `[Authorize(Policy = AuthorizationPolicies.AdminOrSupport)]` to `[Authorize]`:
```
Anonymous_is_401_customer_is_403_without_data_and_support_and_admin_are_200 [FAIL]
Expected: Forbidden
Actual:   OK
Failed!  - Failed:     1, Passed:     0, Skipped:     0, Total:     1
```
I restored the file from a scratch copy, and `cmp` printed `RESTORED`.

### 2. Paging offset overflow — NOTE (backlog)
`Skip((page - 1) * pageSize)` overflows `int` for a huge `page` and returns 500. Every list endpoint has the same pattern, so this needs one cross-cutting fix, not a fix only here.

### 3. List error and past-end states — NOTE (backlog)
With `keepPreviousData`, a failed refetch still shows the old rows. A page past the end has no pager to go back.

**What was good:** `TryParseStatus` reuse, which pins case and rejects numeric strings, with `"3"` covered in both test layers. The date-bound test pins 23:59:59Z in and the next 00:00:00Z out, which is exactly the boundary that goes wrong in UTC-day filters.

## Round 1 — Marco → Rob

1. **Fixed.** `OrderActions.tsx` now has `close()` (open=null, carrier/tracking/reason/problems cleared), passed as `{ onSuccess: close }` to the ship and override `mutate` calls (lines ~80, ~86). Failure keeps the form and values; no optimistic status (D10). Test `closes the ship form once the server reports Shipped, offering Deliver` failed before (`expect(queryByRole 'Confirm ship').toBeNull()` received the button) and passes now. `keeps the ship form and its values after a 409` also added (passes, guards the failure path). OrderActions.test.tsx 24/24; `verify.sh changed` PASS.

## Round 2 — Rob → Marco
Verdict: PASS · verify.sh: VERIFY PASS (secrets hygiene build test drift expo-check tsc jest export); jest 1103/1103.

### 1. Ship/override form stays open — Confirmed fixed
`OrderActions.tsx`: `close()` resets open, carrier, tracking, reason and problems; it is passed as `{ onSuccess: close }` to both the ship and override `mutate` calls, so failure (409/400) leaves the form and values intact and no optimistic status is set. Tests present at `OrderActions.test.tsx:151` (closes after Shipped, Deliver offered) and `:176` (keeps form and values after 409). Rows 11.2/11.4/11.5 set to Done.
