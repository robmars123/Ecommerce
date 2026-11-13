import { Link } from 'expo-router';
import { Text, useWindowDimensions, View } from 'react-native';
import Svg, { Circle, G, Text as SvgText } from 'react-native-svg';

import { Panel } from '@/components/Panel';
import { useThemeColors } from '@/lib/theme';

import { statusBuckets, type StatusTone } from '../status-buckets';
import type { Dashboard } from '../types';

const RADIUS = 46;
const CIRCUMFERENCE = 2 * Math.PI * RADIUS;
const COLUMN_BELOW = 560;

/** Orders by status as a donut and a legend of four display groups (D11-D13). Counts come from the server; percents are display only. */
export function OrdersByStatusPanel({
  ordersByStatus,
  showPercent,
}: {
  ordersByStatus: Dashboard['ordersByStatus'];
  showPercent: boolean;
}) {
  const colors = useThemeColors();
  const column = useWindowDimensions().width < COLUMN_BELOW;
  const { total, buckets } = statusBuckets(ordersByStatus);
  const toneColor: Record<StatusTone, string> = {
    warning: colors.warning,
    muted: colors.muted,
    success: colors.success,
    danger: colors.danger,
  };

  if (total === 0) {
    return (
      <Panel title="Orders by status">
        <Text className="text-ui-sm text-muted">No orders yet.</Text>
      </Panel>
    );
  }

  let offset = 0;
  const arcs = buckets
    .filter((b) => b.count > 0)
    .map((b) => {
      const length = (b.count / total) * CIRCUMFERENCE;
      const arc = { key: b.key, color: toneColor[b.tone], length, offset };
      offset += length;
      return arc;
    });

  return (
    <Panel title="Orders by status">
      <View className={`${column ? 'items-start' : 'flex-row items-center'} gap-[18px]`}>
        <Svg testID="orders-donut" width={118} height={118} viewBox="0 0 120 120">
          <Circle cx={60} cy={60} r={RADIUS} fill="none" stroke={colors.border} strokeWidth={14} />
          <G rotation={-90} originX={60} originY={60}>
            {arcs.map((a) => (
              <Circle
                key={a.key}
                cx={60}
                cy={60}
                r={RADIUS}
                fill="none"
                stroke={a.color}
                strokeWidth={14}
                strokeDasharray={`${a.length} ${CIRCUMFERENCE - a.length}`}
                strokeDashoffset={-a.offset}
              />
            ))}
          </G>
          <SvgText x={60} y={58} fontSize={22} fontWeight="700" fill={colors.foreground} textAnchor="middle">
            {String(total)}
          </SvgText>
          <SvgText x={60} y={74} fontSize={10} fontWeight="600" fill={colors.muted} textAnchor="middle">
            orders
          </SvgText>
        </Svg>
        <View className={`${column ? 'w-full' : 'flex-1'} gap-sm`}>
          {buckets.map((b) => (
            <View key={b.key} className="border-b border-border py-sm web:border-dashed">
              <View className="flex-row items-center justify-between gap-md">
                <View className="shrink flex-row items-center gap-sm">
                  <View className="h-[8px] w-[8px] rounded-pill" style={{ backgroundColor: toneColor[b.tone] }} />
                  {b.href ? (
                    <Link href={b.href} accessibilityRole="link" asChild>
                      <Text className="min-h-touch py-md text-ui-sm text-foreground">{b.label}</Text>
                    </Link>
                  ) : (
                    <Text className="text-ui-sm text-foreground">{b.label}</Text>
                  )}
                </View>
                <Text className="text-ui-sm font-semibold text-foreground">
                  {showPercent ? `${b.count} (${b.percent}%)` : String(b.count)}
                </Text>
              </View>
              {!b.href
                ? b.statuses.map((s) => (
                    <Link
                      key={s.status}
                      href={{ pathname: '/admin/orders', params: { status: s.status } }}
                      accessibilityRole="link"
                      asChild
                    >
                      <Text className="min-h-touch py-sm pl-lg text-ui-sm text-muted">{`${s.status}: ${s.count}`}</Text>
                    </Link>
                  ))
                : null}
            </View>
          ))}
        </View>
      </View>
    </Panel>
  );
}
