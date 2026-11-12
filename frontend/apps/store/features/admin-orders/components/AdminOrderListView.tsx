import { Link, useLocalSearchParams, useRouter } from 'expo-router';
import { useState } from 'react';
import { Pressable, ScrollView, Text, View } from 'react-native';

import { ActionButton } from '@/components/ActionButton';
import { AdminPanel } from '@/components/AdminPanel';
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
import { parseOrderListParams, STATUSES } from '../order-list-params';
import type { AdminOrderFilters } from '../types';

type TextFilters = Omit<AdminOrderFilters, 'status'>;

/** The admin order list (11.4). Status and page live in the URL (arch 13.2 "Admin order list URL contract"); the text filters wait for Apply. */
export function AdminOrderListView() {
  const router = useRouter();
  const { status, page } =
    parseOrderListParams(
      useLocalSearchParams<{
        status?: string | string[];
        page?: string | string[];
      }>(),
    );
  const [draft, setDraft] = useState<TextFilters>({
    from: '',
    to: '',
    customer: '',
    orderNumber: '',
  });
  const [applied, setApplied] = useState<TextFilters>(draft);
  const [errors, setErrors] = useState<FilterErrors>({});
  const query = useAdminOrdersQuery({ ...applied, status }, page);

  function apply() {
    const found = validateFilters(draft);
    setErrors(found);
    if (found.from || found.to) {
      return;
    }
    setApplied(draft);
    router.setParams({
      status: status === 'all' ? undefined : status,
      page: undefined,
    });
  }

  function chooseStatus(next: OrderStatus | 'all') {
    router.setParams({
      status: next === 'all' ? undefined : next,
      page: undefined,
    });
  }

  function goTo(next: number) {
    router.setParams({
      status: status === 'all' ? undefined : status,
      page: next > 1 ? String(next) : undefined,
    });
  }

  function set(patch: Partial<TextFilters>) {
    setDraft({ ...draft, ...patch });
  }

  return (
    <ScrollView contentContainerClassName="w-full max-w-content gap-lg self-center p-lg">
      <ScreenTitle title="Orders" />
      <View className="flex-row flex-wrap gap-sm">
        {(['all', ...STATUSES] as const).map((s) => (
          <Pressable
            key={s}
            accessibilityRole="button"
            accessibilityState={{ selected: s === status }}
            onPress={() => chooseStatus(s)}
            className={`min-h-touch justify-center rounded-pill border px-md ${s === status ? 'border-primary bg-primary-subtle' : 'border-border bg-surface'}`}
          >
            <Text className={s === status ? 'text-body font-semibold text-primary-text' : 'text-body text-foreground'}>
              {s === 'all' ? 'All' : statusLabel(s)}
            </Text>
          </Pressable>
        ))}
      </View>
      <AdminPanel>
        <TextField
          label="From (yyyy-mm-dd)"
          value={draft.from}
          onChangeText={(from) => set({ from })}
          error={errors.from}
          autoCapitalize="none"
        />
        <TextField label="To (yyyy-mm-dd)" value={draft.to} onChangeText={(to) => set({ to })} error={errors.to} autoCapitalize="none" />
        <TextField
          label="Customer email"
          value={draft.customer}
          onChangeText={(customer) => set({ customer })}
          keyboardType="email-address"
          autoCapitalize="none"
        />
        <TextField
          label="Order number"
          value={draft.orderNumber}
          onChangeText={(orderNumber) => set({ orderNumber })}
          autoCapitalize="characters"
        />
        <ActionButton label="Apply filters" onPress={apply} />
      </AdminPanel>
      <AdminPanel>
        {query.data ? (
          query.data.items.length === 0 ? (
            <Text className="text-body text-muted">No orders match.</Text>
          ) : (
            <View>
              {query.data.items.map((o) => (
                <Link
                  key={o.id}
                  href={{
                    pathname: '/admin/orders/[id]',
                    params: { id: o.id },
                  }}
                  accessibilityRole="link"
                  asChild
                >
                  <Text className="min-h-touch border-b border-border py-md text-body text-foreground">
                    {`${o.orderNumber} · ${o.customerEmail ?? 'Unknown customer'} · ${formatOrderDate(o.createdAt)} · ${statusLabel(o.status)} · ${formatMoney(o.grandTotal)}`}
                  </Text>
                </Link>
              ))}
            </View>
          )
        ) : query.isError ? (
          <ErrorState message={userMessage(query.error)} onRetry={() => void query.refetch()} />
        ) : (
          <LoadingState />
        )}
      </AdminPanel>
      {query.data && query.data.items.length > 0 && query.data.totalPages > 1 ? (
        <View className="flex-row flex-wrap items-center gap-md">
          <ActionButton label="Previous page" onPress={() => goTo(page - 1)} disabled={page <= 1} />
          <Text className="text-body text-foreground">{`Page ${query.data.page} of ${query.data.totalPages}`}</Text>
          <ActionButton label="Next page" onPress={() => goTo(page + 1)} disabled={page >= query.data.totalPages} />
        </View>
      ) : null}
    </ScrollView>
  );
}
