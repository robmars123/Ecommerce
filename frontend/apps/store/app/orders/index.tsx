import { OrderListView } from '@/features/orders/components/OrderListView';
import { RequireSignIn } from '@/features/orders/components/RequireSignIn';

export default function OrderListScreen() {
  return (
    <RequireSignIn returnTo="/orders">
      <OrderListView />
    </RequireSignIn>
  );
}
