import { Link, useLocalSearchParams, useRouter } from 'expo-router';
import { useState } from 'react';
import { Pressable, ScrollView, Text, View } from 'react-native';

import { ActionButton } from '@/components/ActionButton';
import { AdminPanel } from '@/components/AdminPanel';
import { ErrorState } from '@/components/ErrorState';
import { LoadingState } from '@/components/LoadingState';
import { ScreenTitle } from '@/components/ScreenTitle';
import { adminRouteDecision } from '@/features/auth/admin-access';
import { useSession } from '@/features/auth/hooks/useSession';
import type { UserRole } from '@/features/auth/types';
import { formatOrderDate } from '@/features/orders/status-labels';
import { userMessage } from '@/lib/api-client';
import { formatMoney } from '@/lib/money';

import { useDashboardQuery, useRecentOrdersQuery } from '../hooks/hooks';
import { asOfParts } from '../as-of';
import { greeting } from '../greeting';
import { kpiCards } from '../kpi-cards';
import { parseDashboardPeriod, PERIODS } from '../period';
import type { Dashboard, DashboardPeriod } from '../types';

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

function AsOfLine({ data }: { data: Dashboard }) {
  const parts = asOfParts(data);
  return (
    <View className="flex-row flex-wrap gap-sm">
      <Text className="text-small text-muted">{parts.time}</Text>
      <Text className="text-small text-muted">{parts.period}</Text>
      <Text className="text-small text-muted">{parts.currency}</Text>
      <Text className={`text-small ${parts.lagging ? 'text-warning' : 'text-muted'}`}>{parts.lag}</Text>
    </View>
  );
}

