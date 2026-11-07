import { Link, useLocalSearchParams, useRouter } from 'expo-router';
import { useState } from 'react';
import { ScrollView, Text, View } from 'react-native';

import { ActionButton } from '@/components/ActionButton';
import { ErrorState } from '@/components/ErrorState';
import { LoadingState } from '@/components/LoadingState';
import { ScreenTitle } from '@/components/ScreenTitle';
import type { Cart } from '@/features/cart/types';
import { useCartMutations } from '@/features/cart/hooks/useCartMutations';
import { useCartQuery } from '@/features/cart/hooks/useCartQuery';
import { ApiError, userMessage, type RejectedLine } from '@/lib/api-client';
import { formatMoney } from '@/lib/money';

import { estimateTotal } from '../estimate';
import { usePlaceOrderMutation } from '../hooks/usePlaceOrderMutation';
import { useShippingMethodsQuery } from '../hooks/useShippingMethodsQuery';
import type { AddressInput, CheckoutBody, CheckoutResponse } from '../types';
import { AddressForm, EMPTY_ADDRESS } from './AddressForm';
import { PaymentStep } from './PaymentStep';
import { ShippingMethodPicker } from './ShippingMethodPicker';

function toBody(address: AddressInput, shippingMethodCode: string): CheckoutBody {
  return {
    shippingAddress: {
      fullName: address.fullName,
      line1: address.line1,
      ...(address.line2.trim() ? { line2: address.line2 } : {}),
      city: address.city,
      ...(address.region.trim() ? { region: address.region } : {}),
      postalCode: address.postalCode,
      country: address.country,
    },
    shippingMethodCode,
  };
}

/** First server message per wire field name (`shippingAddress.city`, `shippingMethodCode`). */
function fieldMessages(error: unknown): Record<string, string> {
  if (!(error instanceof ApiError) || error.kind !== 'validation' || !error.fieldErrors) {
    return {};
  }
  return Object.fromEntries(Object.entries(error.fieldErrors).map(([field, messages]) => [field, messages[0]]));
}

function lineText(line: RejectedLine, name: string): string {
  switch (line.reason) {
    case 'OutOfStock':
      return line.available && line.available > 0 ? `${name}: only ${line.available} left.` : `${name}: sold out.`;
    case 'PriceChanged':
      return `${name}: the price changed.`;
    default:
      return `${name}: no longer available.`;
  }
}

function nameOf(cart: Cart | undefined, variantId: string): string {
  const line = cart?.items.find((item) => item.variantId === variantId);
  return line ? `${line.productName} (${line.variantName})` : 'An item';
}

type DetailsProps = {
  onPlaced: (response: CheckoutResponse) => void;
};

