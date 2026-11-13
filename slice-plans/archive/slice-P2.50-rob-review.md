# Slice P2.50 — Review thread

## Round 1 — Rob → Marco
Verdict: PASS · verify.sh: `VERIFY PASS (secrets hygiene format build test drift expo-check tsc jest export)` — build 0 warnings, jest 260 suites / 1983 tests, drift clean.

Low tier, no mutations. The diff matches the plan. One pure function reads the response, the mapping is the only new caller, and the client renders what the server sends. I found no blocking issues and no bugs.

### What I checked

- **`OrderStatusNotes` is pure** (`Application/OrderStatusNotes.cs`). It is a static switch over `status`, with no I/O, no clock and no DbContext, and its notes are immutable static records. For `Cancelled` you pick `timeline.LastOrDefault(t => t.ToStatus == OrderStatus.Cancelled)`, which is the latest entry because the timeline is ascending (09-api). The reason is compared ordinally against `Order.ReservationExpired`, so the code uses the domain's constant and not a copied literal. A `null` entry or reason falls through to the generic copy, so `For` covers every case.
- **Hard rules:** there is no `DateTime.UtcNow`, no `DbSet`, no cross-module reference and no new `using` outside Orders. The new types are `internal`. Drift is clean, so there is no migration.
- **Ownership:** the 403/404 paths return before `ToResponse`, so the note can only appear in a 200 body. I4 seeds an expired order for customer B, requests it as A, asserts 403, and asserts the pill text is not in the body. You report that I4 was green before the wiring, which is expected: the guard comes first and did not change. T7 is untouched.
- **UI shows only server text** (`OrderDetailView.tsx`). The pill note and `CancellationAlert` render `order.statusNote.*` and nothing else. S4 (`Cancelled` with `statusNote: null` → no title, no label) proves the client invents nothing. The styling uses tokens only, there is no `console.*`, and the alert is placed between the Header and the columns as planned.
- **`tests/order-fixtures.ts` default:** I reviewed this as a possibly weakened invariant and it is not one. The single added line `statusNote: null` comes before `...overrides`, so every caller can still override it. Before this change, existing tests got `undefined`, which renders the same as `null` (both falsy). The type now requires the field, so the default keeps `tsc` honest without hiding anything. No existing test changed.
- **U4 string-parse deviation:** I accept it. `OrderStatus` is `internal`, and a public xUnit theory method cannot take an internal type as a parameter (CS0051). `Enum.Parse<OrderStatus>` with the six names covers every status except `Cancelled` and `Refunded` (I checked against `Domain/OrderStatus.cs`, which has 8 members). A typo would throw, so the test cannot pass silently.
- **U1 uses the real domain** (`Order.Place` → `CancelForExpiry` → `History` mapped to `TimelineRow`), so renaming the reason turns it red. That is the guard the plan asked for.
- **Docs:** the `09-api.md` contract line and the decision-log row match the code. The tracker row is set to Done.

### 1. I4 has a tautological assertion — NOTE (`OrdersReadApiTests.cs`, last line of I4)
**What I saw:** `Assert.NotEqual(a.Id, b.Id)` can never fail, and it adds nothing to the 403 proof.
**What I want:** drop it next time the file is touched. → backlog.

### 2. Pill note is a separate screen-reader stop — NOTE (`OrderDetailView.tsx` Header pills row)
**What I saw:** the note is a plain `Text`, so a screen reader reads it with no link to the status pill. → backlog, for when a11y testing asks for it.

**What was good:** you used `Order.ReservationExpired` instead of a string literal in production code, while the integration seed pins the wire literal. Between them, a renamed reason fails a test whichever side it changes on. The S4 test is the right negative check for "the API returns the text".
