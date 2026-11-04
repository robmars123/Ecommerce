import { StyleSheet, Text, View } from 'react-native';

import { formatMoney } from '@/lib/money';

import type { Money } from '../types';

type PriceTextProps = {
  price: Money;
  compareAtPrice?: Money | null;
};

export function PriceText({ price, compareAtPrice }: PriceTextProps) {
  return (
    <View style={styles.row}>
      <Text style={styles.price}>{formatMoney(price)}</Text>
      {compareAtPrice ? (
        <Text accessibilityLabel={`Was ${formatMoney(compareAtPrice)}`} style={styles.compareAt}>
          {formatMoney(compareAtPrice)}
        </Text>
      ) : null}
    </View>
  );
}

const styles = StyleSheet.create({
  row: { flexDirection: 'row', alignItems: 'baseline', gap: 8 },
  price: { fontSize: 16, fontWeight: '600' },
  compareAt: { fontSize: 14, textDecorationLine: 'line-through', opacity: 0.7 },
});
