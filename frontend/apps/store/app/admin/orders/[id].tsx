import { useLocalSearchParams } from 'expo-router';

import { AdminOrderDetailView } from '@/features/admin-orders/components/AdminOrderDetailView';

export default function AdminOrderDetailScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  return <AdminOrderDetailView id={id} />;
}
