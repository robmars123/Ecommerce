import { setStringAsync } from 'expo-clipboard';
import { Link } from 'expo-router';
import { useEffect, useRef, useState } from 'react';
import { Pressable, Text, useWindowDimensions, View } from 'react-native';

import { ErrorState } from '@/components/ErrorState';
import { LoadingState } from '@/components/LoadingState';
import { Panel } from '@/components/Panel';
import { ScreenTitle } from '@/components/ScreenTitle';
import { mergeResultText } from '@/features/cart/cart-messages';
import { ProductImageView } from '@/features/catalog/components/ProductImageView';
import { ApiError, userMessage } from '@/lib/api-client';
import { touch } from '@/lib/design-tokens';
import { formatMoney } from '@/lib/money';

import { useReorder } from '../hooks/useReorder';
import { useOrderLiveStatus } from '../hooks/useOrderLiveStatus';
import { useOrderQuery } from '../hooks/useOrderQuery';
import { orderDetailLayout } from '../layout';
import {
  formatOrderDate,
  paymentMethodLabel,
  paymentStatusLabel,
  paymentTone,
  refundStatusLabel,
  statusLabel,
  statusTone,
  totalsNote,
} from '../status-labels';
import type { OrderAlert, OrderDetail, OrderDetailItem } from '../types';
import { OrderTimeline } from './OrderTimeline';
import { ReturnRequestForm } from './ReturnRequestForm';
import { StatusPill } from './StatusPill';
import { TrackingLink } from './TrackingLink';

function TotalRow({ label, amount, emphasis = false }: { label: string; amount: string; emphasis?: boolean }) {
  return (
    <View
      accessible
      accessibilityLabel={`${label} ${amount}`}
      className={`flex-row justify-between gap-md ${emphasis ? 'mt-xs border-t border-border pt-md' : ''}`}
    >
      <Text className={emphasis ? 'shrink text-title font-bold text-foreground' : 'shrink text-ui text-foreground'}>{label}</Text>
      <Text
        className={emphasis ? 'text-title font-bold tabular-nums text-foreground' : 'text-ui font-semibold tabular-nums text-foreground'}
      >
        {amount}
      </Text>
    </View>
  );
}

function Breadcrumb({ orderNumber }: { orderNumber: string }) {
  return (
    <View className="flex-row flex-wrap items-center gap-sm">
      <Link href="/orders" asChild>
        <Pressable accessibilityRole="link" className="min-h-touch justify-center">
          <Text className="text-ui font-semibold text-muted">Orders</Text>
        </Pressable>
      </Link>
      <Text className="text-ui text-subtle">›</Text>
      <Text className="font-mono text-ui font-semibold text-foreground">{orderNumber}</Text>
    </View>
  );
}

type CopyState = 'idle' | 'copied' | 'failed';

const COPY_LABELS: Record<CopyState, { text: string; a11y: string }> = {
  idle: { text: 'Copy', a11y: 'Copy order number' },
  copied: { text: 'Copied', a11y: 'Order number copied' },
  failed: { text: 'Copy failed', a11y: "Couldn't copy order number" },
};

function CopyOrderNumber({ orderNumber }: { orderNumber: string }) {
  const [state, setState] = useState<CopyState>('idle');
  const timer = useRef<ReturnType<typeof setTimeout> | null>(null);
  useEffect(
    () => () => {
      if (timer.current) clearTimeout(timer.current);
    },
    [],
  );

  const copy = async () => {
    if (timer.current) clearTimeout(timer.current);
    try {
      const ok = await setStringAsync(orderNumber);
      setState(ok ? 'copied' : 'failed');
    } catch {
      setState('failed');
    }
    timer.current = setTimeout(() => setState('idle'), 2000);
  };

  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={COPY_LABELS[state].a11y}
      onPress={() => void copy()}
      className="min-h-touch items-center justify-center rounded-input border border-border bg-surface-raised px-md"
    >
      <Text className="text-ui font-semibold text-foreground">{COPY_LABELS[state].text}</Text>
    </Pressable>
  );
}

