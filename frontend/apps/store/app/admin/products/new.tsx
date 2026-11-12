import { Stack } from 'expo-router';

import { AdminProductCreateView } from '@/features/admin-catalog/components/AdminProductCreateView';

export default function AdminProductCreateScreen() {
  return (
    <>
      <Stack.Screen options={{ headerShown: false }} />
      <AdminProductCreateView />
    </>
  );
}
