import { Link } from 'expo-router';
import { Text, View } from 'react-native';

import { ActionButton } from '@/components/ActionButton';

import { avatarInitial, shownName } from '../avatar-initial';
import { useLogoutMutation } from '../hooks/useLogoutMutation';
import { useSession } from '../hooks/useSession';

/** Who is signed in, plus the links that depend on it. The "Admin" link is a convenience: the API enforces the role. */
export function AccountBar() {
  const session = useSession();
  const logout = useLogoutMutation();

  if (session.status === 'restoring') {
    return null;
  }

  if (!session.user) {
    return (
      <View className="flex-row flex-wrap items-center gap-md">
        <Link href="/login" asChild>
          <Text className="min-h-touch py-md text-body text-foreground">Log in</Text>
        </Link>
        <Link href="/register" asChild>
          <Text className="min-h-touch py-md text-body text-foreground">Register</Text>
        </Link>
      </View>
    );
  }

  const name = shownName(session.user);
  const isStaff = session.user.role === 'Admin' || session.user.role === 'Support';
  return (
    <View className="flex-row flex-wrap items-center gap-md">
      <View accessible accessibilityLabel={`Signed in as ${name}`} className="flex-row items-center gap-sm">
        <View className="h-[28px] w-[28px] items-center justify-center rounded-pill bg-primary-subtle">
          <Text className="text-small font-bold text-primary-text">{avatarInitial(session.user)}</Text>
        </View>
        <Text className="text-body text-foreground">{name}</Text>
      </View>
      {isStaff ? (
        <Link href="/admin" asChild>
          <Text className="min-h-touch py-md text-body text-foreground">Admin</Text>
        </Link>
      ) : null}
      <ActionButton label="Log out" onPress={() => logout.mutate()} pending={logout.isPending} />
    </View>
  );
}
