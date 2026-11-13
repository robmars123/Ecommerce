import { parseDashboardPeriod } from './period';

describe('parseDashboardPeriod', () => {
  it.each(['today', '7d', '30d'] as const)('P1 keeps %s', (value) => {
    expect(parseDashboardPeriod(value)).toBe(value);
  });

  it.each([undefined, '', '7D', '1y', 'yesterday', ['7d', '30d']])('P2 falls back to today for %j', (value) => {
    expect(parseDashboardPeriod(value)).toBe('today');
  });
});
