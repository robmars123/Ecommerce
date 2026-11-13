import { Link } from 'expo-router';
import { Text, View } from 'react-native';
import Svg, { Circle, Defs, Line, LinearGradient, Path, Stop, Text as SvgText } from 'react-native-svg';

import { ActionButton } from '@/components/ActionButton';
import { LoadingState } from '@/components/LoadingState';
import { Panel } from '@/components/Panel';
import { userMessage } from '@/lib/api-client';
import { formatMoney } from '@/lib/money';
import { useThemeColors } from '@/lib/theme';

import { useRevenueTrendQuery } from '../hooks/hooks';
import { bucketLabel, trendGeometry } from '../trend';
import type { DashboardPeriod } from '../types';

const WIDTH = 560;
const HEIGHT = 170;

/**
 * Revenue over time (Admin only: the caller gates it, D9). Every figure is what the server returned: the header total and order
 * count are the server's, and the line only places the server's bucket amounts on the page.
 */
export function RevenueTrendPanel({ period, reportLink }: { period: DashboardPeriod; reportLink?: boolean }) {
  const colors = useThemeColors();
  const query = useRevenueTrendQuery(period, true);
  // A body without buckets is unusable (a proxy page, a version mismatch): show the error and retry, never crash the dashboard.
  const trend = query.data && Array.isArray(query.data.buckets) ? query.data : undefined;
  const failed = query.isError || (query.data !== undefined && trend === undefined);
  const geometry = trend ? trendGeometry(trend, WIDTH, HEIGHT) : null;
  const first = trend?.buckets[0];
  const last = trend?.buckets[trend.buckets.length - 1];

  return (
    <Panel title="Revenue trend">
      {reportLink ? (
        <Link href={{ pathname: '/admin/analytics', params: period === 'today' ? {} : { period } }} accessibilityRole="link" asChild>
          <Text className="min-h-touch text-ui-sm font-semibold text-foreground underline">View report →</Text>
        </Link>
      ) : null}
      {trend && geometry && first && last ? (
        <View className="gap-md">
          {query.isError ? (
            <Text accessibilityLiveRegion="polite" className="text-meta text-danger">
              {`Refresh failed: ${userMessage(query.error)} Showing the last figures.`}
            </Text>
          ) : null}
          <View className="flex-row flex-wrap gap-x-[28px] gap-y-sm">
            <View>
              <Text className="text-label font-semibold uppercase text-subtle">Total</Text>
              <Text className="text-kpi font-bold text-foreground">{formatMoney(trend.total)}</Text>
            </View>
            <View>
              <Text className="text-label font-semibold uppercase text-subtle">Orders</Text>
              <Text className="text-kpi font-bold text-foreground">{String(trend.orderCount)}</Text>
            </View>
          </View>
          <Svg testID="revenue-trend-chart" width="100%" height={HEIGHT} viewBox={`0 0 ${WIDTH} ${HEIGHT}`}>
            <Defs>
              <LinearGradient id="trend-fill" x1="0" y1="0" x2="0" y2="1">
                <Stop offset="0" stopColor={colors.success} stopOpacity={0.18} />
                <Stop offset="1" stopColor={colors.success} stopOpacity={0} />
              </LinearGradient>
            </Defs>
            {geometry.gridYs.map((y) => (
              <Line key={y} x1={0} y1={y} x2={WIDTH} y2={y} stroke={colors.border} strokeWidth={1} strokeDasharray="3 5" />
            ))}
            <Path d={geometry.areaPath} fill="url(#trend-fill)" />
            <Path
              d={geometry.linePath}
              stroke={colors.success}
              strokeWidth={2.5}
              strokeLinecap="round"
              strokeLinejoin="round"
              fill="none"
            />
            <Circle
              cx={geometry.points[geometry.points.length - 1].x}
              cy={geometry.points[geometry.points.length - 1].y}
              r={4}
              fill={colors.success}
              stroke={colors.surface}
              strokeWidth={2}
            />
            <SvgText x={0} y={HEIGHT - 4} fontSize={11} fill={colors.subtle} textAnchor="start">
              {bucketLabel(first.start, trend.granularity)}
            </SvgText>
            <SvgText x={WIDTH} y={HEIGHT - 4} fontSize={11} fill={colors.subtle} textAnchor="end">
              {bucketLabel(last.start, trend.granularity)}
            </SvgText>
          </Svg>
        </View>
      ) : failed ? (
        <View className="items-start gap-md">
          <Text accessibilityLiveRegion="polite" className="text-body text-danger">
            {userMessage(query.error)}
          </Text>
          <ActionButton label="Retry trend" onPress={() => void query.refetch()} />
        </View>
      ) : (
        <LoadingState />
      )}
    </Panel>
  );
}
