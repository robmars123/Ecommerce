import { useState } from 'react';
import { ScrollView, Text, View } from 'react-native';

import { ActionButton } from '@/components/ActionButton';
import { ErrorState } from '@/components/ErrorState';
import { LoadingState } from '@/components/LoadingState';
import { ScreenTitle } from '@/components/ScreenTitle';
import { ProductDetailContent } from '@/features/catalog/components/ProductDetailContent';
import { ApiError, userMessage } from '@/lib/api-client';

import { toFormState, toUpdateRequest } from '../form-model';
import { useAdminProductPreviewQuery } from '../hooks/useAdminProductPreviewQuery';
import { useAdminProductQuery } from '../hooks/useAdminProductQuery';
import { usePublishProductMutation } from '../hooks/usePublishProductMutation';
import { useUpdateProductMutation } from '../hooks/useUpdateProductMutation';
import { ProductForm } from './ProductForm';

export const NOT_VISIBLE_COPY = 'Not visible to customers';

export function AdminProductDetailView({ id }: { id: string }) {
  const query = useAdminProductQuery(id);
  const update = useUpdateProductMutation(id);
  const publish = usePublishProductMutation(id);
  const [tab, setTab] = useState<'edit' | 'preview'>('edit');
  const preview = useAdminProductPreviewQuery(id, tab === 'preview');
  const product = query.data;

  if (!product) {
    return (
      <View className="gap-md p-lg">
        <ScreenTitle title="Admin · Product" />
        {query.isError ? (
          <ErrorState message={userMessage(query.error)} onRetry={() => void query.refetch()} />
        ) : (
          <LoadingState />
        )}
      </View>
    );
  }

  const publishError = publish.error instanceof ApiError ? publish.error : undefined;
  const publishMessage = publishError
    ? (Object.values(publishError.fieldErrors ?? {})[0]?.[0] ?? userMessage(publishError))
    : publish.error
      ? userMessage(publish.error)
      : undefined;

  return (
    <ScrollView contentContainerClassName="gap-md p-lg">
      <ScreenTitle title="Admin · Product" />
      <Text className="text-title font-semibold text-foreground">{product.name}</Text>
      {!product.isActive ? <Text className="rounded-control border border-border p-sm text-body font-semibold text-foreground">{NOT_VISIBLE_COPY}</Text> : null}

      <View className="flex-row flex-wrap gap-sm">
        <ActionButton label="Edit" onPress={() => setTab('edit')} disabled={tab === 'edit'} />
        <ActionButton label="Preview" onPress={() => setTab('preview')} disabled={tab === 'preview'} />
        <ActionButton
          label={product.isActive ? 'Unpublish' : 'Publish'}
          onPress={() => publish.mutate(!product.isActive)}
          pending={publish.isPending}
        />
      </View>
      {publishMessage ? (
        <Text accessibilityRole="alert" className="text-body text-danger">
          {publishMessage}
        </Text>
      ) : null}

      {tab === 'edit' ? (
        <ProductForm
          // A new version remounts the form with what the server has.
          key={product.version}
          mode="edit"
          initial={toFormState(product)}
          onSubmit={(state) => update.mutate(toUpdateRequest(state, product.version))}
          pending={update.isPending}
          error={update.error}
          onReload={() => {
            update.reset();
            void query.refetch();
          }}
        />
      ) : preview.data ? (
        <ProductDetailContent product={preview.data.product} hideAddToCart />
      ) : preview.isError ? (
        <ErrorState message={userMessage(preview.error)} onRetry={() => void preview.refetch()} />
      ) : (
        <LoadingState />
      )}
    </ScrollView>
  );
}
