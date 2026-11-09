# Slice 4 — Checkout + Payments — Marco report 

Concerns:
1. Web Jest project runs only `*.test.web.*`. jest-expo/web (jsdom) has no `fetch`/`Response`, and expo-constants web crashes (`merchantIdentifier`) under expo-router, so the existing RNTL suite cannot run there. iOS + Android run everything (822 tests); web runs 3 files (10). Arch §11.3 needs Rob's call.
2. Backend integration ★ tests and F4/F5/F6 were written after the code, then run red against stubbed services. Domain ★ tests (totals, order, payment, stock commit) were truly test-first.
3. `PaymentIntentHandle` has a third field `Attempt` (retry replay needs the attempt number; plan had 2 fields).
4. `Idempotency-Key` added to the CORS allowed headers: without it browser preflight fails for merge, checkout, retry.
5. Stripe.net 53.1.0 names the card list `AllowedPaymentMethodTypes`; never called live. `app.json` plugin is `["@stripe/stripe-react-native", {}]` (bare string crashes `expo export`).
6. Webhook service is `PaymentWebhookService`: the layer test forbids the word "Stripe" in Application.
7. Migration `AddCheckoutAndPayments` adds `orders.idempotency_key NOT NULL` with default `''` + unique index: fails on a non-empty orders table (empty outside tests). Applied cleanly to `ecommerce_smoke`.

Git write (my error): in the frontend step I ran `git mv -f lib/platform.test.ts lib/platform.test.native.tsx` (before the first `jest --selectProjects web` run, after the jest-setup.js edit). That staged the rename. Left staged for Nathaniel; no other git write.

Mutation checks (red line quoted; each restored, grep for markers = 0):
1. dedupe: `Failed!  - Failed:     2, Passed:     0` (WebhookTests.T3 x2)
2. early flush: `Failed!  - Failed:     1, Passed:     0` (CheckoutTests.T2_..._sent_concurrently)
3. expires_at: `Failed!  - Failed:     1, Passed:     9` (T24_past_the_reservation_expiry)
4. owner: `Failed!  - Failed:     1, Passed:     9` (T7_part_another_customer_retrying)
5. auto:/status: status check removed `Failed: 2, Passed: 0` (T19, T25); random auto key `Failed: 2, Passed: 0` (T19, T25)
6. gateway in tx: `Failed!  - Failed:     1, Passed:     0` (Rule6_the_gateway_never_sees_an_open_transaction)
AuthorizationTests: duplicate webhook line removed.

Bugs found: None
Bugs fixed: None
Bugs open: None

Tests changed (all justified, none weakened):
- `ReservationExpiryServiceTests` — constructor takes `IOrderExpiry`; fake returns false (Slice 2 behaviour unchanged).
- `AuthorizationTests` anonymous list + webhook; `SolutionLayout` + `Payments`.
- `OrderSeed` SQL + `idempotency_key`. `CatalogSchemaTests` table list + `outbox_events`, `payments`, `refunds`.
- `InventoryEnv`/`CartEnv.StartAsync`: optional `configure` (additive). `ApiFactory`: random Stripe test secrets.
- Moved (Platform.OS fakes gone, assertions identical): `lib/platform.test.ts` web case -> `platform.test.web.tsx`, native case -> `platform.test.native.tsx`; `auth/api.test.ts` "web: asks for the cookie transport..." -> `api.test.web.ts`; `auth/session.test.ts` "on web sign-in never touches SecureStore" and "restore on web makes exactly one cookie refresh" -> `session.test.web.ts`. Remaining native cases stay.
- `routes.test.tsx` checkout row stub -> built. `order-fixtures.ts` + `paymentStatus`. `render-with-query.tsx` mutations `gcTime: Infinity` (leaked timer hung Jest). `jest-setup.js` mocks the native Stripe SDK.

Changed: Slice 4. New: Payments (3 projects), `IOutbox`/`outbox_events`, migration `AddCheckoutAndPayments`, Orders checkout/retry/handler/options, Inventory commit + expiry, Cart contracts, `features/checkout`, `lib/platform.{d.ts,native,web}`.

Dirty-baseline files, my edits (old -> new):
- Dirty files edited (old -> new): Dockerfile +2 COPY; slnx +Payments; Api.csproj +Payments ref; ModuleComposition +AddPaymentsModule; Program.cs headers +"Idempotency-Key"; appsettings(+Development) shipping/tax/checkout/Stripe placeholders; EcommerceDbContext +outbox config; Persistence ext +IOutbox; Inventory (csproj, reserver, expiry, stock/reservation Commit); CartModule +CartLines; Orders (csproj, module, Order, OrderItem, stores, config, DbModel, query/views/responses/controller; `OrderStatus from` -> `OrderStatus? from`).
- Frontend: `api-client.ts` ApiError +`problemType`, `lines`, `orderId`; `query-keys.ts` +`shippingMethods`; `package.json`: zustand `^5.1.0`->`5.1.0`, expo-crypto `~58.0.7`->`58.0.7`, +3 Stripe pins, jest projects; `useOrderQuery(id, poll=false)`; orders `types.ts` +`PaymentStatus`/`paymentStatus`; `OrderDetailView` +payment line; `CartView` +Checkout link.

verify.sh (full, Release):
```
VERIFY PASS (secrets hygiene build test drift expo-check tsc jest export)
```
test: Payments 23, Inventory 28, Cart 19, Orders 171, Catalog 166, Identity 73, Architecture 25, Integration 301 (Checkout 40). build 0 warnings. drift green. jest 133 suites / 832 tests. `verify.sh docker` PASS. Smoke :5090 (`ecommerce_smoke`): checkout 503 + orderId, same key same order, no key 400, anonymous 401, unsigned webhook 400. API ended by `timeout`; :5090 free.

Quality-bar §2:
- [x] Module boundaries (Payments refs SharedKernel, Persistence, Contracts only; ArchitectureTests 25 green).
- [x] Money/stock tests (T1-T8, T19, T23-T25, T29, webhook, rule 6; mutation checks run).
- [x] Frontend F4, F5, F6, F10, F11, F12 part; no `.only`/`.skip`/`app/` tests.
- [x] `IClock` everywhere.
- [x] Server-side totals only (T5/T23).
- [x] Idempotency + outbox (T2, T3, T8, T19, T25).
- [x] Commands run, output quoted.
- [x] Placeholders only (`Stripe:*` empty; `EXPO_PUBLIC_STRIPE_PUBLISHABLE_KEY` empty).
- [ ] Docs: not edited by me. Gaps for Rob: concern 1 (§11.3), 3 (contract), 4 (CORS).
- [x] Explained above.
