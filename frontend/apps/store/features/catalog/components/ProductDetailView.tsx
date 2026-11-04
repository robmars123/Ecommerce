import { Link, Stack } from 'expo-router';
import { useState } from 'react';
import { Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';

import { ErrorState } from '@/components/ErrorState';
import { LoadingState } from '@/components/LoadingState';
import { ApiError, userMessage } from '@/lib/api-client';

import { useProductQuery } from '../hooks/useProductQuery';
import type { ProductDetail } from '../types';
import { PriceText } from './PriceText';
import { ProductImageView } from './ProductImageView';
import { StockBadge } from './StockBadge';
import { VariantSelector } from './VariantSelector';

export function ProductDetailView({ slug }: { slug: string }) {
  const query = useProductQuery(slug);

  if (query.data) {
    return <ProductDetailContent product={query.data} />;
  }

  return (
    <View style={styles.container}>
      <Stack.Screen options={{ title: 'Product' }} />
      <Text accessibilityRole="header" style={styles.title}>
        Product
      </Text>
      {query.isError ? (
        query.error instanceof ApiError && query.error.kind === 'not-found' ? (
          <View style={styles.notFound}>
            <Text style={styles.body}>Product not found</Text>
            <Link href="/products" style={styles.link}>
              Browse products
            </Link>
          </View>
        ) : (
          <ErrorState message={userMessage(query.error)} onRetry={() => void query.refetch()} />
        )
      ) : (
        <LoadingState />
      )}
    </View>
  );
}

function ProductDetailContent({ product }: { product: ProductDetail }) {
  const [imageIndex, setImageIndex] = useState(0);
  const [variantId, setVariantId] = useState<string | undefined>(undefined);

  const variant = product.variants.find((v) => v.id === variantId) ?? product.variants[0];
  const mainImage = product.images[imageIndex] ?? product.images[0];

  return (
    <ScrollView contentContainerStyle={styles.container}>
      <Stack.Screen options={{ title: product.name }} />
      <Text accessibilityRole="header" style={styles.title}>
        {product.name}
      </Text>
      <Text style={styles.category}>{product.category.name}</Text>

      <ProductImageView
        url={mainImage?.url ?? null}
        altText={mainImage?.altText ?? ''}
        fallbackLabel={product.name}
        maxWidth={480}
      />
      {product.images.length > 1 ? (
        <View style={styles.thumbs}>
          {product.images.map((image, index) => (
            <Pressable
              key={`${index}-${image.url ?? 'none'}`}
              accessibilityRole="button"
              accessibilityLabel={`Show image ${index + 1}: ${image.altText || product.name}`}
              accessibilityState={{ selected: index === imageIndex }}
              onPress={() => setImageIndex(index)}
              style={[styles.thumb, index === imageIndex && styles.thumbSelected]}
            >
              <ProductImageView
                url={image.url}
                altText={image.altText}
                fallbackLabel={product.name}
                width={56}
              />
            </Pressable>
          ))}
        </View>
      ) : null}

      <Text style={styles.body}>{product.description}</Text>

      {variant ? (
        <>
          <VariantSelector variants={product.variants} selectedId={variant.id} onSelect={setVariantId} />
          <PriceText price={variant.price} compareAtPrice={variant.compareAtPrice} />
          <StockBadge status={variant.stockStatus} />
        </>
      ) : null}
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  container: { padding: 16, gap: 12 },
  title: { fontSize: 24, fontWeight: '600' },
  category: { fontSize: 14, opacity: 0.7 },
  body: { fontSize: 16 },
  link: { minHeight: 44, paddingVertical: 12, fontSize: 16 },
  notFound: { gap: 8 },
  thumbs: { flexDirection: 'row', gap: 8, flexWrap: 'wrap' },
  thumb: { minHeight: 44, minWidth: 44, padding: 2, borderWidth: 1, borderColor: 'transparent', borderRadius: 8 },
  thumbSelected: { borderColor: '#111827' },
});
