import { useLocalSearchParams, useRouter } from 'expo-router';
import { useState } from 'react';
import { ScrollView, View } from 'react-native';

import { Panel } from '@/components/Panel';
import { EmptyState } from '@/components/EmptyState';
import { ErrorState } from '@/components/ErrorState';
import { LoadingState } from '@/components/LoadingState';
import { ScreenTitle } from '@/components/ScreenTitle';
import { Pagination } from '@/features/catalog/components/Pagination';
import { userMessage } from '@/lib/api-client';

import { useAdminInventoryQuery } from '../hooks/useAdminInventoryQuery';
import { InventoryRow } from './InventoryRow';

const PAGE_SIZE = 20;

export function AdminInventoryView() {
  const router = useRouter();
  const raw = useLocalSearchParams<{ page?: string }>();
  const page = raw.page && /^\d+$/.test(raw.page) && Number(raw.page) >= 1 ? Number(raw.page) : 1;
  const inventory = useAdminInventoryQuery(page, PAGE_SIZE);
  const [openVariantId, setOpenVariantId] = useState<string | null>(null);

  function goTo(next: number) {
    setOpenVariantId(null);
    router.setParams({ page: next > 1 ? String(next) : undefined });
  }

  const data = inventory.data;

  return (
    <ScrollView contentContainerClassName="w-full max-w-content gap-lg self-center p-lg">
      <ScreenTitle title="Admin · Inventory" />

      <Panel>
        {data ? (
          data.items.length === 0 ? (
            <EmptyState message="No variants yet." />
          ) : (
            <View>
              {data.items.map((item) => (
                <InventoryRow
                  key={item.variantId}
                  item={item}
                  open={openVariantId === item.variantId}
                  onOpen={() => setOpenVariantId(item.variantId)}
                  onClose={() => setOpenVariantId(null)}
                  onReload={() => void inventory.refetch()}
                />
              ))}
            </View>
          )
        ) : inventory.isError ? (
          <ErrorState message={userMessage(inventory.error)} onRetry={() => void inventory.refetch()} />
        ) : (
          <LoadingState />
        )}
      </Panel>

      {data ? (
        <Pagination
          page={data.page}
          totalPages={data.totalPages}
          onPrevious={() => goTo(data.page - 1)}
          onNext={() => goTo(data.page + 1)}
        />
      ) : null}
    </ScrollView>
  );
}
