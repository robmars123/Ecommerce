import { Stack } from 'expo-router';

import { AdminShell } from '@/features/admin-shell/components/AdminShell';
import { AdminGate } from '@/features/auth/components/AdminGate';
import { attentionItems, type AttentionState } from '@/features/admin-shell/attention';
import { navBadges } from '@/features/admin-shell/badges';
import { useShellDashboardQuery } from '@/features/admin-shell/hooks';
import { useSession } from '@/features/auth/hooks/useSession';
import { useThemeColors } from '@/lib/theme';

export default function AdminLayout() {
  const colors = useThemeColors();
  const role = useSession().user?.role ?? null;
  const shell = useShellDashboardQuery(role);
  // A failed poll keeps the last data; loading shows no card.
  const attention: AttentionState | undefined = shell.data
    ? { kind: 'ready', items: attentionItems(shell.data, role) }
    : shell.isError
      ? { kind: 'unavailable' }
      : undefined;
  return (
    <AdminGate>
      {/* A nested Stack does not inherit the root's screenOptions; without these it falls back to
          React Navigation's light theme, unreadable against the token text. Token-sourced (arch §5.5). */}
      <AdminShell badges={shell.data ? navBadges(shell.data) : undefined} attention={attention}>
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
