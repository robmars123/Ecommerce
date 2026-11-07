import { useLocalSearchParams } from 'expo-router';

import { OrderDetailView } from '@/features/orders/components/OrderDetailView';
import { RequireSignIn } from '@/features/orders/components/RequireSignIn';

export default function OrderDetailScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  return (
    <RequireSignIn returnTo={`/orders/${id}`}>
      <OrderDetailView id={id} />
    </RequireSignIn>
  );
}
