import { Link } from 'expo-router';
import { useState } from 'react';
import { Text, View } from 'react-native';

import { EmptyState } from '@/components/EmptyState';
import { ErrorState } from '@/components/ErrorState';
import { LoadingState } from '@/components/LoadingState';
import { ScreenTitle } from '@/components/ScreenTitle';
import { Pagination } from '@/features/catalog/components/Pagination';
import { userMessage } from '@/lib/api-client';
import { formatMoney } from '@/lib/money';

import { useOrdersQuery } from '../hooks/useOrdersQuery';
import { formatOrderDate, statusLabel } from '../status-labels';
import type { OrderSummary } from '../types';
import { TrackingLink } from './TrackingLink';

function OrderRow({ order }: { order: OrderSummary }) {
  return (
    <View className="gap-xs rounded-card border border-border bg-surface p-md">
      <Link href={{ pathname: '/orders/[id]', params: { id: order.id } }} asChild>
        <Text accessibilityRole="link" className="min-h-touch py-sm text-body font-semibold text-foreground">
          {order.orderNumber}
        </Text>
      </Link>
      <Text className="text-small text-muted">{formatOrderDate(order.createdAt)}</Text>
      <Text className="text-body text-foreground">{statusLabel(order.status)}</Text>
      <Text className="text-body text-foreground">{formatMoney(order.grandTotal)}</Text>
      {/* The list carries no tracking number (arch 9), so only an https link is shown; the detail shows the number. */}
      {order.trackingUrl?.startsWith('https://') ? (
        <TrackingLink trackingNumber="" trackingUrl={order.trackingUrl} label="Track" />
      ) : null}
    </View>
  );
}

export function OrderListView() {
  const [page, setPage] = useState(1);
  const orders = useOrdersQuery(page);
  const data = orders.data;

  return (
    <View className="w-full max-w-content flex-1 gap-sm self-center p-lg">
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
