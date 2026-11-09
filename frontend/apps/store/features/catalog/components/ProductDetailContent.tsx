import { Stack } from 'expo-router';
import { useState } from 'react';
import { Pressable, ScrollView, Text, View } from 'react-native';

import { AddToCartControl } from '@/features/cart/components/AddToCartControl';

import type { ProductDetail } from '../types';
import { PriceText } from './PriceText';
import { ProductCard } from './ProductCard';
import { ProductImageView } from './ProductImageView';
import { StockBadge } from './StockBadge';
import { VariantSelector } from './VariantSelector';

type ProductDetailContentProps = {
  product: ProductDetail;
  /** The admin preview shows the page but must not offer to buy it. */
  hideAddToCart?: boolean;
};

/** The loaded product page. Shared by the storefront and the admin preview, so the preview is the real thing. */
export function ProductDetailContent({ product, hideAddToCart }: ProductDetailContentProps) {
  const [imageIndex, setImageIndex] = useState(0);
  const [variantId, setVariantId] = useState<string | undefined>(undefined);

  const variant = product.variants.find((v) => v.id === variantId) ?? product.variants[0];
  const mainImage = product.images[imageIndex] ?? product.images[0];

  return (
    <ScrollView contentContainerClassName="w-full max-w-content gap-xl self-center p-lg">
      <Stack.Screen options={{ title: product.name }} />
      <View className="gap-xl lg:flex-row">
        <View className="gap-md lg:flex-1">
          <ProductImageView
            url={mainImage?.url ?? null}
            altText={mainImage?.altText ?? ''}
            fallbackLabel={product.name}
            maxWidth={560}
          />
          {product.images.length > 1 ? (
            <View className="flex-row flex-wrap gap-sm">
              {product.images.map((image, index) => (
                <Pressable
                  key={`${index}-${image.url ?? 'none'}`}
                  accessibilityRole="button"
                  accessibilityLabel={`Show image ${index + 1}: ${image.altText || product.name}`}
                  accessibilityState={{ selected: index === imageIndex }}
                  onPress={() => setImageIndex(index)}
                  className={`min-h-touch min-w-touch rounded-control border-2 p-xs ${index === imageIndex ? 'border-primary' : 'border-transparent'}`}
                >
                  <ProductImageView url={image.url} altText={image.altText} fallbackLabel={product.name} width={56} />
                </Pressable>
              ))}
            </View>
          ) : null}
        </View>

        <View className="gap-md lg:flex-1">
          <Text className="text-caption uppercase tracking-wide text-muted">{product.category.name}</Text>
          <Text accessibilityRole="header" className="text-display font-semibold text-foreground">
            {product.name}
          </Text>
          {variant ? <PriceText price={variant.price} compareAtPrice={variant.compareAtPrice} /> : null}
          <Text className="text-body text-muted">{product.description}</Text>

          {variant ? (
            <>
              <VariantSelector variants={product.variants} selectedId={variant.id} onSelect={setVariantId} />
              <StockBadge status={variant.stockStatus} />
              {hideAddToCart ? null : <AddToCartControl key={variant.id} product={product} variant={variant} />}
              <View className="flex-row gap-sm border-t border-border pt-md">
                <Text className="text-small text-muted">SKU</Text>
                <Text className="text-small text-foreground">{variant.sku}</Text>
              </View>
            </>
          ) : null}
        </View>
      </View>

      {product.related.length > 0 ? (
        <View className="gap-md">
          <Text accessibilityRole="header" className="text-heading font-semibold text-foreground">
            Related products
          </Text>
          <View className="gap-md lg:flex-row">
            {product.related.map((item) => (
              <View key={item.id} className="lg:flex-1">
                <ProductCard item={item} />
              </View>
            ))}
          </View>
        </View>
      ) : null}
    </ScrollView>
  );
}
