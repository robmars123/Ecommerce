import { Stack, useLocalSearchParams } from 'expo-router';

import { AdminOrderDetailView } from '@/features/admin-orders/components/AdminOrderDetailView';

export default function AdminOrderDetailScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  return (
    <>
      <Stack.Screen options={{ headerShown: false }} />
      <AdminOrderDetailView id={id} />
    </>
  );
}
