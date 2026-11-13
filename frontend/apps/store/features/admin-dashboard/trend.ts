import type { RevenueTrend } from './types';

export type TrendGeometry = {
  points: { x: number; y: number; label: string }[];
  linePath: string;
  areaPath: string;
  gridYs: number[];
  /** y of the chart floor: where an all-zero line sits and where the area fill ends. */
  baselineY: number;
};

// The target layout is drawn for a 170 px tall chart: gridlines at 30, 75, 120 and a flat line at 135 (CSS doc 6).
const DESIGN_HEIGHT = 170;
const TOP = 30;
const FLOOR = 135;
const GRID = [30, 75, 120];

/**
 * Pixel positions for the line chart. The numbers are parsed from the server strings for y-positions only (D10): every
 * money label on screen is the server string. The range always includes zero, so a negative bucket sits below it.
 */
export function trendGeometry(trend: RevenueTrend, width: number, height: number): TrendGeometry {
  const scale = height / DESIGN_HEIGHT;
  const top = TOP * scale;
  const floor = FLOOR * scale;
  const values = trend.buckets.map((b) => Number(b.revenue.amount));
  const lo = Math.min(0, ...values);
  const hi = Math.max(0, ...values);
  const yOf = (v: number) => (hi === lo ? floor : floor - ((v - lo) / (hi - lo)) * (floor - top));
  const last = trend.buckets.length - 1;
  const points = trend.buckets.map((b, i) => ({
    x: last === 0 ? width : (i / last) * width,
    y: yOf(values[i]),
    label: b.revenue.amount,
  }));
  const linePath = points.map((p, i) => `${i === 0 ? 'M' : 'L'} ${p.x} ${p.y}`).join(' ');
  const areaPath = points.length === 0 ? '' : `${linePath} L ${points[points.length - 1].x} ${floor} L ${points[0].x} ${floor} Z`;
  return { points, linePath, areaPath, gridYs: GRID.map((g) => g * scale), baselineY: floor };
}

/** An axis label in UTC, like the buckets: "14:00" for hours, "Nov 12" for days. */
export function bucketLabel(start: string, granularity: RevenueTrend['granularity']): string {
  const date = new Date(start);
  return granularity === 'hour'
    ? `${String(date.getUTCHours()).padStart(2, '0')}:00`
    : date.toLocaleDateString('en-US', { timeZone: 'UTC', month: 'short', day: 'numeric' });
}
