import { Stack } from 'expo-router';

import { AdminProductListView } from '@/features/admin-catalog/components/AdminProductListView';

export default function AdminProductListScreen() {
  return (
    <>
      <Stack.Screen options={{ headerShown: false }} />
      <AdminProductListView />
    </>
  );
}
