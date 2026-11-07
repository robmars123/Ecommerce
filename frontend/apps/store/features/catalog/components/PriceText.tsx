import { StyleSheet, Text, View } from 'react-native';

import { formatMoney } from '@/lib/money';

import type { Money } from '../types';

type PriceTextProps = {
  price: Money;
  compareAtPrice?: Money | null;
};

export function PriceText({ price, compareAtPrice }: PriceTextProps) {
  return (
    <View className="flex-row items-baseline gap-sm">
      <Text className="text-title font-bold text-foreground">{formatMoney(price)}</Text>
      {compareAtPrice ? (
        <Text
          accessibilityLabel={`Was ${formatMoney(compareAtPrice)}`}
          className="text-small text-muted"
          style={styles.compareAt}
        >
          {formatMoney(compareAtPrice)}
        </Text>
      ) : null}
    </View>
  );
}

// className does not resolve under Jest, so the asserted line-through stays a style (arch §5.5 fallback).
const styles = StyleSheet.create({
  compareAt: { textDecorationLine: 'line-through' },
});
