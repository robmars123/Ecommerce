import { Stack, useLocalSearchParams } from 'expo-router';

import { AdminProductDetailView } from '@/features/admin-catalog/components/AdminProductDetailView';

export default function AdminProductDetailScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  return (
    <>
      <Stack.Screen options={{ headerShown: false }} />
      <AdminProductDetailView id={id} />
    </>
  );
}
