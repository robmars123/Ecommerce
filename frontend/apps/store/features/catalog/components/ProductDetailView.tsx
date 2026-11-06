import { Link, Stack } from 'expo-router';
import { StyleSheet, Text, View } from 'react-native';

import { ErrorState } from '@/components/ErrorState';
import { LoadingState } from '@/components/LoadingState';
import { ApiError, userMessage } from '@/lib/api-client';

import { useProductQuery } from '../hooks/useProductQuery';
import { ProductDetailContent } from './ProductDetailContent';

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

const styles = StyleSheet.create({
  container: { padding: 16, gap: 12 },
  title: { fontSize: 24, fontWeight: '600' },
  body: { fontSize: 16 },
  link: { minHeight: 44, paddingVertical: 12, fontSize: 16 },
  notFound: { gap: 8 },
});
