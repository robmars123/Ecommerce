import type { ReactNode } from 'react';
import { Text, View } from 'react-native';

import { ErrorState } from '@/components/ErrorState';
import { LoadingState } from '@/components/LoadingState';
import { ScreenTitle } from '@/components/ScreenTitle';
import { ApiError, userMessage } from '@/lib/api-client';
import { formatMoney } from '@/lib/money';

import { useOrderQuery } from '../hooks/useOrderQuery';
import { formatOrderDate, paymentStatusLabel, statusLabel } from '../status-labels';
import type { OrderDetail } from '../types';
import { OrderTimeline } from './OrderTimeline';
import { TrackingLink } from './TrackingLink';

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

function Content({ order }: { order: OrderDetail }) {
  const address = order.shippingAddress;
  return (
    <View className="gap-lg">
      <View className="gap-xs">
        <Text className="text-body font-semibold text-foreground">{order.orderNumber}</Text>
        <Text className="text-small text-muted">{formatOrderDate(order.createdAt)}</Text>
        <Text className="text-body text-foreground">{statusLabel(order.status)}</Text>
        {order.paymentStatus ? (
          <Text className="text-small text-muted">{`Payment: ${paymentStatusLabel(order.paymentStatus)}`}</Text>
        ) : null}
      </View>

      <Section title="Items">
        {order.items.map((item) => (
          <View key={item.variantId} className="flex-row flex-wrap justify-between gap-sm">
            <Text className="text-body text-foreground">{`${item.name} × ${item.quantity}`}</Text>
            <Text className="text-body text-foreground">{formatMoney(item.lineTotal)}</Text>
          </View>
        ))}
      </Section>

      <Section title="Totals">
        <Text className="text-body text-foreground">{`Subtotal ${formatMoney(order.subtotal)}`}</Text>
        <Text className="text-body text-foreground">{`Shipping ${formatMoney(order.shippingTotal)}`}</Text>
        <Text className="text-body text-foreground">{`Tax ${formatMoney(order.taxTotal)}`}</Text>
        <Text className="text-body font-semibold text-foreground">{`Total ${formatMoney(order.grandTotal)}`}</Text>
      </Section>

      <Section title="Shipping">
        <Text className="text-body text-foreground">{order.shippingMethod.name}</Text>
        <Text className="text-body text-foreground">{address.fullName}</Text>
        <Text className="text-body text-foreground">{address.line1}</Text>
        {address.line2 ? <Text className="text-body text-foreground">{address.line2}</Text> : null}
        <Text className="text-body text-foreground">
          {[address.city, address.region, address.postalCode].filter(Boolean).join(', ')}
        </Text>
        <Text className="text-body text-foreground">{address.country}</Text>
      </Section>

      {order.shipments.length > 0 ? (
        <Section title="Tracking">
          {order.shipments.map((shipment) => (
            <View key={`${shipment.carrierName}-${shipment.trackingNumber}`} className="gap-xs">
              <Text className="text-body text-foreground">{`${shipment.carrierName} · ${shipment.trackingNumber}`}</Text>
              <TrackingLink trackingNumber={shipment.trackingNumber} trackingUrl={shipment.trackingUrl} />
            </View>
          ))}
        </Section>
      ) : null}

      <Section title="Timeline">
        <OrderTimeline entries={order.timeline} />
      </Section>
    </View>
  );
}

// 403 (another customer's order) and 404 read the same to the customer, and retrying cannot help.
function isMissing(error: unknown): boolean {
  return error instanceof ApiError && (error.status === 403 || error.status === 404);
}

export function OrderDetailView({ id }: { id: string }) {
  const query = useOrderQuery(id);

  return (
    <View className="w-full max-w-content flex-1 gap-sm self-center p-lg">
      <ScreenTitle title="Order" />
      {query.data ? (
        <Content order={query.data} />
      ) : query.isError ? (
        isMissing(query.error) ? (
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
