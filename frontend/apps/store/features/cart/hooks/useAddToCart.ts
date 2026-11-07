import { useSession } from '@/features/auth/hooks/useSession';
import type { ProductDetail, ProductVariant } from '@/features/catalog/types';

import { useGuestCartStore } from '../guest-cart-store';
import { useCartMutations } from './useCartMutations';

type AddHandlers = { onSuccess: () => void; onError: (error: unknown) => void };

/**
 * The one way a variant goes into the cart. Signed in: the server decides and its answer wins (a stock conflict or a limit
 * is reported as the server says it). Signed out: the line goes to the device cart as a display snapshot (estimate only).
 */
export function useAddToCart() {
  const session = useSession();
  const { add } = useCartMutations();
  const addToGuest = useGuestCartStore((s) => s.add);

  function addLine(product: ProductDetail, variant: ProductVariant, quantity: number, handlers: AddHandlers): void {
    if (session.status === 'signed-in') {
      add.mutate({ variantId: variant.id, quantity }, { onSuccess: handlers.onSuccess, onError: handlers.onError });
      return;
    }
    const image = product.images[0];
    addToGuest(
      {
        variantId: variant.id,
        productSlug: product.slug,
        snapshot: {
          productName: product.name,
          variantName: variant.name,
          price: variant.price,
          image: image?.url ? { url: image.url, altText: image.altText } : null,
        },
      },
      quantity,
    );
    handlers.onSuccess();
  }

  return { addLine, isPending: add.isPending, signedIn: session.status === 'signed-in', restoring: session.status === 'restoring' };
}
