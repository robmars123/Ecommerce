import { Stack } from 'expo-router';

import { AdminCustomerListView } from '@/features/admin-customers/components/AdminCustomerListView';

export default function AdminCustomerListScreen() {
  return (
    <>
      <Stack.Screen options={{ headerShown: false }} />
      <AdminCustomerListView />
    </>
  );
}