type Reorder = ReturnType<typeof useReorder>;

function ReorderAction({ reorder: { state, reorder }, compact }: { reorder: Reorder; compact: boolean }) {
  const pending = state.status === 'pending';
  const done = state.status === 'done';
  const disabled = pending || done;
  const label = pending ? 'Adding items to cart' : done ? 'Items added to cart' : 'Reorder all items';
  const text = pending ? 'Adding…' : done ? 'Added to cart' : 'Reorder';
  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={label}
      accessibilityState={{ disabled, busy: pending }}
      disabled={disabled}
      onPress={reorder}
      className={`min-h-touch items-center justify-center rounded-input border border-primary bg-primary px-lg ${compact ? 'flex-1' : ''} ${disabled ? 'opacity-50' : ''}`}
    >
      <Text className="text-ui font-semibold text-primary-foreground">{text}</Text>
    </Pressable>
  );
}

function ReorderResult({ reorder: { state, reorder } }: { reorder: Reorder }) {
  if (state.status === 'done') {
    return (
      <View className="items-start gap-xs border-t border-border pt-lg">
        <View accessible accessibilityRole="summary" className="gap-xs">
          <Text className="text-panel-title font-bold text-foreground">Added to your cart</Text>
          {state.lines.map((line) => (
            <Text key={line.variantId} className="text-ui text-foreground">{`${line.name}: ${mergeResultText(line)}`}</Text>
          ))}
        </View>
        <Link href="/cart" asChild>
          <Pressable accessibilityRole="link" className="min-h-touch justify-center">
            <Text className="text-ui font-semibold text-primary">View cart</Text>
          </Pressable>
        </Link>
      </View>
    );
  }
  if (state.status === 'failed') {
    return (
      <View className="items-start gap-sm border-t border-border pt-lg">
        <Text accessibilityRole="alert" className="text-ui text-danger">
          {state.message}
        </Text>
        <Pressable
          accessibilityRole="button"
          onPress={reorder}
          className="min-h-touch items-center justify-center rounded-input border border-border bg-surface-raised px-lg"
        >
          <Text className="text-ui font-semibold text-foreground">Try again</Text>
        </Pressable>
      </View>
    );
  }
  return null;
}

function Header({ order, compact }: { order: OrderDetail; compact: boolean }) {
  const itemCount = order.items.reduce((sum, item) => sum + item.quantity, 0);
  const reorder = useReorder(order.items);
  return (
    <Panel>
      <View className="flex-row flex-wrap items-start gap-lg">
        <View className="min-w-[240px] flex-1 gap-xs">
          <Text className="text-label font-bold uppercase text-muted">Order detail</Text>
          <View className="flex-row flex-wrap items-center gap-md">
            <View
              accessible
              accessibilityRole="header"
              accessibilityLabel={`Order ${order.orderNumber}`}
              className="flex-row flex-wrap items-center gap-md"
            >
              <Text className={`${compact ? 'text-page-title-sm' : 'text-page-title'} font-bold text-foreground`}>Order</Text>
              <Text className="rounded-control border border-border bg-surface-raised px-[9px] py-[5px] font-mono text-panel-title font-semibold text-foreground">
                {order.orderNumber}
              </Text>
            </View>
            <CopyOrderNumber orderNumber={order.orderNumber} />
          </View>
          <Text className="text-ui text-muted">{`Placed ${formatOrderDate(order.createdAt)} · ${itemCount} ${itemCount === 1 ? 'item' : 'items'}`}</Text>
        </View>
        <View className={`flex-row flex-wrap gap-sm ${compact ? 'w-full' : ''}`}>
          {order.status === 'PendingPayment' ? null : <ReorderAction reorder={reorder} compact={compact} />}
          <Link href="/orders" asChild>
            <Pressable
              accessibilityRole="link"
              accessibilityLabel="All orders"
              className={`min-h-touch items-center justify-center rounded-input border border-border bg-surface-raised px-lg ${compact ? 'flex-1' : ''}`}
            >
              <Text className="text-ui font-semibold text-foreground">← All orders</Text>
            </Pressable>
          </Link>
        </View>
      </View>
      <View className="flex-row flex-wrap gap-sm border-t border-border pt-lg">
        <StatusPill tone={statusTone(order.status)} label={statusLabel(order.status)} />
        {order.paymentStatus ? (
          <StatusPill tone={paymentTone(order.paymentStatus)} label={`Payment: ${paymentStatusLabel(order.paymentStatus)}`} />
        ) : null}
        {order.statusNote ? <Text className="self-center text-ui-sm text-muted">{order.statusNote.pillNote}</Text> : null}
      </View>
      <ReorderResult reorder={reorder} />
    </Panel>
  );
}

