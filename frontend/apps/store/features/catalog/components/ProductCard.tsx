import { Link } from 'expo-router';
import { StyleSheet, Text, View } from 'react-native';

import type { ProductListItem } from '../types';
import { PriceText } from './PriceText';
import { ProductImageView } from './ProductImageView';
import { StockBadge } from './StockBadge';

export function ProductCard({ item }: { item: ProductListItem }) {
  return (
    <Link
      href={`/products/${encodeURIComponent(item.slug)}`}
      accessibilityRole="link"
      accessibilityLabel={item.name}
      style={styles.link}
    >
      <View style={styles.card}>
        <ProductImageView
          url={item.primaryImage?.url ?? null}
          altText={item.primaryImage?.altText ?? ''}
          fallbackLabel={item.name}
        />
        <Text style={styles.name} numberOfLines={2}>
          {item.name}
        </Text>
        <Text style={styles.category}>{item.category.name}</Text>
        <PriceText price={item.price} compareAtPrice={item.compareAtPrice} />
        <StockBadge status={item.stockStatus} />
      </View>
    </Link>
  );
}

const styles = StyleSheet.create({
  link: { flex: 1, minHeight: 44 },
  card: { flex: 1, gap: 4, width: '100%' },
  name: { fontSize: 16, fontWeight: '600' },
  category: { fontSize: 14, opacity: 0.7 },
});
