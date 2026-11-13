import { ScrollView, Text, View } from 'react-native';

import { Panel } from '@/components/Panel';
import { ErrorState } from '@/components/ErrorState';
import { LoadingState } from '@/components/LoadingState';
import { ScreenTitle } from '@/components/ScreenTitle';
import { formatOrderDate, refundStatusLabel, statusLabel } from '@/features/orders/status-labels';
import { ApiError, userMessage } from '@/lib/api-client';
import { formatMoney } from '@/lib/money';

import { useAdminOrderQuery } from '../hooks/hooks';
import type { AdminOrderDetail } from '../types';
import { OrderActions } from './OrderActions';
import { RefundForm } from './RefundForm';

function Content({ detail }: { detail: AdminOrderDetail }) {
  const { order } = detail;
  return (
    <View className="gap-lg">
      <Panel>
        <View className="gap-xs">
          <Text className="text-body font-semibold text-foreground">{order.orderNumber}</Text>
          <Text className="text-small text-muted">{formatOrderDate(order.createdAt)}</Text>
          <Text className="text-body text-foreground">{statusLabel(order.status)}</Text>
          <Text className="text-small text-muted">{`Customer ${detail.userId}`}</Text>
        </View>
      </Panel>

      <Panel title="Items">
        {order.items.map((item) => (
          <View key={item.variantId} className="flex-row flex-wrap justify-between gap-sm">
            <Text className="text-body text-foreground">{`${item.name} × ${item.quantity}`}</Text>
            <Text className="text-body text-foreground">{formatMoney(item.lineTotal)}</Text>
          </View>
        ))}
        <Text className="text-body font-semibold text-foreground">{`Total ${formatMoney(order.grandTotal)}`}</Text>
      </Panel>

      <Panel title="Refunds">
        <Text className="text-body text-foreground">{`Refunded ${formatMoney(detail.refundedTotal)}`}</Text>
        <Text className="text-body text-foreground">{`Pending ${formatMoney(detail.pendingTotal)}`}</Text>
        <Text className="text-body text-foreground">{`Remaining ${formatMoney(detail.remaining)}`}</Text>
        {detail.refunds.map((refund) => (
          <View key={refund.refundId} className="flex-row flex-wrap justify-between gap-sm">
            <Text className="text-body text-foreground">{`${formatMoney(refund.amount)} · ${refundStatusLabel(refund.status)}`}</Text>
            <Text className="text-small text-muted">{formatOrderDate(refund.createdAt)}</Text>
          </View>
        ))}
      </Panel>

      {detail.returnRequests.length > 0 ? (
        <Panel title="Return requests">
          {detail.returnRequests.map((request) => (
            <Text key={request.id} className="text-body text-foreground">{`${request.status}: ${request.reason}`}</Text>
          ))}
        </Panel>
      ) : null}

      <OrderActions detail={detail} />

      <RefundForm detail={detail} />
    </View>
  );
}

export function AdminOrderDetailView({ id }: { id: string }) {
  const query = useAdminOrderQuery(id);

  return (
    <ScrollView contentContainerClassName="w-full max-w-content gap-lg self-center p-lg">
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
    </ScrollView>
  );
}