/** Step 1: address, shipping method, estimate, place the order. */
function Details({ onPlaced }: DetailsProps) {
  const cart = useCartQuery();
  const methods = useShippingMethodsQuery();
  const { set } = useCartMutations();
  const place = usePlaceOrderMutation();
  const [address, setAddress] = useState<AddressInput>(EMPTY_ADDRESS);
  const [methodCode, setMethodCode] = useState<string | null>(null);
  const [accepting, setAccepting] = useState(false);

  if (cart.isPending || methods.isPending) {
    return <LoadingState />;
  }
  if (cart.isError) {
    return <ErrorState message={userMessage(cart.error)} onRetry={() => void cart.refetch()} />;
  }
  if (methods.isError) {
    return <ErrorState message={userMessage(methods.error)} onRetry={() => void methods.refetch()} />;
  }

  const items = cart.data.items;
  const selectedCode = methodCode ?? methods.data.items[0]?.code ?? null;
  const selected = methods.data.items.find((m) => m.code === selectedCode);
  const estimate = selected ? estimateTotal(cart.data.subtotal, selected.price) : null;
  const error = place.error;
  const fields = fieldMessages(error);
  const busy = place.isPending || accepting;
  const rejected = error instanceof ApiError && error.problemType === 'checkout-lines-rejected' ? (error.lines ?? []) : [];
  const priceChanged = rejected.filter((line) => line.reason === 'PriceChanged');

  function submit() {
    if (!selectedCode) {
      return;
    }
    place.submit(toBody(address, selectedCode), onPlaced);
  }

  async function acceptNewPrices() {
    if (!selectedCode) {
      return;
    }
    setAccepting(true);
    try {
      // PUT with the same quantity refreshes the price the cart last showed; then a new attempt (new key) is made.
      await Promise.all(
        priceChanged.map((line) => {
          const quantity = items.find((item) => item.variantId === line.variantId)?.quantity ?? 1;
          return set.mutateAsync({ variantId: line.variantId, quantity });
        }),
      );
      place.submit(toBody(address, selectedCode), onPlaced);
    } catch {
      // The cart write failed and refetched the cart; the customer sees the current prices and can try again.
    } finally {
      setAccepting(false);
    }
  }

  if (items.length === 0 && !place.isPending) {
    return (
      <View className="gap-md">
        <Text className="text-body text-foreground">Your cart is empty.</Text>
        <Link href="/products" asChild>
          <Text accessibilityRole="link" className="min-h-touch py-md text-body font-semibold text-foreground">
            Browse products
          </Text>
        </Link>
      </View>
    );
  }

  return (
    <View className="gap-lg">
      <View className="gap-xs">
        <Text accessibilityRole="header" className="text-title font-semibold text-foreground">
          Your items
        </Text>
        {items.map((item) => (
          <Text key={item.variantId} className="text-body text-foreground">{`${item.productName} (${item.variantName}) × ${item.quantity}`}</Text>
        ))}
      </View>

      <View className="gap-sm">
        <Text accessibilityRole="header" className="text-title font-semibold text-foreground">
          Shipping address
        </Text>
        <AddressForm value={address} onChange={setAddress} errors={fields} editable={!busy} />
      </View>

      {methods.data.items.length === 0 ? (
        <Text accessibilityRole="alert" className="text-body text-danger">
          No shipping methods are available right now.
        </Text>
      ) : (
        <ShippingMethodPicker
          methods={methods.data.items}
          selected={selectedCode}
          onSelect={setMethodCode}
          error={fields.shippingMethodCode}
          editable={!busy}
        />
      )}

      {estimate ? (
        <View className="gap-xs">
          <Text className="text-title font-semibold text-foreground">{`Estimated total ${formatMoney(estimate)}`}</Text>
          <Text className="text-small text-muted">Tax is calculated at checkout. The final total comes from the store.</Text>
        </View>
      ) : null}

      {rejected.length > 0 ? (
        <View className="gap-xs">
          {rejected.map((line) => (
            <Text key={line.variantId} accessibilityRole="alert" className="text-body text-danger">
              {lineText(line, nameOf(cart.data, line.variantId))}
            </Text>
          ))}
          {priceChanged.length > 0 ? (
            <ActionButton label="Accept new prices" onPress={() => void acceptNewPrices()} pending={busy} />
          ) : (
            <Link href="/cart" asChild>
              <Text accessibilityRole="link" className="min-h-touch py-md text-body font-semibold text-foreground">
                Edit your cart
              </Text>
            </Link>
          )}
        </View>
      ) : error ? (
        <Text accessibilityRole="alert" className="text-body text-danger">
          {errorText(error)}
        </Text>
      ) : null}

      <ActionButton label="Place order" onPress={submit} pending={busy} disabled={!selectedCode} />
    </View>
  );
}

function errorText(error: unknown): string {
  if (error instanceof ApiError) {
    if (error.problemType === 'cart-empty') return 'Your cart is empty.';
    if (error.problemType === 'payment-provider-unavailable') return 'Payment is temporarily unavailable. Please try again.';
    if (error.kind === 'validation') return 'Please check the highlighted fields.';
  }
  return userMessage(error);
}

/**
 * The checkout screen (arch 8.2, app context 4.5). Details, then payment. Once the order exists its id is in the URL, so a
 * refresh resumes the payment of that order and never makes a second one.
 */
export function CheckoutView() {
  const params = useLocalSearchParams<{ orderId?: string; redirect_status?: string }>();
  const router = useRouter();
  const [placed, setPlaced] = useState<CheckoutResponse | null>(null);
  const orderId = params.orderId ?? placed?.order.id;

  function onPlaced(response: CheckoutResponse) {
    if (placed?.order.id === response.order.id) {
      return;
    }
    setPlaced(response);
    router.setParams({ orderId: response.order.id });
  }

  function checkOutAgain() {
    setPlaced(null);
    router.setParams({ orderId: undefined, redirect_status: undefined });
  }

  return (
    <ScrollView contentContainerClassName="w-full max-w-content gap-lg self-center p-lg" keyboardShouldPersistTaps="handled">
      <ScreenTitle title="Checkout" />
      {orderId ? (
        <PaymentStep
          key={orderId}
          orderId={orderId}
          initialSecret={placed?.order.id === orderId ? placed.clientSecret : null}
          placedTotal={placed?.order.id === orderId ? placed.order.grandTotal : undefined}
          startConfirming={params.redirect_status === 'succeeded' || params.redirect_status === 'processing'}
          onCheckoutAgain={checkOutAgain}
        />
      ) : (
        <Details onPlaced={onPlaced} />
      )}
    </ScrollView>
  );
}
