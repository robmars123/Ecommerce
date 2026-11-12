import { Stack } from 'expo-router';

import { AdminOrderListView } from '@/features/admin-orders/components/AdminOrderListView';

export default function AdminOrderListScreen() {
  return (
    <>
      <Stack.Screen options={{ headerShown: false }} />
      <AdminOrderListView />
    </>
  );
}
