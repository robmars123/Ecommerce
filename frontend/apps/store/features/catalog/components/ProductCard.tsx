import { Link } from 'expo-router';
import { Pressable, Text, View } from 'react-native';

import type { ProductListItem } from '../types';
import { PriceText } from './PriceText';
import { ProductImageView } from './ProductImageView';
import { QuickAdd } from './QuickAdd';
import { StockBadge } from './StockBadge';

export function ProductCard({ item }: { item: ProductListItem }) {
  const href = `/products/${encodeURIComponent(item.slug)}`;
  return (
    <View className="min-h-touch w-full flex-1 overflow-hidden rounded-card border border-border bg-surface">
      {/* The card body is the product link; the quick-add button is its sibling, never nested inside it. */}
      <Link href={href} asChild>
        <Pressable accessibilityRole="link" accessibilityLabel={item.name} className="min-h-touch flex-1">
          <ProductImageView
            url={item.primaryImage?.url ?? null}
            altText={item.primaryImage?.altText ?? ''}
            fallbackLabel={item.name}
          />
          <View className="gap-xs p-lg">
            {/* Upper-cased by the uppercase class, never in the string. */}
            <Text className="text-caption uppercase tracking-wide text-muted">{item.category.name}</Text>
            <Text className="text-title text-foreground" numberOfLines={2}>
              {item.name}
            </Text>
            <StockBadge status={item.stockStatus} />
            <PriceText price={item.price} compareAtPrice={item.compareAtPrice} />
          </View>
        </Pressable>
      </Link>
      <QuickAdd item={item} />
    </View>
  );
}
