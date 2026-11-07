import { useQueryClient } from '@tanstack/react-query';
import { Link } from 'expo-router';
import { useRef, useState } from 'react';
import { Pressable, Text, View } from 'react-native';

import { cartErrorMessage } from '@/features/cart/cart-messages';
import { useAddToCart } from '@/features/cart/hooks/useAddToCart';
import { userMessage } from '@/lib/api-client';
import { queryKeys } from '@/lib/query-keys';

import { fetchProduct } from '../api';
import type { ProductDetail, ProductListItem } from '../types';
import { QuickAddPicker } from './QuickAddPicker';

type Feedback = { kind: 'added' } | { kind: 'error'; text: string } | null;

/**
 * "Add to Cart" on a Shop card. The list item has no variants, so a tap loads the product detail (the same query as the
 * product page). One variant goes straight into the cart (quantity 1); several open the picker. The button stays disabled
 * while the detail loads or the add runs, so a double tap cannot add twice.
 */
export function QuickAdd({ item }: { item: ProductListItem }) {
  const queryClient = useQueryClient();
  const { addLine, restoring } = useAddToCart();
  const inFlight = useRef(false);
  const [busy, setBusy] = useState(false);
  const [picker, setPicker] = useState<ProductDetail | null>(null);
  const [feedback, setFeedback] = useState<Feedback>(null);

  const out = item.stockStatus === 'Out';
  const disabled = out || busy || restoring;
  const label = out ? `Add to Cart: ${item.name} is out of stock` : `Add to Cart: ${item.name}`;

  async function onPress() {
    if (inFlight.current || out) {
      return;
    }
    inFlight.current = true;
    setBusy(true);
    setFeedback(null);
    try {
      const product = await queryClient.fetchQuery({
        queryKey: queryKeys.products.detail(item.slug),
        queryFn: ({ signal }) => fetchProduct(item.slug, signal),
      });
      if (product.variants.length !== 1) {
        setPicker(product);
        return;
      }
      const variant = product.variants[0];
      if (variant.stockStatus === 'Out') {
        setFeedback({ kind: 'error', text: 'This item is out of stock.' });
        return;
      }
      await new Promise<void>((resolve) =>
        addLine(product, variant, 1, {
          onSuccess: () => {
            setFeedback({ kind: 'added' });
            resolve();
          },
          onError: (error) => {
            setFeedback({ kind: 'error', text: cartErrorMessage(error) });
            resolve();
          },
        }),
      );
    } catch (error) {
      setFeedback({ kind: 'error', text: userMessage(error) });
    } finally {
      inFlight.current = false;
      setBusy(false);
    }
  }

  return (
    <View className="gap-sm px-lg pb-lg">
      <Pressable
        accessibilityRole="button"
        accessibilityLabel={label}
        accessibilityState={{ disabled, busy }}
        disabled={disabled}
        onPress={() => void onPress()}
        className={`min-h-touch items-center justify-center rounded-control border border-border px-lg ${disabled ? 'opacity-50' : ''}`}
      >
        <Text className="text-body font-semibold text-foreground">Add to Cart</Text>
      </Pressable>
      {feedback?.kind === 'error' ? (
        <Text accessibilityRole="alert" className="text-body text-danger">
          {feedback.text}
        </Text>
      ) : null}
      {feedback?.kind === 'added' ? (
        <View className="flex-row items-center gap-md">
          <Text accessibilityRole="alert" className="text-body text-foreground">
            Added to cart
          </Text>
          <Link href="/cart" asChild>
            <Text accessibilityRole="link" className="min-h-touch py-md text-body font-semibold text-foreground">
              View cart
            </Text>
          </Link>
        </View>
      ) : null}
      {picker ? <QuickAddPicker product={picker} onClose={() => setPicker(null)} /> : null}
    </View>
  );
}
