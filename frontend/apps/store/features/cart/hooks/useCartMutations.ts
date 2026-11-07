import { useMutation, useQueryClient } from '@tanstack/react-query';

import { queryKeys } from '@/lib/query-keys';

import { addCartItem, removeCartItem, setCartItem } from '../api';
import type { Cart } from '../types';

/**
 * Cart writes. The server reply is the whole cart and replaces the cached one (F3): there are no optimistic writes, and
 * a failed write refetches so the screen never keeps a value the server has moved past.
 */
export function useCartMutations() {
  const queryClient = useQueryClient();
  const replace = (cart: Cart) => queryClient.setQueryData(queryKeys.cart, cart);
  const refetch = () => queryClient.invalidateQueries({ queryKey: queryKeys.cart });

  const add = useMutation({
    mutationFn: (input: { variantId: string; quantity: number }) => addCartItem(input.variantId, input.quantity),
    onSuccess: replace,
    onError: refetch,
  });
  const set = useMutation({
    mutationFn: (input: { variantId: string; quantity: number }) => setCartItem(input.variantId, input.quantity),
    onSuccess: replace,
    onError: refetch,
  });
  const remove = useMutation({
    mutationFn: (variantId: string) => removeCartItem(variantId),
    onSettled: refetch,
  });
  return { add, set, remove };
}
