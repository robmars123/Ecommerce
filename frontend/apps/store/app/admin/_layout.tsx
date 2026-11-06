import { Stack } from 'expo-router';

import { AdminGate } from '@/features/auth/components/AdminGate';

export default function AdminLayout() {
  return (
    <AdminGate>
      <Stack />
    </AdminGate>
  );
}
