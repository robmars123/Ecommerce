import { formatMoney } from '@/lib/money';

import type { DashboardKpis, DashboardPeriod } from './types';

export type KpiCard = { label: string; value: string; secondary: string };

const PERIOD_PHRASE: Record<DashboardPeriod, string> = {
  today: 'today',
  '7d': 'in the last 7 days',
  '30d': 'in the last 30 days',
};

/** The four sales cards. Every figure is the server's; the client only words the line under it. */
export function kpiCards(kpis: DashboardKpis, period: DashboardPeriod): KpiCard[] {
  const phrase = PERIOD_PHRASE[period];
  const noOrders = kpis.orderCount === 0;
  return [
    { label: 'Revenue', value: formatMoney(kpis.revenue), secondary: `Paid minus refunds ${phrase}` },
    { label: 'Orders', value: String(kpis.orderCount), secondary: noOrders ? `No paid orders ${phrase}` : `Paid ${phrase}` },
    {
      label: 'Average order value',
      value: formatMoney(kpis.averageOrderValue),
      secondary: noOrders ? `No paid orders ${phrase}` : `Across ${kpis.orderCount} ${kpis.orderCount === 1 ? 'order' : 'orders'}`,
    },
    {
      label: 'Customers',
      value: String(kpis.customers.total),
      secondary:
        kpis.customers.total === 0 ? `No customers ${phrase}` : `${kpis.customers.new} new · ${kpis.customers.returning} returning`,
    },
  ];
}
