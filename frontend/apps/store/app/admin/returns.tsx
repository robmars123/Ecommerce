import { Stack } from 'expo-router';

import { AdminReturnsView } from '@/features/admin-orders/components/AdminReturnsView';

export default function AdminReturnListScreen() {
  return (
    <>
      <Stack.Screen options={{ headerShown: false }} />
      <AdminReturnsView />
    </>
  );
}