function ItemRow({ item, last, compact }: { item: OrderDetailItem; last: boolean; compact: boolean }) {
  const amount = formatMoney(item.lineTotal);
  return (
    <View
      accessible
      accessibilityLabel={`${item.name}${item.colour ? `, ${item.colour}` : ''} × ${item.quantity}, ${amount}`}
      className={`flex-row items-center gap-lg py-md ${compact ? 'flex-wrap' : ''} ${last ? '' : 'border-b border-border'}`}
    >
      <ProductImageView url={item.image?.url ?? null} altText={item.image?.altText ?? ''} fallbackLabel={item.name} width={touch} />
      <View className="min-w-0 flex-1 gap-xs">
        <Text className="text-panel-title font-bold text-foreground">{item.name}</Text>
        <Text className="font-mono text-meta text-muted">{item.sku}</Text>
        {item.colour ? <Text className="text-meta text-muted">{`Colour: ${item.colour}`}</Text> : null}
      </View>
      <View className="items-center gap-xs">
        <Text className="rounded-control border border-border bg-surface-raised px-md py-xs text-ui font-bold text-foreground">{`× ${item.quantity}`}</Text>
        <Text className="text-label text-subtle">{`${formatMoney(item.unitPrice)} each`}</Text>
      </View>
      <Text className="ml-auto min-w-[76px] text-right text-panel-title font-bold tabular-nums text-foreground">{amount}</Text>
    </View>
  );
}

function CancellationAlert({ alert }: { alert: OrderAlert }) {
  return (
    <View
      accessible
      accessibilityLabel={`${alert.title}. ${alert.message}`}
      className="gap-xs rounded-panel border border-danger-border bg-danger-subtle px-lg py-md"
    >
      <Text className="text-ui font-bold text-danger">{alert.title}</Text>
      <Text className="text-ui text-foreground">{alert.message}</Text>
    </View>
  );
}

