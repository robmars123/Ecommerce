import { Link } from 'expo-router';
import type { ReactNode } from 'react';
import { ScrollView, Text, View } from 'react-native';

import { ErrorState } from '@/components/ErrorState';
import { LoadingState } from '@/components/LoadingState';
import { ScreenTitle } from '@/components/ScreenTitle';
import { formatOrderDate, refundStatusLabel, statusLabel } from '@/features/orders/status-labels';
import { ApiError, userMessage } from '@/lib/api-client';
import { formatMoney } from '@/lib/money';

import { useAdminCustomerQuery } from '../hooks/useAdminCustomerQuery';
import type { AdminCustomerDetail } from '../types';

function Section({ title, count, shown, empty, children }: { title: string; count: number; shown: number; empty: string; children: ReactNode }) {
  return (
    <View className="gap-xs">
      <Text accessibilityRole="header" className="text-title font-semibold text-foreground">
        {title}
      </Text>
      {shown === 0 ? <Text className="text-body text-muted">{empty}</Text> : children}
      {count > shown ? <Text className="text-small text-muted">{`Showing ${shown} of ${count}`}</Text> : null}
    </View>
  );
}

/** Total spent comes from the server only: null means it is withheld, so the line is not drawn (slice 12.2 D5). */
function Content({ customer }: { customer: AdminCustomerDetail }) {
  return (
    <View className="gap-lg">
      <View className="gap-xs">
        <Text className="text-body font-semibold text-foreground">{customer.displayName}</Text>
        <Text className="text-body text-foreground">{customer.email}</Text>
        <Text className="text-small text-muted">{`Customer since ${formatOrderDate(customer.createdAt)}`}</Text>
        <Text className="text-body text-foreground">{`Orders ${customer.orderCount}`}</Text>
        {customer.totalSpent !== null ? <Text className="text-body text-foreground">{`Total spent ${formatMoney(customer.totalSpent)}`}</Text> : null}
      </View>

      <Section title="Recent orders" count={customer.orderCount} shown={customer.recentOrders.length} empty="No orders yet.">
        {customer.recentOrders.map((order) => (
          <Link key={order.id} href={{ pathname: '/admin/orders/[id]', params: { id: order.id } }} accessibilityRole="link" asChild>
            <Text className="min-h-touch py-md text-body text-foreground">
              {`${order.orderNumber} · ${formatOrderDate(order.createdAt)} · ${statusLabel(order.status)} · ${formatMoney(order.grandTotal)}`}
            </Text>
          </Link>
        ))}
      </Section>

      <Section title="Recent returns" count={customer.returnCount} shown={customer.recentReturns.length} empty="No returns.">
        {customer.recentReturns.map((ret) => (
          <View key={ret.id} className="flex-row flex-wrap justify-between gap-sm">
            <Text className="text-body text-foreground">{`${ret.orderNumber} · ${ret.status} · ${ret.reason}`}</Text>
            <Text className="text-small text-muted">{formatOrderDate(ret.createdAt)}</Text>
          </View>
        ))}
      </Section>

      <Section title="Recent refunds" count={customer.refundCount} shown={customer.recentRefunds.length} empty="No refunds.">
        {customer.recentRefunds.map((refund) => (
          <View key={refund.id} className="flex-row flex-wrap justify-between gap-sm">
            <Text className="text-body text-foreground">
              {`${refund.orderNumber} · ${formatMoney(refund.amount)} · ${refundStatusLabel(refund.status)}${refund.automatic ? ' · Automatic' : ''}`}
            </Text>
            <Text className="text-small text-muted">{formatOrderDate(refund.createdAt)}</Text>
          </View>
        ))}
      </Section>
    </View>
  );
}

export function AdminCustomerDetailView({ id }: { id: string }) {
  const query = useAdminCustomerQuery(id);

  return (
    <ScrollView contentContainerClassName="w-full max-w-content gap-sm self-center p-lg">
      <ScreenTitle title="Admin · Customer" />
      {query.data ? (
        <Content customer={query.data} />
      ) : query.isError ? (
        query.error instanceof ApiError && query.error.status === 404 ? (
          <ErrorState message="We could not find that customer." />
        ) : (
          <ErrorState message={userMessage(query.error)} onRetry={() => void query.refetch()} />
        )
      ) : (
        <LoadingState />
      )}
    </ScrollView>
  );
}
