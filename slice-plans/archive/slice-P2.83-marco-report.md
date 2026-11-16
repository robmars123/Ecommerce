Concerns: None
Bugs found: None
Bugs fixed: None
Bugs open: None
Tests changed: None (no test edited or added; `git diff --stat backend/tests` empty)

Changed (slice P2.83, Low tier):
- New records (internal sealed, positional, `[FromQuery] string[]?` per member, comment moved here):
  - backend/src/Modules/Catalog/Ecommerce.Catalog/Api/ProductListQuery.cs
  - backend/src/Modules/Catalog/Ecommerce.Catalog/Api/Admin/AdminProductListQuery.cs
  - backend/src/Modules/Admin/Ecommerce.Admin/Api/AdminCustomerListQuery.cs
  - backend/src/Modules/Admin/Ecommerce.Admin/Api/RecentOrdersQuery.cs
  - backend/src/Modules/Inventory/Ecommerce.Inventory/Api/InventoryListQuery.cs
  - backend/src/Modules/Orders/Ecommerce.Orders/Api/AdminOrderListRawQuery.cs
- Handlers now take `[AsParameters] <Record> query` (Orders: `raw`), body only renamed to `query.X`:
  ProductEndpoints.List, AdminProductsEndpoints.List, AdminCustomersEndpoints.List, AdminDashboardEndpoints.RecentOrders, AdminInventoryEndpoints.List, AdminOrderReadsEndpoints.List.
- Old "string[]" comments removed from the five handlers that had them (they now live on the record). Error keys, check order and calls unchanged.
- docs/admin-dashboard-feature-tracker.md: P2.83 set to Done.

Filtered binding pins (9 classes in plan §4): 118 passed, 0 failed, before and after (baseline run was the same filter; build-only change in between). CT1-CT4 green, so no GET became a body route.

verify.sh: VERIFY PASS (secrets hygiene format build test drift expo-check tsc jest export); build 0 warnings 0 errors; jest 2111 passed.

Quality-bar §2:
- [x] Builds, 0 warnings; no `var`; code plain and minimal.
- [x] Existing tests pass unedited; no new tests (plan: pure binding refactor, pins cover all six routes).
- [x] No money/stock/auth change; module boundaries kept (records internal, per module).
- N/A: migrations, packages, docs beyond tracker row, mutations (Low tier).
