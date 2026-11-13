export type UserStory = {
  id: string;
  title: string;
  quote: string;
  priority: 'High' | 'Medium';
  /** Tracker rows that shipped the story. Only Done rows. */
  rows: string[];
  criteria: string[];
};

/** Static copy for the User Stories and Acceptance Criteria tabs (P2.19, P2.20). Every line restates a shipped tracker row, nothing more. */
export const USER_STORIES: readonly UserStory[] = [
  {
    id: 'US-01',
    title: 'Admin shell and role-based navigation',
    quote:
      'As an admin user, I want one frame with a sidebar and a top bar, so I can move between admin screens and see only what my role allows.',
    priority: 'High',
    rows: ['P2.2', 'P2.3', 'P2.4', 'P2.6', 'P2.46', 'P2.47'],
    criteria: [
      'The sidebar groups Overview, Manage and Insights and hides items the role may not open.',
      'The sidebar footer shows the signed-in name and role.',
      'The top bar shows a breadcrumb for the current admin page.',
      'At phone width the sidebar collapses and no screen scrolls sideways.',
      'Every admin screen works in light and dark mode.',
    ],
  },
  {
    id: 'US-02',
    title: 'Find an order, product or SKU from anywhere',
    quote: 'As an admin user, I want a search box in the top bar, so I can jump straight to an order, a product or a SKU.',
    priority: 'Medium',
    rows: ['P2.7', 'P2.8'],
    criteria: ['Results are grouped into orders, products and SKUs, up to 5 of each.', 'The search needs 2 to 100 characters.'],
  },
  {
    id: 'US-03',
    title: "See today's or the period's figures at a glance",
    quote: 'As an admin user, I want the key figures for Today, 7 days or 30 days on one screen, so I know how the store is doing.',
    priority: 'High',
    rows: ['P2.13', 'P2.14', 'P2.15', 'P2.16', 'P2.21', 'P2.22', 'P2.23'],
    criteria: [
      'The greeting follows the time of day on the device.',
      'The period switch offers Today, 7 days and 30 days.',
      'Refresh keeps the old figures when it fails.',
      'An as-of line shows the time, the period, the currency and the lag.',
      'Each figure card has a secondary line and is never blank.',
      'The revenue card shows a change against the previous period, and the orders card shows cancelled and delivered orders.',
    ],
  },
  {
    id: 'US-04',
    title: 'See orders by status and open the filtered list',
    quote: 'As an admin user, I want to see how orders split by status, so I can open the list for one status in a tap.',
    priority: 'High',
    rows: ['P2.27', 'P2.28', 'P2.34', 'P2.10'],
    criteria: [
      'The donut shows the 8 statuses in 4 legend buckets, with an empty state.',
      'A single-status bucket links to the filtered orders list; the mixed buckets (In progress, Cancelled or refunded) show one link per status.',
      'The orders list reads its status filter from the URL.',
      'The Orders item in the sidebar shows the Paid count and links to the Paid list.',
    ],
  },
  {
    id: 'US-05',
    title: 'Revenue trend, analytics and CSV export (Admin)',
    quote:
      'As an admin, I want a revenue trend, an analytics page and a CSV export, so I can follow sales over time and take the figures with me.',
    priority: 'Medium',
    rows: ['P2.25', 'P2.26', 'P2.42', 'P2.17'],
    criteria: [
      'The trend has hourly buckets for Today and daily buckets for 7 and 30 days.',
      'The line chart shows the total and the order count, with a View report link.',
      'The analytics page shows the trend and the top products.',
      'Export CSV downloads a CSV file on web and opens the share sheet on Android and iOS.',
    ],
  },
  {
    id: 'US-06',
    title: 'Act on low stock and items that need attention',
    quote: 'As an admin user, I want low stock and open problems pointed out, so I can act before a customer notices.',
    priority: 'High',
    rows: ['P2.24', 'P2.31', 'P2.11', 'P2.12'],
    criteria: [
      'A banner links to the inventory page when stock is running low.',
      'The low stock panel lists each variant with how many are left.',
      'The Inventory item in the sidebar shows how many are low.',
      'The needs attention card lists low stock, cancelled orders, pending returns, dead letters and lag.',
    ],
  },
  {
    id: 'US-07',
    title: 'Read-only store settings (Admin)',
    quote: 'As an admin, I want to see the store currency and the low-stock threshold, so I know which values the figures use.',
    priority: 'Medium',
    rows: ['P2.43', 'P2.44'],
    criteria: [
      'The settings page shows the currency and the threshold and cannot change them.',
      'Only an Admin can open the settings page or read the settings from the API.',
    ],
  },
  {
    id: 'US-08',
    title: 'Support sees only what Support may',
    quote: 'As a support user, I want a dashboard without sales data, so I work with orders and customers and nothing else.',
    priority: 'High',
    rows: ['P2.45', 'P2.32'],
    criteria: [
      'Support gets 403 on export, trend and settings.',
      'Support never sees revenue.',
      'Quick actions are hidden when the role may not open them.',
    ],
  },
];

/** The tracker footer rules, shown as the Definition of Done bar. */
export const DEFINITION_OF_DONE: readonly string[] = [
  'Every new endpoint has tests for the happy path, 401/403 and 400.',
  'Every screen has Jest tests.',
  'Restyle rows leave the existing tests unchanged.',
  'Theme rows use theme tokens only.',
];

export function storyStats(stories: readonly UserStory[]): { stories: number; criteria: number } {
  return { stories: stories.length, criteria: stories.reduce((total, s) => total + s.criteria.length, 0) };
}
