import { Link, useRouter } from 'expo-router';
import { Text, View } from 'react-native';

import { EmptyState } from '@/components/EmptyState';
import { ErrorState } from '@/components/ErrorState';
import { LoadingState } from '@/components/LoadingState';
import { ScreenTitle } from '@/components/ScreenTitle';
import { useSession } from '@/features/auth/hooks/useSession';
import { userMessage } from '@/lib/api-client';
import { formatMoney } from '@/lib/money';

import { cartErrorMessage } from '../cart-messages';
import { useGuestCartStore } from '../guest-cart-store';
import { useCartMutations } from '../hooks/useCartMutations';
import { useCartQuery } from '../hooks/useCartQuery';
import { CartLineRow, GuestLineRow } from './CartLineRow';

function EmptyCart() {
  const router = useRouter();
  return <EmptyState message="Your cart is empty." action={{ label: 'Browse products', onPress: () => router.push('/products') }} />;
}

/** The signed-in cart. Every price, total and flag is the server's; this screen does no price arithmetic. */
function ServerCart() {
  const cart = useCartQuery();
  const { set, remove } = useCartMutations();

  if (cart.isPending) {
    return <LoadingState />;
  }
  if (cart.isError) {
    return <ErrorState message={userMessage(cart.error)} onRetry={() => void cart.refetch()} />;
  }
  if (cart.data.items.length === 0) {
    return <EmptyCart />;
  }

  const writeError = set.error ?? remove.error;
  const busy = set.isPending || remove.isPending;
  return (
    <View className="gap-md">
      {cart.data.items.map((line) => (
        <CartLineRow
          key={line.variantId}
          line={line}
          busy={busy}
          onSetQuantity={(quantity) => set.mutate({ variantId: line.variantId, quantity })}
          onRemove={() => remove.mutate(line.variantId)}
        />
      ))}
      {writeError ? (
        <Text accessibilityRole="alert" className="text-body text-danger">
          {cartErrorMessage(writeError)}
        </Text>
      ) : null}
      <Text className="text-title font-semibold text-foreground">{`Subtotal ${formatMoney(cart.data.subtotal)}`}</Text>
      <Text className="text-small text-muted">Final prices and availability are confirmed at checkout.</Text>
      <Link href="/checkout" asChild>
        <Text accessibilityRole="link" className="min-h-touch py-md text-body font-semibold text-foreground">
          Checkout
        </Text>
      </Link>
    </View>
  );
}

/** The device cart for a signed-out visitor. Prices are estimates until sign-in. */
function GuestCart() {
  const lines = useGuestCartStore((s) => s.lines);
  const setQuantity = useGuestCartStore((s) => s.setQuantity);
  const remove = useGuestCartStore((s) => s.remove);

  if (lines.length === 0) {
    return <EmptyCart />;
  }
  return (
    <View className="gap-md">
      {lines.map((line) => (
        <GuestLineRow
          key={line.variantId}
          line={line}
          onSetQuantity={(quantity) => setQuantity(line.variantId, quantity)}
          onRemove={() => remove(line.variantId)}
        />
      ))}
      <Text className="text-small text-muted">Prices are estimates until you sign in. Your cart will be checked and added to your account.</Text>
      <Link href={{ pathname: '/login', params: { returnTo: '/cart' } }} asChild>
        <Text accessibilityRole="link" className="min-h-touch py-md text-body font-semibold text-foreground">
          Sign in to continue
        </Text>
      </Link>
    </View>
  );
}

export function CartView() {
  const session = useSession();
  return (
    <View className="w-full max-w-content gap-lg self-center p-lg">
      <ScreenTitle title="Cart" />
      {session.status === 'restoring' ? <LoadingState /> : session.status === 'signed-in' ? <ServerCart /> : <GuestCart />}
    </View>
  );
}
