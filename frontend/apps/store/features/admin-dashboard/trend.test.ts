import { bucketLabel, trendGeometry } from './trend';
import type { RevenueTrend } from './types';

function trendOf(amounts: string[]): RevenueTrend {
  return {
    period: '7d',
    granularity: 'day',
    asOf: '2026-11-12T10:00:00Z',
    currency: 'USD',
    total: { amount: '0.00', currency: 'USD' },
    orderCount: 0,
    buckets: amounts.map((amount, i) => ({
      start: `2026-11-${String(6 + i).padStart(2, '0')}T00:00:00Z`,
      revenue: { amount, currency: 'USD' },
      orders: 0,
    })),
  };
}

describe('trendGeometry', () => {
  it('has one point per bucket, from x 0 to the width, the maximum at the top', () => {
    const g = trendGeometry(trendOf(['10.00', '40.00', '20.00']), 560, 170);

    expect(g.points).toHaveLength(3);
    expect([g.points[0].x, g.points[2].x]).toEqual([0, 560]);
    expect(g.points[1].y).toBe(30);
    expect(g.points[0].y).toBeGreaterThan(g.points[2].y);
    expect(g.linePath.startsWith('M 0 ')).toBe(true);
    expect(g.areaPath.endsWith('Z')).toBe(true);
    expect(g.gridYs).toEqual([30, 75, 120]);
  });

  it('puts a negative bucket below the zero line', () => {
    const g = trendGeometry(trendOf(['20.00', '0.00', '-5.00']), 560, 170);

    expect(g.points[2].y).toBeGreaterThan(g.points[1].y);
    expect(g.points[2].y).toBe(135);
    expect(g.points[0].y).toBe(30);
  });

  it('draws an all-zero series as a flat line on the baseline', () => {
    const g = trendGeometry(trendOf(['0.00', '0.00', '0.00']), 560, 170);

    expect(new Set(g.points.map((p) => p.y))).toEqual(new Set([135]));
  });

  it('draws one bucket as one point', () => {
    const g = trendGeometry(trendOf(['12.00']), 560, 170);

    expect(g.points).toHaveLength(1);
    expect(g.points[0].x).toBe(560);
  });
});

describe('bucketLabel', () => {
  it('is the UTC hour for hourly buckets and the UTC month and day for daily ones', () => {
    expect(bucketLabel('2026-11-12T14:00:00Z', 'hour')).toBe('14:00');
    expect(bucketLabel('2026-11-12T00:00:00Z', 'hour')).toBe('00:00');
    expect(bucketLabel('2026-11-12T00:00:00Z', 'day')).toBe('Nov 12');
  });
});
