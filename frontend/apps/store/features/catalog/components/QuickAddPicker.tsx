import { useState } from 'react';
import { Modal, Pressable, ScrollView, Text, View } from 'react-native';

import { AddToCartControl } from '@/features/cart/components/AddToCartControl';
import { useThemeStyle } from '@/lib/theme';

import type { ProductDetail } from '../types';
import { PriceText } from './PriceText';
import { VariantSelector } from './VariantSelector';

/**
 * The Shop quick-add picker for a product with several variants. Variant choice, quantity, the cart add and its feedback
 * are the product page's own pieces; the price shown is the server's detail response, never computed here.
 */
export function QuickAddPicker({ product, onClose }: { product: ProductDetail; onClose: () => void }) {
  const firstAvailable = product.variants.find((v) => v.stockStatus !== 'Out') ?? product.variants[0];
  // Web portals a Modal outside the root view, so it must set the theme variables itself (arch 5.5).
  const themeStyle = useThemeStyle();
  const [variantId, setVariantId] = useState<string | undefined>(firstAvailable?.id);
  const variant = product.variants.find((v) => v.id === variantId) ?? firstAvailable;

  return (
    <Modal transparent animationType="fade" visible onRequestClose={onClose}>
      <View style={themeStyle} className="flex-1 items-center justify-center bg-black/50 p-lg">
        <View className="max-h-full w-full max-w-content rounded-card border border-border bg-surface p-lg">
          <ScrollView contentContainerClassName="gap-md">
            <Text accessibilityRole="header" className="text-title font-semibold text-foreground">
              {product.name}
            </Text>
            {variant ? <PriceText price={variant.price} compareAtPrice={variant.compareAtPrice} /> : null}
            <VariantSelector variants={product.variants} selectedId={variant?.id} onSelect={setVariantId} />
            {variant ? <AddToCartControl key={variant.id} product={product} variant={variant} /> : null}
            <Pressable
              accessibilityRole="button"
              accessibilityLabel="Close"
              onPress={onClose}
              className="min-h-touch items-center justify-center rounded-control border border-border px-lg"
            >
              <Text className="text-body font-semibold text-foreground">Close</Text>
            </Pressable>
          </ScrollView>
        </View>
      </View>
    </Modal>
  );
}
