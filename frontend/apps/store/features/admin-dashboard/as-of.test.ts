import { makeDashboard } from '@/tests/admin-dashboard-fixtures';

import { asOfParts } from './as-of';

describe('asOfParts', () => {
  it('A1 words time, period, currency and lag from the response', () => {
    expect(asOfParts(makeDashboard())).toEqual({
      time: 'Figures as of Nov 8, 2026, 14:30 UTC',
      period: 'Period: Today',
      currency: 'Currency: USD',
      lag: 'Lag: within target',
      lagging: false,
    });
  });

  it('A2 reports the largest lag, not the first or the sum', () => {
    const d = makeDashboard({
      needsAttention: {
        pendingReturns: 0,
        deadLetters: [],
        laggingProjectors: [
          { name: 'orders-dashboard', lagSeconds: 75 },
          { name: 'sales', lagSeconds: 130 },
        ],
      },
    });
    const parts = asOfParts(d);
    expect(parts.lag).toBe('Lag: 130 s behind');
    expect(parts.lagging).toBe(true);
  });

  it.each([
    ['7d', 'Period: 7 days'],
    ['30d', 'Period: 30 days'],
  ] as const)('A3 labels %s', (period, label) => {
    expect(asOfParts(makeDashboard({ period })).period).toBe(label);
  });

  it('A4 passes the currency through', () => {
    expect(asOfParts(makeDashboard({ currency: 'EUR' })).currency).toBe('Currency: EUR');
  });
});
