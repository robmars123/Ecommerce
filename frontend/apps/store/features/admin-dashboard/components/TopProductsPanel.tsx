import { Text } from 'react-native';

import { Panel } from '@/components/Panel';

import type { Dashboard } from '../types';

export function TopProductsPanel({ products }: { products: Dashboard['topProducts'] }) {
  return (
    <Panel title="Top products">
      {products.length === 0 ? <Text className="text-ui-sm text-muted">No sales in this period.</Text> : null}
      {products.map((t) => (
        <Text key={t.productId} className="border-b border-border py-[11px] text-ui text-foreground">{`${t.name} · ${t.units} sold`}</Text>
      ))}
    </Panel>
  );
}
