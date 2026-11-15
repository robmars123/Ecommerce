import { Text, View } from 'react-native';

import { ActionButton } from '@/components/ActionButton';
import { Panel } from '@/components/Panel';
import { ScreenTitle } from '@/components/ScreenTitle';
import { useLogoutMutation } from '@/features/auth/hooks/useLogoutMutation';
import { useSession } from '@/features/auth/hooks/useSession';

function Row({ label, value }: { label: string; value: string }) {
  return (
    <View className="gap-xs">
      <Text className="text-caption text-subtle">{label}</Text>
      <Text className="text-body text-foreground">{value}</Text>
    </View>
  );
}

/** Read-only: the signed-in user's own name, email and role from the session, plus Log out. */
export function AccountView() {
  const { user } = useSession();
  const logout = useLogoutMutation();

  // RequireSignIn guarantees a user; null covers the instant between endSession and its redirect.
  if (!user) return null;

  return (
    <View className="w-full max-w-content flex-1 gap-gutter self-center p-lg">
      <ScreenTitle title="Account" />
      <Panel>
        <Row label="Name" value={user.displayName.trim() || 'Not set'} />
        <Row label="Email" value={user.email} />
        <Row label="Role" value={user.role} />
      </Panel>
      <ActionButton label="Log out" onPress={() => logout.mutate()} pending={logout.isPending} />
    </View>
  );
}
