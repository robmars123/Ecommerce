import { Text } from 'react-native';

import { useGuestCartStore } from '../guest-cart-store';

export const GUEST_CART_NOTICE = "Items in this device's cart will be added to your account.";

/** Shown on sign-in and register only while this device holds cart lines, so it is always true. */
export function GuestCartNotice() {
  const hasLines = useGuestCartStore((s) => s.lines.length > 0);
  return hasLines ? <Text className="text-body text-muted">{GUEST_CART_NOTICE}</Text> : null;
}
