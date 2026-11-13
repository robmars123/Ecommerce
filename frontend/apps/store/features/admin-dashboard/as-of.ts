import { PERIODS } from './period';
import type { Dashboard } from './types';

function formatAsOf(iso: string): string {
  return new Date(iso).toLocaleString('en-US', {
    timeZone: 'UTC',
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
    hour12: false,
  });
}

/** The four parts of the as-of line, all from the response (arch 8.8.3). Lag lists only projectors beyond the server's target. */
export function asOfParts(d: Dashboard) {
  const lagging = d.needsAttention.laggingProjectors;
  const maxLag = Math.max(0, ...lagging.map((p) => p.lagSeconds));
  return {
    time: `Figures as of ${formatAsOf(d.asOf)} UTC`,
    period: `Period: ${PERIODS.find((p) => p.value === d.period)?.label ?? d.period}`,
    currency: `Currency: ${d.currency}`,
    lag: lagging.length === 0 ? 'Lag: within target' : `Lag: ${maxLag} s behind`,
    lagging: lagging.length > 0,
  };
}
