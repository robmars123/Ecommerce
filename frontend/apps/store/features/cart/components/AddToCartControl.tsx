import { Link } from 'expo-router';
import { useState } from 'react';
import { Pressable, Text, View } from 'react-native';

import { ActionButton } from '@/components/ActionButton';
import type { ProductDetail, ProductVariant } from '@/features/catalog/types';

import { cartErrorMessage } from '../cart-messages';
import { useAddToCart } from '../hooks/useAddToCart';

export const MAX_ADD_QUANTITY = 10;

/** Add the selected variant to the cart through the shared add path (useAddToCart), with quantity and feedback. */
export function AddToCartControl({ product, variant }: { product: ProductDetail; variant: ProductVariant }) {
  const { addLine, isPending, restoring } = useAddToCart();
  const [quantity, setQuantity] = useState(1);
  const [added, setAdded] = useState(false);
  const [error, setError] = useState<unknown>(null);

  const out = variant.stockStatus === 'Out';

  function submit() {
    setAdded(false);
    setError(null);
    addLine(product, variant, quantity, { onSuccess: () => setAdded(true), onError: setError });
  }

  return (
    <View className="gap-sm border-t border-border pt-md">
      <View className="flex-row items-center gap-md">
        <Pressable
          accessibilityRole="button"
          accessibilityLabel="Decrease quantity"
          disabled={quantity <= 1}
          onPress={() => setQuantity((q) => Math.max(1, q - 1))}
          className={`min-h-touch min-w-touch items-center justify-center rounded-control border border-border ${quantity <= 1 ? 'opacity-50' : ''}`}
        >
          <Text className="text-body font-semibold text-foreground">-</Text>
        </Pressable>
        <Text accessibilityLabel="Quantity to add" className="min-w-touch text-center text-body text-foreground">
          {quantity}
        </Text>
        <Pressable
          accessibilityRole="button"
          accessibilityLabel="Increase quantity"
          disabled={quantity >= MAX_ADD_QUANTITY}
          onPress={() => setQuantity((q) => Math.min(MAX_ADD_QUANTITY, q + 1))}
          className={`min-h-touch min-w-touch items-center justify-center rounded-control border border-border ${quantity >= MAX_ADD_QUANTITY ? 'opacity-50' : ''}`}
        >
          <Text className="text-body font-semibold text-foreground">+</Text>
        </Pressable>
      </View>
      <ActionButton label="Add to cart" onPress={submit} disabled={out || restoring} pending={isPending} />
      {out ? <Text className="text-small text-muted">This item is out of stock.</Text> : null}
      {error ? (
        <Text accessibilityRole="alert" className="text-body text-danger">
          {cartErrorMessage(error)}
        </Text>
      ) : null}
      {added ? (
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
    </View>
  );
}
