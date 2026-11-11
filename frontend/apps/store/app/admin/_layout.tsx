import { Stack } from 'expo-router';

import { AdminShell } from '@/features/admin-shell/components/AdminShell';
import { AdminGate } from '@/features/auth/components/AdminGate';
import { colors } from '@/lib/design-tokens';

export default function AdminLayout() {
  return (
    <AdminGate>
      {/* A nested Stack does not inherit the root's screenOptions; without these it falls back to
          React Navigation's light theme and the dark tokens' light text is unreadable. Token-sourced (arch §5.5). */}
      <AdminShell>
        <Stack
          screenOptions={{
            contentStyle: { backgroundColor: colors.background },
            headerStyle: { backgroundColor: colors.surface },
            headerTintColor: colors.foreground,
          }}
        />
      </AdminShell>
    </AdminGate>
  );
}
