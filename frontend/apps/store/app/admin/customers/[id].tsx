import { Stack, useLocalSearchParams } from 'expo-router';

import { AdminCustomerDetailView } from '@/features/admin-customers/components/AdminCustomerDetailView';

export default function AdminCustomerDetailScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  return (
    <>
      <Stack.Screen options={{ headerShown: false }} />
      <AdminCustomerDetailView id={id} />
    </>
  );
}
