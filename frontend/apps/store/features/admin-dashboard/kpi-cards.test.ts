import { makeDashboard } from '@/tests/admin-dashboard-fixtures';

import { kpiCards } from './kpi-cards';
import type { DashboardKpis, DashboardPeriod } from './types';

const usd = (amount: string) => ({ amount, currency: 'USD' });
const adminKpis = (): DashboardKpis => makeDashboard().kpis as DashboardKpis;
const outcomes = { delivered: 3, cancelled: 1 };

describe('kpiCards', () => {
  it('K1 maps the server KPIs to four cards in order', () => {
    expect(kpiCards(adminKpis(), 'today', outcomes)).toEqual([
      {
        label: 'Revenue',
        value: '$1,234.50',
        secondary: 'Paid minus refunds today',
        change: { pill: '+23.5%', tone: 'success', text: 'vs $1,000.00 previous period' },
      },
      { label: 'Orders', value: '7', secondary: 'Paid today', detail: '3 delivered · 1 cancelled' },
      { label: 'Average order value', value: '$176.36', secondary: 'Across 7 orders' },
      { label: 'Customers', value: '5', secondary: '3 new · 2 returning' },
    ]);
  });

  it('K2 is never blank for a zero period', () => {
    const cards = kpiCards(
      {
        revenue: usd('0.00'),
        orderCount: 0,
        averageOrderValue: usd('0.00'),
        customers: { total: 0, new: 0, returning: 0 },
        previousRevenue: usd('0.00'),
        revenueChangePercent: null,
      },
      '7d',
      { delivered: 0, cancelled: 0 },
    );

    expect(cards.map((c) => c.value)).toEqual(['$0.00', '0', '$0.00', '0']);
    expect(cards.map((c) => c.secondary)).toEqual([
      'Paid minus refunds in the last 7 days',
      'No paid orders in the last 7 days',
      'No paid orders in the last 7 days',
      'No customers in the last 7 days',
    ]);
    for (const c of cards) {
      expect(c.value).not.toBe('');
      expect(c.secondary).not.toBe('');
    }
  });

  it.each<[DashboardPeriod, string]>([
    ['today', 'Paid minus refunds today'],
    ['7d', 'Paid minus refunds in the last 7 days'],
    ['30d', 'Paid minus refunds in the last 30 days'],
  ])('K3 words the period %s', (period, expected) => {
    expect(kpiCards(adminKpis(), period, outcomes)[0].secondary).toBe(expected);
  });

  it('K4 uses the singular for one order', () => {
    const cards = kpiCards({ ...adminKpis(), orderCount: 1 }, 'today', outcomes);

    expect(cards[2].secondary).toBe('Across 1 order');
    expect(cards[1].secondary).toBe('Paid today');
  });

  it('K5 passes server money through without clamping', () => {
    const cards = kpiCards({ ...adminKpis(), revenue: usd('-5.00'), customers: { total: 2, new: 0, returning: 2 } }, 'today', outcomes);

    expect(cards[0].value).toBe('-$5.00');
    expect(cards[3].secondary).toBe('0 new · 2 returning');
  });

  it('J1 words the delivered and cancelled counts on the Orders card, zeros shown', () => {
    expect(kpiCards(adminKpis(), 'today', { delivered: 3, cancelled: 1 })[1].detail).toBe('3 delivered · 1 cancelled');
    expect(kpiCards(adminKpis(), 'today', { delivered: 0, cancelled: 0 })[1].detail).toBe('0 delivered · 0 cancelled');
  });

  it.each<[number | null, string | null, 'success' | 'danger' | 'muted']>([
    [23.5, '+23.5%', 'success'],
    [-4, '-4.0%', 'danger'],
    [0, '0.0%', 'muted'],
    [null, null, 'muted'],
  ])('J2 the revenue change %s gives pill %s and tone %s, text from the server previous revenue', (percent, pill, tone) => {
    const kpis = { ...adminKpis(), revenueChangePercent: percent, previousRevenue: percent === null ? usd('0.00') : usd('1000.00') };

    expect(kpiCards(kpis, 'today', outcomes)[0].change).toEqual({
      pill,
      tone,
      text: percent === null ? 'vs $0.00 previous period' : 'vs $1,000.00 previous period',
    });
  });
});
