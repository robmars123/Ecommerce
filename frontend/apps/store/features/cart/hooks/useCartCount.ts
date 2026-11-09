import { useSession } from '@/features/auth/hooks/useSession';

import { useGuestCartStore } from '../guest-cart-store';
import { useCartQuery } from './useCartQuery';

/** Total item quantity for the header badge: the server cart when signed in, the device cart otherwise. Null until the server cart has loaded. */
export function useCartCount(): number | null {
  const session = useSession();
  const server = useCartQuery();
  const guestLines = useGuestCartStore((s) => s.lines);
  if (session.status === 'signed-in') {
    return server.data ? server.data.items.reduce((sum, line) => sum + line.quantity, 0) : null;
  }
  return guestLines.reduce((sum, line) => sum + line.quantity, 0);
}
