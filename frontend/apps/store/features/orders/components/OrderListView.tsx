import { Link } from 'expo-router';
import { useState } from 'react';
import { Pressable, Text, View } from 'react-native';

import { EmptyState } from '@/components/EmptyState';
import { ErrorState } from '@/components/ErrorState';
import { LoadingState } from '@/components/LoadingState';
import { ScreenTitle } from '@/components/ScreenTitle';
import { Pagination } from '@/features/catalog/components/Pagination';
import { userMessage } from '@/lib/api-client';
import { formatMoney } from '@/lib/money';

import { useOrdersQuery } from '../hooks/useOrdersQuery';
import { formatOrderDate, statusLabel, statusTone } from '../status-labels';
import type { OrderSummary } from '../types';
import { StatusPill } from './StatusPill';
import { TrackingLink } from './TrackingLink';

function OrderRow({ order }: { order: OrderSummary }) {
  return (
    <View className="overflow-hidden rounded-panel border border-border bg-surface web:shadow-card">
      <Link href={{ pathname: '/orders/[id]', params: { id: order.id } }} asChild>
        <Pressable accessibilityRole="link" className="min-h-touch flex-row flex-wrap items-center gap-x-md gap-y-xs px-card py-md">
          <Text className="font-mono text-ui font-semibold text-foreground">{order.orderNumber}</Text>
          <Text className="text-ui-sm text-muted">{formatOrderDate(order.createdAt)}</Text>
          <StatusPill tone={statusTone(order.status)} label={statusLabel(order.status)} />
          <Text className="ml-auto text-panel-title font-bold text-foreground">{formatMoney(order.grandTotal)}</Text>
        </Pressable>
      </Link>
      {/* The list carries no tracking number (arch 9), so only an https link is shown; the detail shows the number. */}
      {order.trackingUrl?.startsWith('https://') ? (
        <View className="px-card pb-sm">
          <TrackingLink trackingNumber="" trackingUrl={order.trackingUrl} label="Track" />
        </View>
      ) : null}
    </View>
  );
}

export function OrderListView() {
  const [page, setPage] = useState(1);
  const orders = useOrdersQuery(page);
  const data = orders.data;

  return (
    <View className="w-full max-w-content flex-1 gap-gutter self-center p-lg">
      <ScreenTitle title="Orders" />
      {data ? (
        data.items.length > 0 ? (
          <>
            {data.items.map((order) => (
              <OrderRow key={order.id} order={order} />
            ))}
            <Pagination
              page={data.page}
              totalPages={data.totalPages}
              onPrevious={() => setPage(data.page - 1)}
              onNext={() => setPage(data.page + 1)}
            />
          </>
        ) : data.totalCount > 0 ? (
          <EmptyState message="There are no orders on that page." action={{ label: 'Go to first page', onPress: () => setPage(1) }} />
        ) : (
          <EmptyState message="You have no orders yet." />
        )
      ) : orders.isError ? (
        <ErrorState message={userMessage(orders.error)} onRetry={() => void orders.refetch()} />
      ) : (
        <LoadingState />
      )}
    </View>
  );
}
