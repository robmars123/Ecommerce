import { useLocalSearchParams } from 'expo-router';

import { AdminOrderListView } from '@/features/admin-orders/components/AdminOrderListView';

export default function AdminOrderListScreen() {
  const { status } = useLocalSearchParams<{ status?: string }>();
  return <AdminOrderListView initialStatus={status} />;
}
