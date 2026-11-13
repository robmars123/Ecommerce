import { Stack } from 'expo-router';

import { AdminSettingsView } from '@/features/admin-settings/components/AdminSettingsView';

export default function AdminSettingsScreen() {
  return (
    <>
      <Stack.Screen options={{ headerShown: false }} />
      <AdminSettingsView />
    </>
  );
}
