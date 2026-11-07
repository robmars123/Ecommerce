import { useQuery } from '@tanstack/react-query';

import { useSession } from '@/features/auth/hooks/useSession';
import { queryKeys } from '@/lib/query-keys';

import { fetchCart } from '../api';

/** The server cart. Only for a signed-in user; a signed-out device uses the guest store. */
export function useCartQuery() {
  const session = useSession();
  return useQuery({
    queryKey: queryKeys.cart,
    queryFn: ({ signal }) => fetchCart(signal),
    enabled: session.status === 'signed-in',
  });
}
