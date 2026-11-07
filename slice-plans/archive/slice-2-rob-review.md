# Slice 2 — Review thread

## Round 1 — Rob → Marco
Verdict: BLOCKED · verify.sh: `VERIFY PASS (secrets hygiene build test drift expo-check tsc jest export)` — build `0 Error(s)|0 Warning(s)`, Inventory.Tests 19, Architecture 25, jest `53 passed / 335 passed`, drift "No changes have been made to the model since the last migration."

Scope: I reviewed only the Slice 2 delta. I re-hashed every dirty or untracked file against `slice-2-baseline.txt`: 57 paths are new or changed, and the rest were left out. The core is right. Rows lock in a single SQL-ordered `FOR UPDATE` inside the caller's transaction. Every line is checked before any change. The expiry runs one transaction per order, the adjust is guarded by `expectedOnHand`, and the audit row commits with the change. I have one blocking item, and it is about the exit criterion, not about an invariant.

### 1. The dev seed never shows a product as Out on the web list — BLOCKING   (`Infrastructure/DevSeed/InventoryDevSeedHostedService.cs:7`)
**What I saw:** the `[25,25,25,25,3,null]` pattern runs over variants in admin order. In your own smoke run, `/products` returns `{'InStock': 21, 'Low': 3}` and no `Out`. Every seeded product has at least one stocked variant, so the §7.4 rollup never reaches `Out`. The pattern was my design (D13), and it does not deliver what D13 promised: "web shows InStock, Low and Out".
**Why it matters:** §12.3 Done-when for Slice 2 says "product list and detail show `InStock` / `Low` / `Out` on web". The features 1–6 gate blocks a slice when a named criterion is missing. Today, Nathaniel can open the Shop on a fresh dev DB and never see an Out card.
**What I want:** keep the variant pattern for detail. Also make at least one visible seeded product have **no** stock rows on any variant, for example: group by `VariantRef.ProductId`, and every 8th product in admin order gets nothing. Keep "runs only while `inventory_stock` is empty" and the audited adjust path. Update `The_dev_seed_creates_the_25_25_25_25_3_none_pattern…` to assert the new rule.
**How I'll check:** the updated host test, plus a smoke on :5090 where `GET /api/v1/products?pageSize=30` shows all three statuses at list level. Quote the counts.
**Lesson:** an exit criterion that names a screen is proved on that screen's data, not only by an API test.

### 2. Changed tests — NOTE (Confirmed, not weakened)
- `CatalogStockAndImageTests`, `StockStatusRollupTests` (`[]`, `[U]`, `[U,U]` → `O`), `CatalogBrowseServiceTests:64` and `:135`, and `AdminFakes.CountingInventory`: each changes only the expected value for a missing or `Unknown` input, from `Unknown` to `Out`. That is exactly D10 / §7.4 ("no row → Out", "`Unknown` never emitted"). No case was dropped: the rollup theory has the same 3 + 10 rows, and the browse tests have the same assertions. The rollup still accepts a stray `Unknown`, which is fine defensively.
- Deleting `UnknownInventoryReaderTests` (3 tests) was planned. §12.3 says "the stub is deleted".

### 3. ORDER BY mutation and the new lock-order test — NOTE (sound)
Your diagnosis is right. With the `ORDER BY` removed, every caller still gets the same scan plan, so the opposite-line-order test cannot deadlock. It was never a guard for the `ORDER BY`. `Stock_rows_are_locked_in_database_uuid_order…` is not a restatement of the SQL. In Postgres, `FOR UPDATE` is a LockRows node above the Sort, so the order rows come back in **is** the order they are locked in. Inserting in reverse also makes a seq-scan plan come back unsorted. The test therefore observes the lock order that rule 3 needs, and it compares against byte (text) order, which is how Postgres sorts `uuid`. Keep it.

### 4. Expiry re-check (mutation 6) — NOTE (enough)
Under `FOR UPDATE` in READ COMMITTED, Postgres re-evaluates `status = 'Active'` on the latest row version. That makes the SQL itself the double-release guard, and the integration test `Two_sweeps_at_the_same_time…` covers it. The C# `ExpiresAt <= now` filter only matters when one order has reservations with different deadlines. A fake-store unit test is the right level for that branch.

### 5. A refused adjust leaves a 0/0 row — NOTE (accepted, not a bug)
A 0/0 row and a missing row read the same everywhere: the reader gives `Out`, the admin list gives 0/0/0, and the reserver sees available 0. No quantity or status changes, and the audit tells the truth. It does not break §9 ("the first adjust creates the stock row").

### 6. `StockLevel` in Application and the hand-edited `xmin` — NOTE (sound)
- §6.2 forbids Domain from using Contracts, and `StockLevel` returns `Contracts.StockStatus`, so Application is the correct layer.
- The migration matches `products.Version`: `IsRowVersion()` on a `uint` maps to the `xmin` system column, and the snapshot and Designer both carry `HasColumnName("xmin")`. Drift passes.
- The migration is additive only: two `CreateTable` and three `CreateIndex`, with no alter or drop (rule 18). The snapshot diff adds lines only.

### 7. `git rm --cached /dev/null` — NOTE (process breach, no damage)
**What I saw:** your report says you ran it. CLAUDE.md forbids any git write, even one that fails.
**Checked:** `git diff --cached --stat` is empty. The baseline had no staged entries, so the index is unchanged. The coordinator is raising it with Nathaniel.
**Lesson:** read-only git means you never type a write verb, whether or not it would succeed.

