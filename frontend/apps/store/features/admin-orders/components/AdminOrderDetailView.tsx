import type { ReactNode } from 'react';
import { Text, View } from 'react-native';

import { ErrorState } from '@/components/ErrorState';
import { LoadingState } from '@/components/LoadingState';
import { ScreenTitle } from '@/components/ScreenTitle';
import { formatOrderDate, refundStatusLabel, statusLabel } from '@/features/orders/status-labels';
import { ApiError, userMessage } from '@/lib/api-client';
import { formatMoney } from '@/lib/money';

import { useAdminOrderQuery } from '../hooks/hooks';
import type { AdminOrderDetail } from '../types';
import { RefundForm } from './RefundForm';

function Section({ title, children }: { title: string; children: ReactNode }) {
  return (
    <View className="gap-xs">
      <Text accessibilityRole="header" className="text-title font-semibold text-foreground">
        {title}
      </Text>
      {children}
    </View>
  );
}

function Content({ detail }: { detail: AdminOrderDetail }) {
  const { order } = detail;
  return (
    <View className="gap-lg">
      <View className="gap-xs">
        <Text className="text-body font-semibold text-foreground">{order.orderNumber}</Text>
        <Text className="text-small text-muted">{formatOrderDate(order.createdAt)}</Text>
        <Text className="text-body text-foreground">{statusLabel(order.status)}</Text>
        <Text className="text-small text-muted">{`Customer ${detail.userId}`}</Text>
      </View>

      <Section title="Items">
        {order.items.map((item) => (
          <View key={item.variantId} className="flex-row flex-wrap justify-between gap-sm">
            <Text className="text-body text-foreground">{`${item.name} × ${item.quantity}`}</Text>
            <Text className="text-body text-foreground">{formatMoney(item.lineTotal)}</Text>
          </View>
        ))}
        <Text className="text-body font-semibold text-foreground">{`Total ${formatMoney(order.grandTotal)}`}</Text>
      </Section>

      <Section title="Refunds">
        <Text className="text-body text-foreground">{`Refunded ${formatMoney(detail.refundedTotal)}`}</Text>
        <Text className="text-body text-foreground">{`Pending ${formatMoney(detail.pendingTotal)}`}</Text>
        <Text className="text-body text-foreground">{`Remaining ${formatMoney(detail.remaining)}`}</Text>
        {detail.refunds.map((refund) => (
          <View key={refund.refundId} className="flex-row flex-wrap justify-between gap-sm">
            <Text className="text-body text-foreground">{`${formatMoney(refund.amount)} · ${refundStatusLabel(refund.status)}`}</Text>
            <Text className="text-small text-muted">{formatOrderDate(refund.createdAt)}</Text>
          </View>
        ))}
      </Section>

      {detail.returnRequests.length > 0 ? (
        <Section title="Return requests">
          {detail.returnRequests.map((request) => (
            <Text key={request.id} className="text-body text-foreground">{`${request.status}: ${request.reason}`}</Text>
          ))}
        </Section>
      ) : null}

      <RefundForm detail={detail} />
    </View>
  );
}

export function AdminOrderDetailView({ id }: { id: string }) {
  const query = useAdminOrderQuery(id);

  return (
    <View className="w-full max-w-content flex-1 gap-sm self-center p-lg">
      <ScreenTitle title="Order" />
      {query.data ? (
        <Content detail={query.data} />
      ) : query.isError ? (
        query.error instanceof ApiError && query.error.status === 404 ? (
          <ErrorState message="We could not find that order." />
        ) : (
          <ErrorState message={userMessage(query.error)} onRetry={() => void query.refetch()} />
        )
      ) : (
        <LoadingState />
      )}
    </View>
  );
}