function KpiCards({ kpis, period }: { kpis: NonNullable<Dashboard['kpis']>; period: DashboardPeriod }) {
  return (
    <View className="flex-row flex-wrap gap-md">
      {kpiCards(kpis, period).map((card) => (
        <View
          key={card.label}
          accessibilityLabel={card.label}
          className="grow basis-40 gap-xs rounded-card border border-border bg-surface p-lg"
        >
          <Text className="text-small text-muted">{card.label}</Text>
          <Text className="text-heading font-semibold text-foreground">{card.value}</Text>
          <Text className="text-small text-muted">{card.secondary}</Text>
        </View>
      ))}
    </View>
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
        <Text
          key={d.consumer}
          accessibilityRole="alert"
          className="text-body text-danger"
        >{`${d.consumer}: ${d.count} failed events need attention`}</Text>
      ))}
      {attention.laggingProjectors.map((p) => (
        <Text
          key={p.name}
          accessibilityRole="alert"
          className="text-body text-danger"
        >{`${p.name} is ${p.lagSeconds} seconds behind`}</Text>
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
    <AdminPanel title="Recent orders">
      {query.data ? (
        query.data.items.length === 0 ? (
          <Text className="text-body text-muted">No orders yet.</Text>
        ) : (
          <View>
            {query.data.items.map((o) => (
              <Link key={o.id} href={{ pathname: '/admin/orders/[id]', params: { id: o.id } }} accessibilityRole="link" asChild>
                <Text className="min-h-touch border-b border-border py-md text-body text-foreground">
                  {`${o.orderNumber} · ${o.customerEmail} · ${formatOrderDate(o.createdAt)} · ${formatMoney(o.grandTotal)} · ${o.status}`}
                </Text>
              </Link>
            ))}
            {totalPages > 1 ? (
              <View className="flex-row flex-wrap items-center gap-md">
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
    </AdminPanel>
  );
}

const QUICK_ACTIONS: {
  href: '/admin/products/new' | '/admin/categories' | '/admin/inventory' | '/admin/orders' | '/admin/returns';
  label: string;
}[] = [
  { href: '/admin/products/new', label: 'Add product' },
  { href: '/admin/categories', label: 'Categories' },
  { href: '/admin/inventory', label: 'Inventory' },
  { href: '/admin/orders', label: 'Orders' },
  { href: '/admin/returns', label: 'Returns' },
];

function QuickActions({ role }: { role: UserRole | null }) {
  const actions = QUICK_ACTIONS.filter((a) => adminRouteDecision(role, a.href) === null);
  if (actions.length === 0) return null;
  return (
    <AdminPanel title="Quick actions">
      <View className="flex-row flex-wrap gap-md">
        {actions.map((a) => (
          <Link key={a.href} href={a.href} accessibilityRole="link" asChild>
            <Text className="min-h-touch rounded-control border border-border px-md py-md text-body text-foreground">{a.label}</Text>
          </Link>
        ))}
      </View>
    </AdminPanel>
  );
}

/** The admin home (arch 8.8.5, app context 4.8). Every figure is what the server returned; "as of" says how fresh it is. */
export function DashboardView({ now = () => new Date() }: { now?: () => Date } = {}) {
  const params = useLocalSearchParams<{ period?: string | string[] }>();
  const router = useRouter();
  const period = parseDashboardPeriod(params.period);
  const choosePeriod = (next: DashboardPeriod) => router.setParams({ period: next === 'today' ? undefined : next });
  const query = useDashboardQuery(period);
  const data = query.data;
  const user = useSession().user;
  const role = user?.role ?? null;

  return (
    <ScrollView contentContainerClassName="w-full max-w-content gap-md self-center p-lg">
      <ScreenTitle title="Admin dashboard" />
      <Text className="text-body text-muted">{greeting(now(), user?.displayName)}</Text>
      <View className="flex-row flex-wrap gap-sm">
        {PERIODS.map((p) => (
          <Pressable
            key={p.value}
            accessibilityRole="button"
            accessibilityState={{ selected: p.value === period }}
            onPress={() => choosePeriod(p.value)}
            className={`min-h-touch justify-center rounded-control border px-md ${p.value === period ? 'border-primary' : 'border-border'}`}
          >
            <Text className="text-body text-foreground">{p.label}</Text>
          </Pressable>
        ))}
        <ActionButton label="Refresh" onPress={() => void query.refetch()} pending={query.isFetching} />
      </View>

      {data ? (
        <View className="gap-md">
          {query.isError ? (
            <Text accessibilityRole="alert" className="text-small text-danger">
              {`Refresh failed: ${userMessage(query.error)} Showing the last figures.`}
            </Text>
          ) : null}
          <AsOfLine data={data} />
          {data.lowStock.length > 0 && adminRouteDecision(role, '/admin/inventory') === null ? (
            <Link href="/admin/inventory" asChild>
              <Pressable
                accessibilityRole="link"
                accessibilityLabel="Stock is running low. Review inventory"
                className="min-h-touch flex-row flex-wrap items-center justify-between gap-sm rounded-card border border-warning bg-surface p-md"
              >
                <Text className="text-body font-semibold text-warning">Stock is running low</Text>
                <Text className="text-small text-primary">Review inventory</Text>
              </Pressable>
            </Link>
          ) : null}
          {data.kpis ? <KpiCards kpis={data.kpis} period={data.period} /> : null}

          <Section title="Orders by status">
            {data.ordersByStatus.length === 0 ? <Text className="text-body text-muted">No orders yet.</Text> : null}
            {data.ordersByStatus.map((s) => (
              <Link key={s.status} href={{ pathname: '/admin/orders', params: { status: s.status } }} accessibilityRole="link" asChild>
                <Text className="min-h-touch py-md text-body text-foreground">{`${s.status}: ${s.count}`}</Text>
              </Link>
            ))}
          </Section>

          <AdminPanel title="Top products">
            {data.topProducts.length === 0 ? <Text className="text-body text-muted">No sales in this period.</Text> : null}
            {data.topProducts.map((t) => (
              <Text
                key={t.productId}
                className="border-b border-border py-md text-body text-foreground"
              >{`${t.name} · ${t.units} sold`}</Text>
            ))}
          </AdminPanel>

          <AdminPanel title="Low stock">
            {data.lowStock.length === 0 ? <Text className="text-body text-muted">Nothing is running low.</Text> : null}
            {data.lowStock.map((l) => (
              <Link key={l.variantId} href="/admin/inventory" accessibilityRole="link" asChild>
                <Text className="min-h-touch border-b border-border py-md text-body text-foreground">{`${l.productName} (${l.sku}) · ${l.available} left`}</Text>
              </Link>
            ))}
          </AdminPanel>

          <NeedsAttention attention={data.needsAttention} />
        </View>
      ) : query.isError ? (
        <ErrorState message={userMessage(query.error)} onRetry={() => void query.refetch()} />
      ) : (
        <LoadingState />
      )}

      <RecentOrders />

      <QuickActions role={role} />
    </ScrollView>
  );
}
