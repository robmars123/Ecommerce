import { Stack } from 'expo-router';

import { AdminCategoriesView } from '@/features/admin-catalog/components/AdminCategoriesView';

export default function AdminCategoriesScreen() {
  return (
    <>
      <Stack.Screen options={{ headerShown: false }} />
      <AdminCategoriesView />
    </>
  );
}
