import { StyleSheet, Text } from 'react-native';

import type { StockStatus } from '../types';

const LABELS: Record<Exclude<StockStatus, 'Unknown'>, string> = {
  InStock: 'In stock',
  Low: 'Low stock',
  Out: 'Out of stock',
};

/** Unknown renders nothing: we never claim "In stock" without data. Text is always present, never colour-only. */
export function StockBadge({ status }: { status: StockStatus }) {
  if (status === 'Unknown') {
    return null;
  }
  return <Text style={styles.badge}>{LABELS[status]}</Text>;
}

const styles = StyleSheet.create({
  badge: { fontSize: 14, fontWeight: '600' },
});
