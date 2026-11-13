import { Link, Stack, useLocalSearchParams, useRouter } from 'expo-router';
import { useState, type ReactNode } from 'react';
import { Pressable, ScrollView, Text, useWindowDimensions, View } from 'react-native';

import { ActionButton } from '@/components/ActionButton';
import { Panel } from '@/components/Panel';
import { ErrorState } from '@/components/ErrorState';
import { LoadingState } from '@/components/LoadingState';
import { adminRouteDecision } from '@/features/auth/admin-access';
import { useSession } from '@/features/auth/hooks/useSession';
import type { UserRole } from '@/features/auth/types';
import { formatOrderDate } from '@/features/orders/status-labels';
import { userMessage } from '@/lib/api-client';
import { formatMoney } from '@/lib/money';
import { saveTextFile } from '@/lib/platform';

import { fetchDashboardExport } from '../api';
import { useDashboardQuery, useRecentOrdersQuery, useRevenueTrendQuery } from '../hooks/hooks';
import { asOfParts } from '../as-of';
import { greeting } from '../greeting';
import { kpiCards } from '../kpi-cards';
import { dashboardLayout, type DashboardLayout } from '../layout';
import { parseDashboardPeriod } from '../period';
import type { Dashboard, DashboardPeriod } from '../types';
import { OrdersByStatusPanel } from './OrdersByStatusPanel';
import { PeriodSwitch } from './PeriodSwitch';
import { RevenueTrendPanel } from './RevenueTrendPanel';
import { TopProductsPanel } from './TopProductsPanel';

function AsOfLine({ data }: { data: Dashboard }) {
  const parts = asOfParts(data);
  return (
    <View className="flex-row flex-wrap items-center gap-x-[14px] gap-y-sm">
      <Text className="text-meta text-subtle">{parts.time}</Text>
      <Text className="text-meta text-subtle">{parts.period}</Text>
      <Text className="text-meta text-subtle">{parts.currency}</Text>
      <Text className={`text-meta ${parts.lagging ? 'text-warning' : 'text-subtle'}`}>{parts.lag}</Text>
    </View>
  );
}

const changeTone = {
  success: { pill: 'border-success text-success' },
  danger: { pill: 'border-danger text-danger' },
  muted: { pill: 'border-border text-muted' },
} as const;

function KpiCards({
  kpis,
  period,
  outcomes,
  kpiBasis,
}: {
  kpis: NonNullable<Dashboard['kpis']>;
  period: DashboardPeriod;
  outcomes: { delivered: number; cancelled: number };
  kpiBasis: DashboardLayout['kpiBasis'];
}) {
  return (
    <View className="flex-row flex-wrap gap-gutter">
      {kpiCards(kpis, period, outcomes).map((card) => (
        <View
          key={card.label}
          accessibilityLabel={card.label}
          className={`grow ${kpiBasis} rounded-panel border border-border bg-surface p-card web:shadow-card`}
        >
          <Text className="mb-md text-label font-semibold uppercase text-subtle">{card.label}</Text>
          <Text className="text-kpi font-bold text-foreground">{card.value}</Text>
          <Text className="mt-sm text-meta text-muted">{card.secondary}</Text>
          {card.change ? (
            <View className="mt-sm flex-row flex-wrap items-center gap-sm">
              {card.change.pill ? (
                <Text
                  className={`rounded-pill border bg-background px-[6px] py-[3px] text-badge font-bold ${changeTone[card.change.tone].pill}`}
                >
                  {card.change.pill}
                </Text>
              ) : null}
              <Text className="text-meta text-muted">{card.change.text}</Text>
            </View>
          ) : null}
          {card.detail ? <Text className="mt-sm text-meta text-muted">{card.detail}</Text> : null}
        </View>
      ))}
    </View>
  );
}