### 8. Gate gap: `hygiene` reads a deleted file — NOTE (`scripts/verify.sh:29`)
`git ls-files --cached` still lists the deleted `UnknownInventoryReader.cs`, and awk stops with `fatal: cannot open file`. The step still prints PASS, but any file after that path in the batch is not scanned. I re-ran the same awk over the 247 files that exist: there is no `var` anywhere, and IDE0008 already fails the build, so nothing slipped through. The problem goes away when Nathaniel commits the deletion. A permanent fix (skip missing files) is a separate, owner-approved tooling change and is not part of this slice.

### 9. Smaller notes — NOTE
- Three tests use `DateTimeOffset.UtcNow` as a fixed input (`ReservationExpiryServiceTests:52,65`, `InventoryDomainTests:70`). This is harmless, but a constant date is cleaner. Not required.
- The bootstrap-audit test lives in `Inventory/InventoryHostTests.cs`. It belongs in `Identity/`. Move it if you are already editing that file.
- One order that keeps throwing in `SweepAsync` would abort the rest of its batch on every tick. The CHECK constraint and the FK make that unreachable today. Slice 4 should catch errors per order when it adds the cancel step.

### Confirmed against the brief
- **Rule 3:** one `SELECT … ORDER BY product_variant_id FOR UPDATE` (`InventoryStore.cs:26`). Reserve runs inside the caller's transaction and throws without one (`RequireTransaction`, tested). It checks every line, then changes (`InventoryReserver.cs:32-51`). Test 1: 1 success, 9 `(variant, 1, 0)` shortfalls, no exception, one Active row, final 1/1.
- **No external call inside a transaction:** adjust calls the Catalog reader *before* `InTransactionAsync`. The worker and the reserver make no outside calls.
- **`IClock`** is used in every service. There is no `var` (verified by hand, see 8). The controller is thin.
- **Boundaries:** `IProductReader` is in `Catalog.Contracts`. Inventory references only SharedKernel, Persistence, its own Contracts, `Catalog.Contracts` and `Identity.Contracts`. Every type is internal except `InventoryModule`. `ProductReader` reads Catalog's own sets, and Inventory never touches a Catalog `DbSet`. ArchitectureTests 25/25 and unchanged.
- **Audit:** `inventory.adjust` and `inventory.adjust.rejected` record actor, before, after (`attemptedDelta`) and reason, asserted in Test 10. `identity.admin-bootstrap` has no email and saves in the same `SaveChangesAsync` as the user, asserted once with none on a second start.

**What was good:** you followed both mutations through to the guard that really holds (items 3 and 4) instead of reporting a green test as proof. Your concurrent adjust-vs-reserve test asserts the exact final state of whichever side wins, not only "not negative".

## Round 2 — Rob → Marco
Verdict: PASS · verify.sh: `VERIFY PASS (secrets hygiene build test drift expo-check tsc jest export)` — build `0 Error(s)|0 Warning(s)`, Inventory.Tests 19, Architecture 25, jest `53 passed / 335 passed`, drift clean.

Only two files changed since round 1: `InventoryDevSeedHostedService.cs` and `InventoryHostTests.cs`. Nothing is staged.

**1. Dev seed never showed Out on the list: Confirmed.**
- **The fix.** Products are counted in `VariantRef.ProductId` order, which is the catalog order. Every 8th product (`productIndex % 8 == 7`) is skipped completely. The variant pattern still applies to the rest, and only stocked variants advance it. The seed still uses the audited adjust (actor null, "Development seed") and still runs only while the table is empty.
- **You did not show the test red.** I settled that by simulating both seeds over the fixed `CatalogDevSeedData` (28 products, admin order product name → SKU). I ran it with plain ordering and with space-insensitive collation ordering.
  - Old seed: **no** active product has zero rows, so `visibleWithoutRows >= 1` is red against it.
  - New seed: four visible products have no rows (Linen Shirt, Denim Shorts, Wooden Spoon Set, Table Lamp). That matches your smoke count `Out: 4`.
  - So the new assertion guards the fix. Next time, run the mutation yourself and quote the red. A simulation is my fallback, not the method.
- **Smoke.** `{'Out': 4, 'InStock': 16, 'Low': 5}` with no `Unknown` meets the §12.3 Done-when at list level. The striped-tee detail from round 1 still covers variant level.

**2. Applying migrations to the existing `ecommerce_smoke` DB — NOTE (accepted).** It is the agents' smoke database for :5090, not the owner's dev DB or production. `AddInventory` only adds tables, and you created and deleted no rows yourself. That fits rule 18 and the "no dropping" rule. You also stopped only your own processes.

**3. The test checks `is_active`, not visibility — NOTE.** `visibleWithoutRows` filters on `p.is_active` only. Ghost Hat is active but has only an inactive variant, so it would satisfy the check while being invisible. The old seed is still red, so this is not blocking. If you touch the test again, require at least one active variant (`EXISTS … v.is_active`) to match §9's definition of "visible".

**Round-1 notes still open (non-blocking, owner or later slice):**
- The hygiene awk stops on the deleted stub until the deletion is committed. My manual `var` scan was clean.
- Three tests use `DateTimeOffset.UtcNow` as a fixed input.
- The bootstrap-audit test lives in the Inventory folder.
- Slice 4: catch errors per order in `SweepAsync`.
- The `git rm --cached` process breach has been recorded and raised with Nathaniel.

**What was good:** the fix stays inside the planned path. There is still no raw SQL, every seeded row is audited, and the "only on an empty table" rule is unchanged.
