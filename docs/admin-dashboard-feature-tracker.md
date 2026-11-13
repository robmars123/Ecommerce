# Admin UI — Phase 2 tracker

Match the target screenshot across all admin screens. Rows are ordered MVP-first: go top to bottom, finishing one tier before starting the next. Row IDs are stable (plans, backlog and decision log cite them), so the numbers are not in order. Mark a row Done only when its API, screen and tests are in.

**Slice:** P2 (arch §12.3). **Scope:** app context §4.14 and §5.

| # | Do | Module | Status |
|---|---|---|---|
| **0** | **Done: shell foundation** | | |
| P2.1 | Admin theme tokens (dark and light), one shared file | `components` | Done |
| P2.2 | Shell in `app/admin/_layout.tsx`: sidebar, collapses at phone width | `admin-shell` | Done |
| P2.3 | Sidebar nav, hidden by role. Overview: Dashboard, User Stories. Manage: Orders, Products, Categories, Inventory, Returns, Customers. Insights: Analytics, Settings | `admin-shell` | Done |
| P2.4 | Sidebar footer: name and role | `admin-shell` | Done |
| P2.5 | Get approval for the chart package (`react-native-svg`) in Rob's plan | — | Done |
| P2.6 | Breadcrumb in the top bar | `admin-shell` | Done |
| P2.7 | Search box with grouped results | `admin-shell` | Done |
| P2.8 | `GET /api/v1/admin/search?q=`: up to 5 each of orders, products and SKUs. `q` 2–100 characters | Admin | Done |
| **1** | **MVP: every daily admin screen usable and themed (layout only)** | | |
| P2.34 | Orders list, with status filters from the URL | `admin-orders` | Done |
| P2.35 | Order detail | `admin-orders` | Done |
| P2.40 | Inventory | `admin-inventory` | Done |
| P2.33 | Set `Inventory:LowStockThreshold` to 3 in config | Inventory | Done |
| P2.37 | Products list | `admin-catalog` | Done |
| P2.38 | Product create and edit | `admin-catalog` | Done |
| P2.39 | Categories | `admin-catalog` | Done |
| P2.36 | Returns | `admin-orders` | Done |
| P2.41 | Customers list and detail | `admin-customers` | Done |
| P2.47 | Light and dark mode on every admin screen | `admin-*` | Done |
| P2.46 | Phone width: no horizontal scroll | `admin-*` | Done |
| **2** | **MVP: dashboard `/admin` essentials** | | |
| P2.21 | KPI cards with secondary lines; never blank. Support sees no revenue | `admin-dashboard` | Done |
| P2.29 | Recent orders table restyle | `admin-dashboard` | Done |
| P2.31 | Low stock panel restyle | `admin-dashboard` | Done |
| P2.30 | Top products panel restyle | `admin-dashboard` | Done |
| P2.24 | Inventory alert banner linking to `/admin/inventory` | `admin-dashboard` | Done |
| P2.32 | Quick actions, hidden by role | `admin-dashboard` | Done |
| P2.14 | Period filter: Today / 7 days / 30 days | `admin-dashboard` | Done |
| P2.15 | Refresh button; keeps old figures on error | `admin-dashboard` | Done |
| P2.16 | As-of line: time, period, currency, lag | `admin-dashboard` | Done |
| P2.13 | Greeting by device time | `admin-dashboard` | Done |
| **3** | **Post-MVP: shell shortcuts** | | |
| P2.10 | Orders badge: Paid count, links to the filtered list | `admin-shell` | Done |
| P2.11 | Inventory badge: "N low" | `admin-shell` | Done |
| P2.12 | Needs attention card: low stock, cancelled, pending returns, dead letters, lag | `admin-shell` | Done |
| **4** | **Post-MVP: charts, insights, settings, export** | | |
| P2.23 | Orders card: cancelled and completed (Delivered) in the period | Admin, `admin-dashboard` | Done |
| P2.22 | "vs previous period" chip; the API adds previous revenue | Admin, `admin-dashboard` | Done |
| P2.25 | Revenue trend API: hourly buckets for Today, daily for 7 and 30 days, from the projection. Admin only | Admin | Done |
| P2.26 | Revenue trend line chart with TOTAL, ORDERS and "View report →" | `admin-dashboard` | Done |
| P2.27 | Orders-by-status donut with legend, % and empty state | `admin-dashboard` | Done |
| P2.28 | Group the 8 statuses into 4 legend buckets (display only); tapping a bucket filters orders | `admin-dashboard` | Done |
| P2.42 | `/admin/analytics`: trend and top products. Admin only | `admin-analytics` | Done |
| P2.44 | `GET /api/v1/admin/settings` | Admin | Done |
| P2.43 | `/admin/settings`: currency and threshold, read-only. Admin only | `admin-settings` | Done |
| P2.17 | Export: `GET /api/v1/admin/dashboard/export?period=` returns CSV. Admin only | Admin | Done |
| P2.45 | Support gets 403 on export, trend and settings, and never sees revenue | API, `admin-*` | Done |
| **5** | **Later: nice-to-have** | | |
| P2.18 | Tabs: Overview, User Stories, Acceptance Criteria | `admin-dashboard` | Done |
| P2.19 | User Stories tab: US-01–US-08 (static) | `admin-dashboard` | Done |
| P2.20 | Acceptance Criteria tab (static) | `admin-dashboard` | Done |
| P2.9 | "Live store" pill: shows whether the SignalR connection is up (deprioritized) | `admin-shell` | Done |
| **C** | **Customer order detail: [feature] items from `docs/design-refs/northline-order-detail.md` (storefront, not admin)** | | |
| P2.48 | Copy order number button in the order header | `orders` | Done |
| P2.49 | Reorder: put the order's items back in the cart | `orders`, Cart | Done |
| P2.50 | Cancellation alert with an explanation, and the pill note text; the API returns the text | Orders, `orders` | Done |
| P2.51 | Item thumbnail and colour attribute on order items; the API returns them | Orders, `orders` | Done |
| P2.52 | Payment card: method, amount due, captured; the API returns them | Orders, Payments, `orders` | Done |
| P2.53 | Timeline entry description text | Orders, `orders` | Done |
| P2.54 | Totals note under the Total ("never charged …") | `orders` | Todo |
| P2.55 | Top bar avatar initial and cart count badge | `components`, Cart | Todo |

**Every new endpoint:** tests for the happy path, 401/403 and 400. **Every screen:** Jest tests. **Restyle rows:** existing tests unchanged. **Theme rows:** P2.47 and P2.46 close tier 1 as a sweep; each tier-1 row already uses theme tokens only.