function NeedsAttention({ attention }: { attention: Dashboard['needsAttention'] }) {
  const quiet = attention.pendingReturns === 0 && attention.deadLetters.length === 0 && attention.laggingProjectors.length === 0;
  return (
    <Panel title="Needs attention">
      {attention.pendingReturns > 0 ? (
        <Link href="/admin/returns" accessibilityRole="link" asChild>
          <Text className="min-h-touch py-md text-ui text-foreground">{`${attention.pendingReturns} return requests waiting for a decision`}</Text>
        </Link>
      ) : null}
      {attention.deadLetters.map((d) => (
        <Text
          key={d.consumer}
          accessibilityRole="alert"
          className="text-ui text-danger"
        >{`${d.consumer}: ${d.count} failed events need attention`}</Text>
      ))}
      {attention.laggingProjectors.map((p) => (
        <Text key={p.name} accessibilityRole="alert" className="text-ui text-danger">{`${p.name} is ${p.lagSeconds} seconds behind`}</Text>
      ))}
      {quiet ? <Text className="text-ui-sm text-muted">Nothing needs attention.</Text> : null}
    </Panel>
  );
}

function RecentOrders() {
  const [page, setPage] = useState(1);
  const query = useRecentOrdersQuery(page);
  const totalPages = query.data ? Math.max(1, Math.ceil(query.data.totalCount / query.data.pageSize)) : 1;

  return (
    <Panel title="Recent orders">
      {query.data ? (
        query.data.items.length === 0 ? (
          <Text className="text-ui-sm text-muted">No orders yet.</Text>
        ) : (
          <View>
            {query.data.items.map((o) => (
              <Link key={o.id} href={{ pathname: '/admin/orders/[id]', params: { id: o.id } }} accessibilityRole="link" asChild>
                <Text className="min-h-touch border-b border-border py-md text-ui text-foreground">
                  {`${o.orderNumber} · ${o.customerEmail} · ${formatOrderDate(o.createdAt)} · ${formatMoney(o.grandTotal)} · ${o.status}`}
                </Text>
              </Link>
            ))}
            {totalPages > 1 ? (
              <View className="flex-row flex-wrap items-center gap-md">
                <ActionButton label="Previous orders" onPress={() => setPage(page - 1)} disabled={page <= 1} />
                <Text className="text-ui text-foreground">{`Page ${page} of ${totalPages}`}</Text>
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
    </Panel>
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
    <Panel title="Quick actions">
      <View className="flex-row flex-wrap gap-md">
        {actions.map((a) => (
          <Link key={a.href} href={a.href} accessibilityRole="link" asChild>
            <Text className="min-h-touch rounded-pill border border-border bg-surface px-[14px] py-md text-ui-sm font-semibold text-foreground">
              {a.label}
            </Text>
          </Link>
        ))}
      </View>
    </Panel>
  );
}

/** The admin home (arch 8.8.5, app context 4.8). Every figure is what the server returned; "as of" says how fresh it is. */
export function DashboardView({ now = () => new Date(), tabs }: { now?: () => Date; tabs?: ReactNode } = {}) {
  const params = useLocalSearchParams<{ period?: string | string[] }>();
  const router = useRouter();
  const period = parseDashboardPeriod(params.period);
  const choosePeriod = (next: DashboardPeriod) => router.setParams({ period: next === 'today' ? undefined : next });
  const query = useDashboardQuery(period);
  const data = query.data;
  const user = useSession().user;
  const role = user?.role ?? null;
  const layout = dashboardLayout(useWindowDimensions().width);
  // Revenue over time is Admin only (D9): the trend, the chart, View report and Export all sit behind this one gate.
  const isAdmin = adminRouteDecision(role, '/admin/analytics') === null;
  const trendQuery = useRevenueTrendQuery(period, isAdmin);
  const [exporting, setExporting] = useState(false);
  const [exportError, setExportError] = useState<string | null>(null);

  async function exportCsv() {
    setExporting(true);
    setExportError(null);
    try {
      await saveTextFile(`dashboard-${period}.csv`, 'text/csv', await fetchDashboardExport(period));
    } catch (error) {
      setExportError(`Export failed: ${userMessage(error)}`);
    } finally {
      setExporting(false);
    }
  }

  function refresh() {
    void query.refetch();
    if (isAdmin) void trendQuery.refetch();
  }

  return (
    <ScrollView contentContainerClassName={`w-full gap-gutter ${layout.wide ? 'px-page pb-[36px]' : 'px-lg pb-[28px]'}`}>
      <Stack.Screen options={{ title: 'Admin dashboard' }} />
      <View className="flex-row flex-wrap items-start justify-between gap-lg pb-lg pt-[22px]">
        <View className="shrink">
          <Text
            accessibilityRole="header"
            className={`${layout.narrow ? 'text-page-title-sm' : 'text-page-title'} font-bold text-foreground`}
          >
            Admin dashboard
          </Text>
          <Text className="mt-[6px] max-w-[620px] text-ui text-muted">{greeting(now(), user?.displayName)}</Text>
        </View>
        <View className="flex-row flex-wrap items-center gap-sm">
          <PeriodSwitch period={period} onChange={choosePeriod} />
          {isAdmin ? <ActionButton label="Export CSV" onPress={() => void exportCsv()} pending={exporting} pill /> : null}
          <ActionButton label="Refresh" onPress={refresh} pending={query.isFetching} pill />
        </View>
      </View>
      {tabs}

      {data ? (
        <View className="gap-gutter">
          {query.isError ? (
            <Text accessibilityRole="alert" className="text-meta text-danger">
              {`Refresh failed: ${userMessage(query.error)} Showing the last figures.`}
            </Text>
          ) : null}
          <AsOfLine data={data} />
          {exportError ? (
            <Text accessibilityRole="alert" className="text-meta text-danger">
              {exportError}
            </Text>
          ) : null}
          {data.lowStock.length > 0 && adminRouteDecision(role, '/admin/inventory') === null ? (
            <Link href="/admin/inventory" asChild>
              <Pressable
                accessibilityRole="link"
                accessibilityLabel="Stock is running low. Review inventory"
                className="min-h-touch flex-row flex-wrap items-center justify-between gap-md rounded-card border border-warning-border bg-warning-subtle px-[14px] py-[13px]"
              >
                <Text className="text-ui font-semibold text-foreground">Stock is running low</Text>
                <Text className="text-ui-sm font-semibold text-foreground underline">Review inventory</Text>
              </Pressable>
            </Link>
          ) : null}
          {data.kpis ? (
            <KpiCards
              kpis={data.kpis}
              period={data.period}
              outcomes={{ delivered: data.deliveredCount, cancelled: data.cancelledCount }}
              kpiBasis={layout.kpiBasis}
            />
          ) : null}

          {isAdmin ? <RevenueTrendPanel period={period} reportLink /> : null}

          <OrdersByStatusPanel ordersByStatus={data.ordersByStatus} showPercent={isAdmin} />

          <View className={layout.panelsSideBySide ? 'flex-row flex-wrap gap-gutter' : 'gap-gutter'}>
            <View className={layout.panelsSideBySide ? 'min-w-0 grow-[115] basis-0' : ''}>
              <TopProductsPanel products={data.topProducts} />
            </View>
            <View className={layout.panelsSideBySide ? 'min-w-0 grow-[85] basis-0' : ''}>
              <Panel title="Low stock">
                {data.lowStock.length === 0 ? <Text className="text-ui-sm text-muted">Nothing is running low.</Text> : null}
                {data.lowStock.map((l) => (
                  <Link key={l.variantId} href="/admin/inventory" accessibilityRole="link" asChild>
                    <Text className="min-h-touch border-b border-border py-[11px] text-ui text-foreground">{`${l.productName} (${l.sku}) · ${l.available} left`}</Text>
                  </Link>
                ))}
              </Panel>
            </View>
          </View>

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
