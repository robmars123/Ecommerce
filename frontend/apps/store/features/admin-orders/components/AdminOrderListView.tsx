import { Link } from 'expo-router';
import { useState } from 'react';
import { Pressable, Text, View } from 'react-native';

import { ActionButton } from '@/components/ActionButton';
import { ErrorState } from '@/components/ErrorState';
import { LoadingState } from '@/components/LoadingState';
import { ScreenTitle } from '@/components/ScreenTitle';
import { TextField } from '@/components/TextField';
import { formatOrderDate, statusLabel } from '@/features/orders/status-labels';
import type { OrderStatus } from '@/features/orders/types';
import { userMessage } from '@/lib/api-client';
import { formatMoney } from '@/lib/money';

import { useAdminOrdersQuery } from '../hooks/hooks';
import { validateFilters, type FilterErrors } from '../order-actions';
import type { AdminOrderFilters } from '../types';

const STATUSES: OrderStatus[] = ['PendingPayment', 'Paid', 'Processing', 'Shipped', 'Delivered', 'Cancelled', 'Refunded', 'Returned'];

function startingStatus(initial?: string): OrderStatus | 'all' {
  return STATUSES.find((s) => s === initial) ?? 'all';
}

/** The admin order list (11.4). Apply sends the filters and goes back to page 1; the dashboard links here with ?status=. */
export function AdminOrderListView({ initialStatus }: { initialStatus?: string }) {
  const [draft, setDraft] = useState<AdminOrderFilters>({ status: startingStatus(initialStatus), from: '', to: '', customer: '', orderNumber: '' });
  const [applied, setApplied] = useState<AdminOrderFilters>(draft);
  const [page, setPage] = useState(1);
  const [errors, setErrors] = useState<FilterErrors>({});
  const query = useAdminOrdersQuery(applied, page);

  function apply() {
    const found = validateFilters(draft);
    setErrors(found);
    if (found.from || found.to) {
      return;
    }
    setApplied(draft);
    setPage(1);
  }

  function set(patch: Partial<AdminOrderFilters>) {
    setDraft({ ...draft, ...patch });
  }

  return (
    <View className="w-full max-w-content flex-1 gap-md self-center p-lg">
      <ScreenTitle title="Orders" />
      <View className="flex-row flex-wrap gap-sm">
        {(['all', ...STATUSES] as const).map((status) => (
          <Pressable
            key={status}
            accessibilityRole="button"
            accessibilityState={{ selected: status === draft.status }}
            onPress={() => set({ status })}
            className={`min-h-touch justify-center rounded-control border px-md ${status === draft.status ? 'border-primary' : 'border-border'}`}
          >
            <Text className="text-body text-foreground">{status === 'all' ? 'All' : statusLabel(status)}</Text>
          </Pressable>
        ))}
      </View>
      <TextField label="From (yyyy-mm-dd)" value={draft.from} onChangeText={(from) => set({ from })} error={errors.from} autoCapitalize="none" />
      <TextField label="To (yyyy-mm-dd)" value={draft.to} onChangeText={(to) => set({ to })} error={errors.to} autoCapitalize="none" />
      <TextField label="Customer email" value={draft.customer} onChangeText={(customer) => set({ customer })} keyboardType="email-address" autoCapitalize="none" />
      <TextField label="Order number" value={draft.orderNumber} onChangeText={(orderNumber) => set({ orderNumber })} autoCapitalize="characters" />
      <ActionButton label="Apply filters" onPress={apply} />
      {query.data ? (
        query.data.items.length === 0 ? (
          <Text className="text-body text-muted">No orders match.</Text>
        ) : (
          <View className="gap-xs">
            {query.data.items.map((o) => (
              <Link key={o.id} href={{ pathname: '/admin/orders/[id]', params: { id: o.id } }} accessibilityRole="link" asChild>
                <Text className="min-h-touch py-md text-body text-foreground">
                  {`${o.orderNumber} · ${o.customerEmail ?? 'Unknown customer'} · ${formatOrderDate(o.createdAt)} · ${statusLabel(o.status)} · ${formatMoney(o.grandTotal)}`}
                </Text>
              </Link>
            ))}
            {query.data.totalPages > 1 ? (
              <View className="flex-row items-center gap-md">
                <ActionButton label="Previous page" onPress={() => setPage(page - 1)} disabled={page <= 1} />
                <Text className="text-body text-foreground">{`Page ${query.data.page} of ${query.data.totalPages}`}</Text>
                <ActionButton label="Next page" onPress={() => setPage(page + 1)} disabled={page >= query.data.totalPages} />
              </View>
            ) : null}
          </View>
        )
      ) : query.isError ? (
        <ErrorState message={userMessage(query.error)} onRetry={() => void query.refetch()} />
      ) : (
        <LoadingState />
      )}
    </View>
  );
}
