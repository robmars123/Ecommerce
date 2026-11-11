# Admin UI — Phase 2 tracker

Match the target screenshot across all admin screens. Go top to bottom. Mark a row Done only when its API, screen and tests are in.

**Slice:** P2 (arch §12.3). **Scope:** app context §4.14 and §5.

| # | Do | Module | Status |
|---|---|---|---|
| **A** | **Shell** | | |
| P2.1 | Admin theme tokens (dark and light), one shared file | `components` | Done |
| P2.2 | Shell in `app/admin/_layout.tsx`: sidebar, collapses at phone width | `admin-shell` | Done |
| P2.3 | Sidebar nav, hidden by role. Overview: Dashboard, User Stories. Manage: Orders, Products, Categories, Inventory, Returns, Customers. Insights: Analytics, Settings | `admin-shell` | Done |
| P2.4 | Sidebar footer: name and role | `admin-shell` | Done |
| P2.5 | Get approval for the chart package (`react-native-svg`) in Rob's plan | — | Done |
| P2.6 | Breadcrumb in the top bar | `admin-shell` | Todo |
| P2.7 | Search box with grouped results | `admin-shell` | Todo |
| P2.8 | `GET /api/v1/admin/search?q=`: up to 5 each of orders, products and SKUs. `q` 2–100 characters | Admin | Todo |
| P2.9 | "Live store" pill: shows whether the SignalR connection is up | `admin-shell` | Todo |
| P2.10 | Orders badge: Paid count, links to the filtered list | `admin-shell` | Todo |
| P2.11 | Inventory badge: "N low" | `admin-shell` | Todo |
| P2.12 | Needs attention card: low stock, cancelled, pending returns, dead letters, lag | `admin-shell` | Todo |
| **B** | **Dashboard `/admin`** | | |
| P2.13 | Greeting by device time | `admin-dashboard` | Todo |
| P2.14 | Period filter: Today / 7 days / 30 days | `admin-dashboard` | Todo |
| P2.15 | Refresh button; keeps old figures on error | `admin-dashboard` | Todo |
| P2.16 | As-of line: time, period, currency, lag | `admin-dashboard` | Todo |
| P2.17 | Export: `GET /api/v1/admin/dashboard/export?period=` returns CSV. Admin only | Admin | Todo |
| P2.18 | Tabs: Overview, User Stories, Acceptance Criteria | `admin-dashboard` | Todo |
| P2.19 | User Stories tab: US-01–US-08 (static) | `admin-dashboard` | Todo |
| P2.20 | Acceptance Criteria tab (static) | `admin-dashboard` | Todo |
| P2.21 | KPI cards with secondary lines; never blank. Support sees no revenue | `admin-dashboard` | Todo |
| P2.22 | "vs previous period" chip; the API adds previous revenue | Admin, `admin-dashboard` | Todo |
| P2.23 | Orders card: cancelled and completed (Delivered) in the period | Admin, `admin-dashboard` | Todo |
| P2.24 | Inventory alert banner linking to `/admin/inventory` | `admin-dashboard` | Todo |
| P2.25 | Revenue trend API: hourly buckets for Today, daily for 7 and 30 days, from the projection. Admin only | Admin | Todo |
| P2.26 | Revenue trend line chart with TOTAL, ORDERS and "View report →" | `admin-dashboard` | Todo |
| P2.27 | Orders-by-status donut with legend, % and empty state | `admin-dashboard` | Todo |
| P2.28 | Group the 8 statuses into 4 legend buckets (display only); tapping a bucket filters orders | `admin-dashboard` | Todo |
| P2.29 | Recent orders table restyle | `admin-dashboard` | Todo |
| P2.30 | Top products panel restyle | `admin-dashboard` | Todo |
| P2.31 | Low stock panel restyle | `admin-dashboard` | Todo |
| P2.32 | Quick actions, hidden by role | `admin-dashboard` | Todo |
| P2.33 | Set `Inventory:LowStockThreshold` to 3 in config | Inventory | Todo |
| **C** | **Restyle into the shell (layout only)** | | |
| P2.34 | Orders list, with status filters from the URL | `admin-orders` | Todo |
| P2.35 | Order detail | `admin-orders` | Todo |
| P2.36 | Returns | `admin-orders` | Todo |
| P2.37 | Products list | `admin-catalog` | Todo |
| P2.38 | Product create and edit | `admin-catalog` | Todo |
| P2.39 | Categories | `admin-catalog` | Todo |
| P2.40 | Inventory | `admin-inventory` | Todo |
| P2.41 | Customers list and detail | `admin-customers` | Todo |
| **D** | **New screens** | | |
| P2.42 | `/admin/analytics`: trend and top products. Admin only | `admin-analytics` | Todo |
| P2.43 | `/admin/settings`: currency and threshold, read-only. Admin only | `admin-settings` | Todo |
| P2.44 | `GET /api/v1/admin/settings` | Admin | Todo |
| **E** | **Checks** | | |
| P2.45 | Support gets 403 on export, trend and settings, and never sees revenue | API, `admin-*` | Todo |
| P2.46 | Phone width: no horizontal scroll | `admin-*` | Todo |
| P2.47 | Light and dark mode on every admin screen | `admin-*` | Todo |

**Every new endpoint:** tests for the happy path, 401/403 and 400. **Every screen:** Jest tests. **Restyle rows:** existing tests unchanged.
