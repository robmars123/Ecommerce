import type { DashboardPeriod } from './types';

export const PERIODS: { value: DashboardPeriod; label: string }[] = [
  { value: 'today', label: 'Today' },
  { value: '7d', label: '7 days' },
  { value: '30d', label: '30 days' },
];

/** Reads ?period= like the server does (exact match); anything else is Today (decision log: Admin dashboard period, refresh, as-of line). */
export function parseDashboardPeriod(raw: string | string[] | undefined): DashboardPeriod {
  return PERIODS.find((p) => p.value === raw)?.value ?? 'today';
}
