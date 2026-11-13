import { formatMoney } from '@/lib/money';

import type { DashboardKpis, DashboardPeriod } from './types';

export type KpiChange = { pill: string | null; tone: 'success' | 'danger' | 'muted'; text: string };

export type KpiCard = { label: string; value: string; secondary: string; detail?: string; change?: KpiChange };

/** The pill shows the server's percent; its tone is that number's sign. null means the server had no previous revenue to compare. */
function revenueChange(kpis: DashboardKpis): KpiChange {
  const percent = kpis.revenueChangePercent;
  const text = `vs ${formatMoney(kpis.previousRevenue)} previous period`;
  if (percent === null) return { pill: null, tone: 'muted', text };
  const tone = percent > 0 ? 'success' : percent < 0 ? 'danger' : 'muted';
  return { pill: `${percent > 0 ? '+' : ''}${percent.toFixed(1)}%`, tone, text };
}

const PERIOD_PHRASE: Record<DashboardPeriod, string> = {
  today: 'today',
  '7d': 'in the last 7 days',
  '30d': 'in the last 30 days',
};

/** The four sales cards. Every figure is the server's; the client only words the line under it. */
export function kpiCards(kpis: DashboardKpis, period: DashboardPeriod, outcomes: { delivered: number; cancelled: number }): KpiCard[] {
  const phrase = PERIOD_PHRASE[period];
  const noOrders = kpis.orderCount === 0;
  return [
    { label: 'Revenue', value: formatMoney(kpis.revenue), secondary: `Paid minus refunds ${phrase}`, change: revenueChange(kpis) },
    {
      label: 'Orders',
      value: String(kpis.orderCount),
      secondary: noOrders ? `No paid orders ${phrase}` : `Paid ${phrase}`,
      detail: `${outcomes.delivered} delivered · ${outcomes.cancelled} cancelled`,
    },
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