function Content({ order, twoColumn, compact }: { order: OrderDetail; twoColumn: boolean; compact: boolean }) {
  const address = order.shippingAddress;
  const note = totalsNote(order.status);
  return (
    <View className="gap-lg">
      <Breadcrumb orderNumber={order.orderNumber} />
      <Header order={order} compact={compact} />
      {order.statusNote?.alert ? <CancellationAlert alert={order.statusNote.alert} /> : null}

      <View className={`gap-lg ${twoColumn ? 'flex-row items-start' : ''}`}>
        <View className="min-w-0 flex-1 gap-lg">
          <Panel title="Items">
            {order.items.map((item, index) => (
              <ItemRow key={item.variantId} item={item} last={index === order.items.length - 1} compact={compact} />
            ))}
          </Panel>

          <ReturnRequestForm order={order} />

          <Panel title="Timeline" subtitle="Newest last">
            <OrderTimeline entries={order.timeline} />
          </Panel>
        </View>

        <View className={`gap-lg ${twoColumn ? 'w-[360px]' : ''}`}>
          <Panel title="Totals">
            <TotalRow label="Subtotal" amount={formatMoney(order.subtotal)} />
            <TotalRow label={`Shipping · ${order.shippingMethod.name}`} amount={formatMoney(order.shippingTotal)} />
            <TotalRow label="Tax" amount={formatMoney(order.taxTotal)} />
            <TotalRow label="Total" amount={formatMoney(order.grandTotal)} emphasis />
            {note ? <Text className="text-ui-sm text-muted">{note}</Text> : null}
          </Panel>

          {order.payment ? (
            <Panel title="Payment">
              <View className="flex-row items-center justify-between gap-md rounded-input border border-border bg-surface-raised px-md py-md">
                <Text className="shrink text-ui font-semibold text-foreground">{paymentMethodLabel(order.payment.method)}</Text>
              </View>
              <TotalRow label="Amount due" amount={formatMoney(order.payment.amountDue)} />
              <TotalRow label="Captured" amount={formatMoney(order.payment.captured)} />
            </Panel>
          ) : null}

          <Panel title="Shipping">
            <View className="flex-row items-center justify-between gap-md rounded-input border border-border bg-surface-raised px-md py-md">
              <Text className="shrink text-ui font-semibold text-foreground">{order.shippingMethod.name}</Text>
              <Text className="text-ui-sm tabular-nums text-muted">{formatMoney(order.shippingTotal)}</Text>
            </View>
            <View className="gap-xs">
              <Text className="text-label font-bold uppercase text-subtle">Deliver to</Text>
              <Text className="text-ui text-foreground">{address.fullName}</Text>
              <Text className="text-ui text-foreground">{address.line1}</Text>
              {address.line2 ? <Text className="text-ui text-foreground">{address.line2}</Text> : null}
              <Text className="text-ui text-foreground">
                {[address.city, address.region, address.postalCode].filter(Boolean).join(', ')}
              </Text>
              <Text className="text-ui text-foreground">{address.country}</Text>
            </View>
            {order.shipments.length > 0 ? (
              <View className="gap-xs">
                <Text className="text-label font-bold uppercase text-subtle">Fulfilment</Text>
                {order.shipments.map((shipment) => (
                  <View key={`${shipment.carrierName}-${shipment.trackingNumber}`} className="gap-xs">
                    <Text className="text-ui text-foreground">{`${shipment.carrierName} · ${shipment.trackingNumber}`}</Text>
                    <TrackingLink trackingNumber={shipment.trackingNumber} trackingUrl={shipment.trackingUrl} />
                  </View>
                ))}
              </View>
            ) : null}
          </Panel>

          {order.refunds.length > 0 ? (
            <Panel title="Refunds">
              {order.refunds.map((refund) => (
                <View key={`${refund.createdAt}-${refund.amount.amount}`} className="flex-row flex-wrap justify-between gap-sm">
                  <Text className="text-ui text-foreground">{`${formatMoney(refund.amount)} · ${refundStatusLabel(refund.status)}`}</Text>
                  <Text className="text-meta text-muted">{formatOrderDate(refund.createdAt)}</Text>
                </View>
              ))}
            </Panel>
          ) : null}
        </View>
      </View>
    </View>
  );
}

// 403 (another customer's order) and 404 read the same to the customer, and retrying cannot help.
function isMissing(error: unknown): boolean {
  return error instanceof ApiError && (error.status === 403 || error.status === 404);
}

export function OrderDetailView({ id }: { id: string }) {
  const query = useOrderQuery(id);
  useOrderLiveStatus(id);
  const { width } = useWindowDimensions();
  const { twoColumn, compact } = orderDetailLayout(width);

  return (
    <View className={`w-full max-w-content flex-1 gap-gutter self-center ${compact ? 'px-gutter py-lg' : 'p-xl'}`}>
      {query.data ? (
        <Content order={query.data} twoColumn={twoColumn} compact={compact} />
      ) : (
        <>
          <ScreenTitle title="Order" />
          {query.isError ? (
            isMissing(query.error) ? (
              <ErrorState message="We could not find that order." />
            ) : (
              <ErrorState message={userMessage(query.error)} onRetry={() => void query.refetch()} />
            )
          ) : (
            <LoadingState />
          )}
        </>
      )}
    </View>
  );
}
