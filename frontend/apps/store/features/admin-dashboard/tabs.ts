export type DashboardTab = 'overview' | 'stories' | 'criteria';

export const DASHBOARD_TABS: readonly { tab: DashboardTab; label: string }[] = [
  { tab: 'overview', label: 'Overview' },
  { tab: 'stories', label: 'User Stories' },
  { tab: 'criteria', label: 'Acceptance Criteria' },
];

/** Reads ?tab= (exact match, first value of an array); anything else is Overview, like parseDashboardPeriod. */
export function parseDashboardTab(raw: string | string[] | undefined): DashboardTab {
  const value = Array.isArray(raw) ? raw[0] : raw;
  return DASHBOARD_TABS.find((t) => t.tab === value)?.tab ?? 'overview';
}
