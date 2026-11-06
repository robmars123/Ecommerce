import { Link } from 'expo-router';
import { StyleSheet, Text, View } from 'react-native';

import { ActionButton } from '@/components/ActionButton';

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
      <View style={styles.row}>
        <Link href="/login" style={styles.link}>
          Log in
        </Link>
        <Link href="/register" style={styles.link}>
          Register
        </Link>
      </View>
    );
  }

  const isStaff = session.user.role === 'Admin' || session.user.role === 'Support';
  return (
    <View style={styles.row}>
      <Text style={styles.text}>{session.user.displayName}</Text>
      {isStaff ? (
        <Link href="/admin" style={styles.link}>
          Admin
        </Link>
      ) : null}
      <ActionButton label="Log out" onPress={() => logout.mutate()} pending={logout.isPending} />
    </View>
  );
}

const styles = StyleSheet.create({
  row: { flexDirection: 'row', flexWrap: 'wrap', alignItems: 'center', gap: 12 },
  text: { fontSize: 16 },
  link: { minHeight: 44, paddingVertical: 12, fontSize: 16 },
});
