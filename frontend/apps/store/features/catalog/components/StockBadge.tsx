import { Text, View } from 'react-native';

import type { StockStatus } from '../types';

const LABELS: Record<Exclude<StockStatus, 'Unknown'>, string> = {
  InStock: 'In stock',
  Low: 'Low stock',
  Out: 'Out of stock',
};

const DOT: Record<Exclude<StockStatus, 'Unknown'>, string> = {
  InStock: 'bg-success',
  Low: 'bg-warning',
  Out: 'bg-danger',
};

/** Unknown renders nothing: we never claim "In stock" without data. Text is always present, never colour-only. */
export function StockBadge({ status }: { status: StockStatus }) {
  if (status === 'Unknown') {
    return null;
  }
  return (
    <View className="flex-row items-center gap-sm">
      <View className={`h-sm w-sm rounded-pill ${DOT[status]}`} />
      <Text className="text-small font-semibold text-foreground">{LABELS[status]}</Text>
    </View>
  );
}
