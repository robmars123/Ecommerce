import { Link, Stack } from 'expo-router';
import { Text, View } from 'react-native';

import { ErrorState } from '@/components/ErrorState';
import { LoadingState } from '@/components/LoadingState';
import { ApiError, userMessage } from '@/lib/api-client';

import { useProductQuery } from '../hooks/useProductQuery';
import { ProductDetailContent } from './ProductDetailContent';

const LINK = 'min-h-touch py-md text-body text-foreground';

function BackToShop() {
  return (
    <View className="w-full max-w-content self-center px-lg pt-md">
      <Link href="/products" asChild>
        <Text className={LINK}>← Back to shop</Text>
      </Link>
    </View>
  );
}

export function ProductDetailView({ slug }: { slug: string }) {
  const query = useProductQuery(slug);

  if (query.data) {
    return (
      <View className="flex-1">
        <BackToShop />
        <ProductDetailContent product={query.data} />
      </View>
    );
  }

  return (
    <View className="w-full max-w-content gap-md self-center p-lg">
      <Stack.Screen options={{ title: 'Product' }} />
      <BackToShop />
      <Text accessibilityRole="header" className="text-heading font-semibold text-foreground">
        Product
      </Text>
      {query.isError ? (
        query.error instanceof ApiError && query.error.kind === 'not-found' ? (
          <View className="gap-sm">
            <Text className="text-body text-foreground">Product not found</Text>
            <Link href="/products" asChild>
              <Text className={LINK}>Browse products</Text>
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
