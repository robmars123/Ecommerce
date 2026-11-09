import { Link } from 'expo-router';
import { useState } from 'react';
import { Pressable, ScrollView, Text, View } from 'react-native';

import { ActionButton } from '@/components/ActionButton';
import { ErrorState } from '@/components/ErrorState';
import { LoadingState } from '@/components/LoadingState';
import { ScreenTitle } from '@/components/ScreenTitle';
import { formatOrderDate } from '@/features/orders/status-labels';
import { userMessage } from '@/lib/api-client';
import { formatMoney } from '@/lib/money';

import { useDashboardQuery, useRecentOrdersQuery } from '../hooks/hooks';
import type { Dashboard, DashboardPeriod } from '../types';

const PERIODS: { value: DashboardPeriod; label: string }[] = [
  { value: 'today', label: 'Today' },
  { value: '7d', label: '7 days' },
  { value: '30d', label: '30 days' },
];

function formatAsOf(iso: string): string {
  return new Date(iso).toLocaleString('en-US', {
    timeZone: 'UTC',
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
    hour12: false,
  });
}

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <View className="gap-sm rounded-control border border-border p-md">
      <Text accessibilityRole="header" className="text-body font-semibold text-foreground">
        {title}
      </Text>
      {children}
    </View>
  );
}

function Kpis({ kpis }: { kpis: NonNullable<Dashboard['kpis']> }) {
  return (
    <Section title="Sales">
      <Text className="text-body text-foreground">{`Revenue: ${formatMoney(kpis.revenue)}`}</Text>
      <Text className="text-body text-foreground">{`Orders: ${kpis.orderCount}`}</Text>
      <Text className="text-body text-foreground">{`Average order value: ${formatMoney(kpis.averageOrderValue)}`}</Text>
      <Text className="text-body text-foreground">{`Customers: ${kpis.customers.total} (${kpis.customers.new} new, ${kpis.customers.returning} returning)`}</Text>
    </Section>
  );
}

function NeedsAttention({ attention }: { attention: Dashboard['needsAttention'] }) {
  const quiet = attention.pendingReturns === 0 && attention.deadLetters.length === 0 && attention.laggingProjectors.length === 0;
  return (
    <Section title="Needs attention">
      {attention.pendingReturns > 0 ? (
        <Link href="/admin/returns" accessibilityRole="link" asChild>
          <Text className="min-h-touch py-md text-body text-foreground">{`${attention.pendingReturns} return requests waiting for a decision`}</Text>
        </Link>
      ) : null}
      {attention.deadLetters.map((d) => (
        <Text key={d.consumer} accessibilityRole="alert" className="text-body text-danger">{`${d.consumer}: ${d.count} failed events need attention`}</Text>
      ))}
      {attention.laggingProjectors.map((p) => (
        <Text key={p.name} accessibilityRole="alert" className="text-body text-danger">{`${p.name} is ${p.lagSeconds} seconds behind`}</Text>
      ))}
      {quiet ? <Text className="text-body text-muted">Nothing needs attention.</Text> : null}
    </Section>
  );
}

function RecentOrders() {
  const [page, setPage] = useState(1);
  const query = useRecentOrdersQuery(page);
  const totalPages = query.data ? Math.max(1, Math.ceil(query.data.totalCount / query.data.pageSize)) : 1;

  return (
    <Section title="Recent orders">
      {query.data ? (
        query.data.items.length === 0 ? (
          <Text className="text-body text-muted">No orders yet.</Text>
        ) : (
          <View className="gap-xs">
            {query.data.items.map((o) => (
              <Link key={o.id} href={{ pathname: '/admin/orders/[id]', params: { id: o.id } }} accessibilityRole="link" asChild>
                <Text className="min-h-touch py-md text-body text-foreground">
                  {`${o.orderNumber} · ${o.customerEmail} · ${formatOrderDate(o.createdAt)} · ${formatMoney(o.grandTotal)} · ${o.status}`}
                </Text>
              </Link>
            ))}
            {totalPages > 1 ? (
              <View className="flex-row items-center gap-md">
                <ActionButton label="Previous orders" onPress={() => setPage(page - 1)} disabled={page <= 1} />
                <Text className="text-body text-foreground">{`Page ${page} of ${totalPages}`}</Text>
                <ActionButton label="Next orders" onPress={() => setPage(page + 1)} disabled={page >= totalPages} />
              </View>
            ) : null}
          </View>
        )
      ) : query.isError ? (
        <ErrorState message={userMessage(query.error)} onRetry={() => void query.refetch()} />
      ) : (
        <LoadingState />
      )}
    </Section>
  );
}

