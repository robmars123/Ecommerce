import { CheckoutView } from '@/features/checkout/components/CheckoutView';
import { RequireSignIn } from '@/features/auth/components/RequireSignIn';

export default function CheckoutScreen() {
  return (
    <RequireSignIn returnTo="/checkout">
      <CheckoutView />
    </RequireSignIn>
  );
}
