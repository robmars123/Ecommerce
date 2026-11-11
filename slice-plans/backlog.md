# Backlog — non-blocking review notes

| From | Note | Suggested action |
|---|---|---|
| 5.2 R1 note 3 | A superseded payment attempt whose intent isn't created yet (a slow checkout call racing a retry, or an expiry racing a just-committed retry) gets no cancel request. `PaymentAttempts.StartAttemptAsync` and `RequestCancelOpenIntentAsync` cancel only the latest attempt, and only if it has a provider id. No money is lost: T19/T25 auto-refund. | In `EnsureIntentAsync`, refuse to create an intent for an attempt that is no longer the latest, or for an order that is no longer `PendingPayment`. |
- 5.2 guard comment 'No order lock' inaccurate (GetForUpdateAsync).

- 6b (Rob, plan): split `docs/architecture/13.2-decision-log.md` (~39 KB) per the ~10 KB file rule; `09-api.md` and `07-data-model.md` are also just over 10 KB.
- 6b (Rob, plan): Slice 6 outbox consumers keep the anti-join scan without a checkpoint; measure in the Slice 7 slow-query pass.
- 6b R1 (Rob): `ProjectionWorker.TickAsync` wraps all projectors in one try; an infrastructure exception from one projector ends the tick before the next runs. Wrap each `RunOnceAsync` loop in its own try/log.
- 6b R1 (Rob): `tests/app/admin.test.tsx` F7b Support-redirect filter uses `includes('/api/v1/admin/dashboard')`; tighten to an exact allow-list of the two dashboard paths.
- 5 gap-fill (Rob, plan): admin ship form takes a typed carrier code; a carrier picker needs a read of `Shipping:Carriers` (no §9 row yet).
- 5 gap-fill (Rob, plan): no "Unfulfillable" (6b saga) button on the admin order detail; the endpoint exists, the button is unscheduled.
- 5 gap-fill R1 (Rob): every list endpoint computes `Skip((page - 1) * pageSize)` in `int` with no upper bound on `page` (admin orders, catalog, admin products, returns); `page=2147483647` overflows to a negative OFFSET and answers 500. Fix once for all lists (cap `page`, or compute the offset in `long` and return an empty page past the end).
- 5 gap-fill R1 (Rob): `AdminOrderListView` shows the previous page's rows when a later fetch fails (`keepPreviousData` hides `isError` while `data` exists), and a page past the end shows "No orders match." with no pager to go back. Show an inline error with retry, and keep the pager when `totalPages > 0`.
- 9.4 gap-fill R1 (Rob): no unit test drives `AdminCategoryService.DeleteAsync` to `Ok`; the fake store's `Removed` list is never asserted (IC7 covers it over HTTP). Add one happy-delete unit test asserting `Removed` and one save.
- 9.4 gap-fill R1 (Rob): IC5 asserts only the 404 status, not the ProblemDetails body the plan named; assert `type`/`status` in the body.
- 9.5 gap-fill R1 (Rob): plan D13's success log line (product id, key, length) was dropped because Catalog Application may not reference Microsoft.Extensions.Logging. An upload is only traceable through the request log; add one Information line in `AdminProductsController.Upload` (or an Infrastructure decorator) if audit of uploads is wanted. Never log config values.
- 9.5 gap-fill R1 (Rob): a non-multipart POST to an `[Consumes]` action answers 404 (the anonymous `MapFallback` outranks MVC's 415 endpoint), here and on any future `[Consumes]` action. Harmless (nothing reaches the action), but the status is misleading; revisit if a client needs a 415.
- 9.5 gap-fill R1 (Rob): orphaned objects (uploaded, never saved on a product) need a periodic sweep; Slice 7 deploy smoke must upload one image to real Spaces and fetch it through the CDN URL (D11 checksum/chunk settings are unverified in CI).
- 9.7 gap-fill R1 (Rob): D8 says the category update invalidates on settle, but only the success path is tested (V4); the error-path invalidation (409/404 → refetch) is unpinned. Add one assertion to the 409 test.
- 9.7 gap-fill R1 (Rob): `AdminProductListView.tsx` keeps an empty line where `<CategoryCreateForm />` was removed (just before `</ScrollView>`).
- 10.4 R1 (Rob): `AdminInventoryView.test.tsx` insufficient-409 case uses `available: -1`; the server sends current `OnHand − Reserved` (8 for that row). Use `available: 8` and assert `Only 8 available; reserved stock cannot be removed.`
- 10.4 R1 (Rob): test 5's exact GET count (owner rule: no reread after adjust) was never seen red against a refetch; when next touched, add a local `onSettled` invalidate, confirm red, restore.
- 10.4 R1 (Rob): `text-warning` (Low stock label) may be under 4.5:1 on the light surface; consider a darker warning-text token.
- 12.1 R1 (Rob): paged lists compute `(page - 1) * pageSize` in `int`; a huge `page` (e.g. 30000000 with pageSize 100) overflows to a negative OFFSET → 500. Applies to Identity `Repositories.cs` customer search, Orders `OrderStores.cs` (3 lists), Catalog `ProductReader.cs`. Fix once API-wide: offset in `long` or a `page` upper bound → 400, with one test per list.
- 12.2 (Rob, plan): no navigation entry to `/admin/customers`; add a "Customers" quick action to `DashboardView` (`admin-dashboard` feature, Admin and Support).
- 12.2 (Rob, plan): customer detail has no "all orders" link; let `AdminOrderListView` take `?customer=<email>` (the API filter exists) and link to it from the detail.
- 12.2 R1 (Rob): customer list reuses catalog `SearchBar`, whose placeholder and accessibilityLabel are hard-coded "Search products"; add an optional `label` prop (default unchanged) and pass "Search customers" from `AdminCustomerListView`, updating its tests.
- P2.1–P2.2 R1 (Rob): manual browser check still open (Nathaniel, after the flex fix): `/admin` + orders/products/categories/inventory/returns/customers at 1280px (fixed sidebar) and 375px (toggle, 16px gutter, 240px overlay panel, no horizontal scroll, Stack header/back readable).
- P2.1–P2.2 R1 (Rob): jest-expo's default window width was never measured; P2.3 adds nav text to every admin screen, so record it there and pin width in any test that depends on sidebar visibility.
- P2.3 R1 (Rob): `app.json` now registers the `expo-status-bar` config plugin (added by `expo install --fix`); on the next EAS/Expo Go native check, confirm status-bar appearance is unchanged on iOS and Android.
- P2.3 R1 (Rob): the 12.2 note "no navigation entry to `/admin/customers`" is covered by the P2.3 sidebar (Customers item, Admin + Support); drop the `DashboardView` quick action unless the dashboard row asks for it.
- P2.3 R1 (Rob): manual browser check still open (Nathaniel): web 1024 px as Admin and Support (groups/items per plan D3), 390 px overlay scrolls to Settings.
- P2.4 R1 (Rob): manual browser check still open (Nathaniel): footer pinned to the sidebar bottom at 1280 px (wide column) and 390 px (overlay), long nav still scrolls above it, 100-char name ellipsises. If `h-full` does not resolve (Yoga % height of a stretched child), drop `h-full`/`flex-1` per plan risk 1; never add `flex-1` to the Sidebar root (S7).