const QUICK_ACTIONS: { href: '/admin/products/new' | '/admin/inventory' | '/admin/orders' | '/admin/returns'; label: string }[] = [
  { href: '/admin/products/new', label: 'Add product' },
  { href: '/admin/inventory', label: 'Inventory' },
  { href: '/admin/orders', label: 'Orders' },
  { href: '/admin/returns', label: 'Returns' },
];

/** The admin home (arch 8.8.5, app context 4.8). Every figure is what the server returned; "as of" says how fresh it is. */
export function DashboardView() {
  const [period, setPeriod] = useState<DashboardPeriod>('today');
  const query = useDashboardQuery(period);
  const data = query.data;

  return (
    <ScrollView contentContainerClassName="w-full max-w-content gap-md self-center p-lg">
      <ScreenTitle title="Admin dashboard" />
      <View className="flex-row flex-wrap gap-sm">
        {PERIODS.map((p) => (
          <Pressable
            key={p.value}
            accessibilityRole="button"
            accessibilityState={{ selected: p.value === period }}
            onPress={() => setPeriod(p.value)}
            className={`min-h-touch justify-center rounded-control border px-md ${p.value === period ? 'border-primary' : 'border-border'}`}
          >
            <Text className="text-body text-foreground">{p.label}</Text>
          </Pressable>
        ))}
        <ActionButton label="Refresh" onPress={() => void query.refetch()} pending={query.isFetching} />
      </View>

      {data ? (
        <View className="gap-md">
          <Text className="text-small text-muted">{`Figures as of ${formatAsOf(data.asOf)} UTC`}</Text>
          {data.kpis ? <Kpis kpis={data.kpis} /> : null}

          <Section title="Orders by status">
            {data.ordersByStatus.length === 0 ? <Text className="text-body text-muted">No orders yet.</Text> : null}
            {data.ordersByStatus.map((s) => (
              <Link key={s.status} href={{ pathname: '/admin/orders', params: { status: s.status } }} accessibilityRole="link" asChild>
                <Text className="min-h-touch py-md text-body text-foreground">{`${s.status}: ${s.count}`}</Text>
              </Link>
            ))}
          </Section>

          <Section title="Top products">
            {data.topProducts.length === 0 ? <Text className="text-body text-muted">No sales in this period.</Text> : null}
            {data.topProducts.map((t) => (
              <Text key={t.productId} className="text-body text-foreground">{`${t.name} · ${t.units} sold`}</Text>
            ))}
          </Section>

          <Section title="Low stock">
            {data.lowStock.length === 0 ? <Text className="text-body text-muted">Nothing is running low.</Text> : null}
            {data.lowStock.map((l) => (
              <Link key={l.variantId} href="/admin/inventory" accessibilityRole="link" asChild>
                <Text className="min-h-touch py-md text-body text-foreground">{`${l.productName} (${l.sku}) · ${l.available} left`}</Text>
              </Link>
            ))}
          </Section>

          <NeedsAttention attention={data.needsAttention} />
        </View>
      ) : query.isError ? (
        <ErrorState message={userMessage(query.error)} onRetry={() => void query.refetch()} />
      ) : (
        <LoadingState />
      )}

      <RecentOrders />

      <Section title="Quick actions">
        <View className="flex-row flex-wrap gap-md">
          {QUICK_ACTIONS.map((a) => (
            <Link key={a.href} href={a.href} accessibilityRole="link" asChild>
              <Text className="min-h-touch py-md text-body text-foreground">{a.label}</Text>
            </Link>
          ))}
        </View>
      </Section>
    </ScrollView>
  );
}
