# Slice 6b — Eventual consistency + admin dashboard (plan)

Mode: Autonomous · Rob · 2026-11-08 · Verdict: READY

## 1. Read (Marco's whole list, once each)

- Arch §2, §3.4, §6.1–6.2, §7.1 (`outbox_events`, `consumer_checkpoints`, `processed_events` rows only), §8.4, §8.5, §8.6, **§8.8 (all, incl. new §8.8.6)**, §9 (endpoint table + money/ProblemDetails conventions), §10.1, §11.2 rows 11–18 and the test-first line, §11.3 intro, §5 (frontend part only).
- App context §4.8 (dashboard), §4.1 item 1 and §4.2 item 4 (best sellers, related).
- Decision log by grep only: `grep -n -i "outbox runtime\|Search source\|Best sellers\|Slice 6b" docs/architecture/13.2-decision-log.md`.
- Code: only the files §3 names, plus `Persistence/Outbox/*` and `tests/.../Outbox/OutboxProcessorTests.cs` as the pattern.

Scope = the §12.3 6b row (tests 11–18): app-context 8.1–8.5 **plus** the search projector, best sellers/related (decision-log carry-in from 1a), the fulfillment saga and Test 15. App context §3.1 has no rows for the last three; Nathaniel adds them (risk R1). Nothing else.

## 2. Decisions

