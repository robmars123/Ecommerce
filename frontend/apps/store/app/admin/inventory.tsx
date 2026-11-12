import { Stack } from 'expo-router';

import { AdminInventoryView } from '@/features/admin-inventory/components/AdminInventoryView';

export default function AdminInventoryScreen() {
  return (
    <>
      <Stack.Screen options={{ headerShown: false }} />
      <AdminInventoryView />
    </>
  );
}