| # | Decision | Reason | Rejected |
|---|---|---|---|
| D1 | New **projector runtime** `ProjectionProcessor` in `Persistence/Projections`, beside (not replacing) `OutboxProcessor`. Per event, **one transaction**: mark `processed_events (projector, event_id, 'Done')` (insert-or-promote a non-Done row; 0 rows → already done → no-op), version gate, `IProjector.ApplyAsync`, version upsert, commit | §8.8.2: read model + dedupe in one tx is what makes a duplicate a no-op (Test 11/12) | Reusing `OutboxProcessor` (marks Done *after* the handler, in its own statement — a crash between double-counts) |
| D2 | **Checkpoints only for projectors**, in `consumer_checkpoints` (§7.1 name). Selection = `sequence > checkpoint` + the existing anti-join (no Done/DeadLetter row, Retrying only when due), sequence order, batch 50. Side-effect consumers keep the Slice 6 anti-join unchanged | Replay needs a checkpoint; email/SignalR never replay, and a checkpoint is exactly what can skip a late commit | Checkpointing the Slice 6 consumers too (risk with no consumer of the benefit; scan cost → backlog, Slice 7) |
| D3 | **Sequence gaps:** apply immediately, but the checkpoint advances only to the highest S where every row in (checkpoint, S] is terminal for this projector (or not its type) **and** `occurred_at ≤ now − Projections:SafetyWindowSeconds` (default **30**, `IClock`). Monotonic upsert (`GREATEST`) | Lag is unaffected (apply isn't delayed); a lower sequence committing late is still above the checkpoint and found next pass; dedupe makes the rescan safe (§8.8.2 "rescan a trailing window") | A read-delay window (adds lag); `xmin`/snapshot high-water (xid wraparound casts, harder to test) |
| D4 | **Version gate in the runtime**, opt-in per projector: `IProjector.VersionedAggregateTypes`. Table `projection_versions (projector, aggregate_type, aggregate_id, version)`, absent = 0. version ≤ stored → skip (mark Done, no apply); version > stored + 1 → **Gap**: rollback, `Retrying` with `OutboxBackoff`, after `MaxAttempts` → `DeadLetter` + `LogCritical(DeadLetterEvent)`; never apply past it | §8.8.2; one tested gate instead of one per projector. Order versions are contiguous (each transition `Version++` + exactly one event; place = 0, no event) | Gate inside each projector; gating `Refund`/catalog streams that are consumed partially (would gap forever) |
| D5 | **Replay** = `IProjectionReplayer.ReplayAsync(name)` (SharedKernel interface, Persistence impl): under the projector's advisory lock (try-lock; held → 409 `projection-busy`), one tx: `IProjector.ResetAsync` (DELETE its rows), delete its `processed_events` and `projection_versions` rows, checkpoint → 0. Exposed as `POST /api/v1/admin/projections/{name}/replay`, **Admin**; unknown name → 404 | §8.8.2 replay must be operable, not test-only; outbox rows are retained | Startup flag / console command (no auth story, can't be tested over HTTP) |
| D6 | **Lag targets confirmed:** dashboard and search ≤ **60 s** (`Projections:LagTargetSeconds`), email ≤ 2 min p95 (not measured in v1). Lag = now − `occurred_at` of the oldest eligible non-terminal event of the projector, 0 if none. `asOf` = that `occurred_at`, else now. Worker logs `ProjectionLagAlert` (EventId 4102, Warning) when lag > target | §8.8.3 asks the plan to confirm | — |
| D7 | `IConsumerHealthReader`: per projector `{name, lastSequence, lag, asOf, deadLetters}` + dead letters per side-effect consumer | §8.8.3; consumer: needs-attention | A health endpoint (no consumer) |
| D8 | **New Admin module** (+ empty `Admin.Contracts`) owns `admin_dashboard_*` and projector `admin.dashboard` (versioned: `Order`) | §6.1 Admin row | Projection inside Orders |
| D9 | Dashboard read model = **one row per order** `(order_id PK, user_id, status, grand_total null, currency, paid_at null)` + **one row per admin refund** `(refund_id PK, order_id, amount, succeeded_at)`. KPIs aggregate at read time. Revenue = Σ `grand_total` paid in period − Σ refunds succeeded in period (cash basis). Order count and AOV (gross Σ/count) from paid in period. Customers: distinct paying users in period, **new** = first-ever `paid_at` in period, returning = rest | Per-order rows make dedupe/version natural and replay exact; no counters to drift | Pre-aggregated daily counters (a duplicate or replay bug double-counts silently) |
| D10 | Periods `today`/`7d`/`30d` (default `today`, else 400) are **UTC dates**: from 00:00 of today, today−6, today−29, to now (`IClock`) | No store timezone configured; matches daily sales buckets | Rolling hours |
| D11 | `OrderPaidPayload` + `OrderPaidLine` move **unchanged** (same JSON) to `Orders.Contracts` | §6.1 public `OrderPaid`; replay must use the event, not today's state | Projector calling `IOrderReader` |
| D12 | Payments emits **`RefundSucceeded`** (`Payments.Contracts`: type constant + `RefundSucceededPayload(RefundId, OrderId, Amount, Currency, SucceededAt)`; aggregate `Refund`, version 2) in the refund-webhook transaction, **only for refunds with an actor** (admin and saga). Automatic refunds (Tests 19/25) emit none | Revenue must drop on partial refunds, which change no order status; auto refunds return an *extra* payment that was never revenue | Deriving refunds from `OrderStatusChanged → Refunded` (misses partials, double-subtracts) |
| D13 | Orders-by-status: projected orders only; **`PendingPayment` not shown** (it has no event) | An `OrderPlaced` event would change the checkout tx (Tests 2/4) | `OrderPlaced` at version 0 |
| D14 | **Recent orders, low stock, pending returns = direct contract reads** (§3): stock rows with available ≤ `Inventory:LowStockThreshold`, ascending | 8.3 says "reads"; strongly consistent; checkout never reads them (rule 15) | Projecting them |
| D15 | **Search projector = sales rank only.** Catalog projector `catalog.search` (not versioned) consumes `OrderPaid`, maps variant → product in its own tables, upserts `catalog_product_sales_daily (product_id, sales_date, units)`. Full-text search stays on `products` (updates decision-log row "Search source in 1a") | The only search data that needs Paid orders; moving FTS to a read model would make publish eventually visible and need a Catalog event stream for nothing | Full search-document read model (decision-log row's original plan) |
| D16 | Best sellers: `sort=bestselling` on `GET /products` (all-time units desc, then `id`; unsold last); `GET /products/{slug}` gains `related` (≤ 4 visible products of the same category, not itself, best sellers first, then newest) — list-item shape. `IProductSalesReader.GetTopAsync(fromDate, toDate, count)` in `Catalog.Contracts` for the dashboard's top 5 | Decision-log carry-in (1a); one read model feeds both | Separate best-sellers endpoint |
| D17 | **Fulfillment saga.** The v1 "fails finally" trigger is `POST /api/v1/admin/orders/{id}/unfulfillable` `{reason}` (Admin). One tx in Orders: lock order; Paid/Processing only (else 409); `fulfillment_sagas (id, order_id **unique**, step 'Fulfill', status 'Compensating', reason, actor_id, refund_amount, created_at)`; `refund_amount` = `IRefundState` `Remaining` (read after the order lock, Slice 6 D8); audit (`fulfillment.unfulfillable`, before/after/reason); outbox `FulfillmentFailed` (aggregate `FulfillmentSaga`, saga id, v1; public payload in `Orders.Contracts`). Repeat → 200 with the same saga, no new event | No automated fulfillment step exists in v1 (manual §8.6); the HTTP call can't lock payments-then-orders, so compensation is async | Calling `RefundService` in the request (wrong lock order → deadlock with the refund path) |
| D18 | Payments consumer `payments.fulfillment-compensation` → `RefundService.RequestAsync` with actor = saga actor, amount = **event's** `refund_amount`, key **`saga:{sagaId}:Fulfill`**. Same amount + key ⇒ same request hash ⇒ replayed result on retry. `refund_amount` 0 → no refund. The existing webhook path moves the order to `Refunded` and audits; `ApplyFullRefundAsync` sets the saga `Compensated` in that tx. **`ship` rejects an order with a saga** (409 `order-unfulfillable`) | Exactly one refund (§8.8.4): consumer dedupe + idempotency key + ceiling as backstop; no shipment between mark and refund | Recomputing the amount at retry time (hash mismatch → 409 → dead letter) |
| D19 | Dashboard + recent orders are **Admin, Support**; Support gets `kpis: null` | App context §4.8, §5 route table | Support 403 |
| D20 | **Test 15: "the duplicate is proven harmless"** — re-send carries the same key `{consumer}:{eventId}`; dev pickup keeps one file; provider 13.7 must honour keys | At-least-once by design (Slice 6) | At-most-once (needs 2PC) |
| D21 | One additive migration `AddProjectionsAndSaga` (rule 18); no new package | — | — |

## 3. Files, projects, contracts

**SharedKernel** `Projections/`: `IProjector { string Name; IReadOnlySet<string> EventTypes; IReadOnlySet<string> VersionedAggregateTypes; Task ApplyAsync(OutboxMessage, CancellationToken); Task ResetAsync(CancellationToken); }` (runs inside the runtime's tx on the scoped context; no external call), `IProjectionReplayer`, `IConsumerHealthReader` + records `ProjectorHealth`, `ConsumerDeadLetters`. Named records, no `var`.

**Persistence** `Projections/`: `ProjectionProcessor` (D1–D4), `ProjectionWorker` (hosted, one per registered projector, `Projections:PollIntervalMs` default 1000, lag alert D6), `ConsumerCheckpointEntry`, `ProjectionVersionEntry` (+ configs), `ProjectionReplayer`, `ConsumerHealthReader`, `ProjectionsOptions` (validated: window ≥ 1, target ≥ 1). Extract the two advisory-lock helpers from `OutboxProcessor` into an internal `AdvisoryLock` used by both — **no behaviour change, no test change** there. Lock key `projection:{name}`.

**Orders**: `Contracts/OrderEvents.cs` += `OrderPaidPayload`, `OrderPaidLine` (D11), `FulfillmentEventTypes.FulfillmentFailed`, `FulfillmentFailedPayload(SagaId, OrderId, ActorId, RefundAmount, Currency, Reason, OccurredAt)`; `IOrderReader` += `ListRecentAsync`, `CountPendingReturnsAsync` (+ records `RecentOrder`, `RecentOrderPage`). `Domain/FulfillmentSaga.cs`; `Application/FulfillmentSagaService.cs`; ship guard in `OrderFulfillmentService`; `ApplyFullRefundAsync` marks saga `Compensated`; endpoint in `AdminOrdersController` (thin).

**Payments**: `Contracts` += `PaymentEventTypes.RefundSucceeded`, `RefundSucceededPayload`; webhook emits it (D12); `FulfillmentCompensationConsumer` (D18) registered like the other consumers.

**Catalog**: `CatalogSalesProjector` (D15), `ProductSalesDaily` entity in `Domain`, sort `bestselling`, detail `related`, `IProductSalesReader` impl. `Catalog.Contracts` += `IProductSalesReader`, `TopProduct(ProductId, Name, Units)`.

**Inventory / Identity**: `ListLowStockAsync` + `LowStockItem(VariantId, Available)`; `GetContactsAsync`.

**Admin (new)**: `AdminModule.cs` (only public type); `Domain/DashboardOrder.cs`, `DashboardRefund.cs`; `Application/DashboardProjector.cs`, `DashboardQueryService.cs`; `Infrastructure/` (EF configs via `IModuleDbModel`); `Api/AdminDashboardController.cs`, `AdminProjectionsController.cs`. References: SharedKernel, Persistence, Orders/Payments/Catalog/Inventory/Identity `.Contracts`. `InternalsVisibleTo` → `Ecommerce.Admin.Tests`, `Ecommerce.Api.IntegrationTests`. New `tests/Ecommerce.Admin.Tests`. Host: reference + `ModuleComposition` (assembly list, `AddAdminModule`). `ArchitectureTests/SolutionLayout.cs`: **add** Admin entries only — no rule loosened.

**Migration** `AddProjectionsAndSaga`: `consumer_checkpoints`, `projection_versions` (PK projector+aggregate_type+aggregate_id), `admin_dashboard_orders` (index `paid_at`, `status`), `admin_dashboard_refunds` (index `succeeded_at`), `catalog_product_sales_daily` (PK product_id+sales_date), `fulfillment_sagas` (unique `order_id`, check on status `Compensating|Compensated`). Money columns `numeric(12,2)` like `orders`.

**HTTP** (all under `/api/v1`, ProblemDetails errors; §9 updated by Rob):
- `GET /admin/dashboard?period=` → `{ period, asOf, currency, kpis|null, ordersByStatus[{status,count}], topProducts[{productId,name,units}], lowStock[{variantId,productName,sku,available}], needsAttention{pendingReturns, deadLetters[{consumer,count}], laggingProjectors[{name,lagSeconds}]} }`. `asOf` = min of the two projectors' `asOf`. Money in the same format as the admin order detail response. Low stock ≤ 20 rows.
- `GET /admin/dashboard/recent-orders?page=&pageSize=` → `{ items[{id, orderNumber, customerEmail, createdAt, grandTotal, status}], page, pageSize, totalCount }`, newest first, includes `PendingPayment`. pageSize 1–50, else 400.
- `POST /admin/orders/{id}/unfulfillable` `{reason}` (1–500 chars) → 200 `{ sagaId, status, refundAmount }`; 404 unknown; 409 wrong status.
- `POST /admin/projections/{name}/replay` → 202; 404 unknown; 409 busy.

**Frontend** `features/admin-dashboard/` (`api.ts`, `types.ts`, `hooks/`, `components/DashboardView.tsx`), `app/admin/index.tsx` renders it (replaces the stub); keys in `lib/query-keys.ts`. Period switch, as-of line, KPIs only when `kpis` non-null, status rows link `/admin/orders?status=X` (filtering is 11.4's), recent orders paging, low stock → `/admin/inventory`, top 5, needs-attention alerts, quick actions (`/admin/products/new`, `/admin/inventory`, `/admin/orders`, `/admin/returns`), error with retry, Refresh action. `features/catalog`: `bestselling` in `list-params` + sort picker; related section on detail. NativeWind classes, no new package.

## 4. Build order (security and invariant tests first)

1. Migration + entities. 2. **Runtime tests red** (T11 via a test projector, sequence gap, T14, T13) → `ProjectionProcessor`/worker/checkpoint → green. 3. Replay + health tests → impl. 4. D11 payload move (existing tests stay green) → D12 `RefundSucceeded` (test first). 5. Admin projector + KPI tests → impl. 6. **Role/400 tests red** for dashboard, recent orders, replay → controllers. 7. Catalog projector, T12, T17 → sort + related. 8. **T16 red** → saga, consumer, ship guard → green. 9. T15. 10. T18. 11. Frontend Jest → screens. 12. Full `verify.sh`.

## 5. Required tests

Numbered (§11.2; §12.3 gives 6b **11–18**; no F test is assigned to 6b). **TF** = test-first, quote the red.

| Test | Where | Proves |
|---|---|---|
| **11 TF** | Api.IntegrationTests `Projections/` | Same `OrderPaid` delivered twice (second pass + direct re-apply of the same message) → revenue and order count once; same for one `RefundSucceeded` |
| 12 | same | `OrderPaid` twice → `catalog_product_sales_daily` and `?sort=bestselling` unchanged on the second delivery |
| **13 TF** | same | Projector not run while N orders are paid and refunded; fake clock +10 min; drain → exact revenue/count/status counts, checkpoint = max sequence; another pass changes nothing; lag alert raised before the catch-up, cleared after |
| 14 | same | Rows inserted v1, v3, v2 (sequence order) → v3 held `Retrying`, status never v3's before v2; after v2 + backoff, v3 applies. v2 never arrives → v3 `DeadLetter` after 5 attempts, `LogCritical`, health `deadLetters` 1; a v ≤ stored is skipped |
| 15 | `Notifications/OrderEmailTests.cs` | Consumer sends, mark is lost (call `HandleAsync`, then run the processor) → second send has the same key; pickup dir holds **one** file (D20) |
| **16 TF** | `Orders/` or `Refunds/` | `unfulfillable` twice + compensation consumer run twice + duplicate refund webhook → **one** saga, one refund row, one provider refund (`saga:{id}:Fulfill`), order `Refunded`, saga `Compensated`, audit (actor, before, after, reason); `ship` → 409 after the mark; consumer retry after its first commit replays, no 409/dead letter |
| 17 | `Projections/` | Fixed dataset: record `?sort=bestselling`, a detail `related`, dashboard top products; replay `catalog.search` (and `admin.dashboard`); drain → identical results and KPIs |
| 18 | same | Projector stopped: webhook → order `Paid`, `GET /orders/{id}` correct; dashboard `asOf` = oldest pending `occurred_at`, lag in `needsAttention` |

Standing challenges → tests: **allowed lag** → D6 + T13/T18 (alert above 60 s, none below; unit-test the lag/asOf calculation with a fake clock) · **duplicates** → T11, T12 · **out-of-order** → T14 · **sequence gaps** → *Gap-commit test* **TF**: tx A draws seq 9 and stays open, B commits seq 10; pass applies 10, checkpoint < 9; A commits; pass applies 9; and the checkpoint never passes a row younger than the window · **replay** → T17 + "replay while busy → 409" · **single saga refund** → T16.

Also required: per endpoint happy, **401, 403 (Customer; Support on unfulfillable/replay)**, 400 (period, pageSize, reason), the 404/409s; Support `kpis` null; auto refund (Test 25 path) leaves revenue unchanged; KPI unit tests (UTC-midnight bounds, new vs returning, AOV 0 orders → 0); `PendingPayment` in recent orders; low-stock edge (= threshold in, +1 out); arch tests green with Admin. Jest: dashboard renders server values, hides KPIs on `null`, shows as-of, status tap route, alerts, error retry; catalog sort param and related section.

**Mutation checks — exactly these** (break, see red, restore, report):
1. Runtime dedupe: move the `processed_events` mark out of the apply tx → T11 red.
2. Checkpoint: advance to the batch's max sequence ignoring the window/non-terminal rule → gap-commit test and T13 red.
3. Saga key: replace `saga:{sagaId}:Fulfill` with a fresh GUID → T16 red (two refunds blocked only by ceiling — assert one refund row).
4. Saga uniqueness: drop the "existing saga → return it" check → T16 double-submit red.
5. Ship guard removed → T16 `ship` 409 red.
6. Support `kpis` null removed → Support test red.
7. Dashboard policy loosened to any authenticated user → Customer 403 red.
8. `unfulfillable` and replay policy loosened to Admin+Support → Support 403 red.

## 6. Review tiers

High: runtime (Persistence), dashboard projector + KPIs (money), `RefundSucceeded`, saga + consumer + ship guard, all role guards, migration. Low: contract reads (recent orders, low stock, contacts), catalog projector + sort + related, Test 15, frontend.

## 7. Risks

- **R1** App context §3.1 lacks deliverable rows for the search projector, best sellers/related, saga and Test 15 although §12.3 puts them in 6b — Nathaniel adds them; nothing beyond §12.3 is planned.
- **R2** D3 assumes no outbox-writing tx lasts > 30 s between `occurred_at` and commit (rule 6 keeps them short). A longer one could be skipped; the gap-commit test pins the rule.
- **R3** A concurrent admin partial refund between `unfulfillable` and compensation makes the saga refund exceed the ceiling → 409 → dead letter (alerted, order stays `Paid`, no shipment possible). Accepted for v1; admin refunds the rest manually.
- **R4** Anti-join cost grows with the outbox; Slice 7 slow-query pass.
- **R5** Provider 13.7 must honour idempotency keys (Test 15).
- **R6** Decision log ~38 KB, over the file rule — split (backlog).
