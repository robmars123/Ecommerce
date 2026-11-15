import { Redirect } from 'expo-router';
import type { ReactNode } from 'react';

import { LoadingState } from '@/components/LoadingState';
import { useSession } from '@/features/auth/hooks/useSession';

/** Pages for a signed-in user. Signed out goes to the sign-in dialog and comes back here afterwards. */
export function RequireSignIn({ returnTo, children }: { returnTo: string; children: ReactNode }) {
  const session = useSession();

  if (session.status === 'restoring') {
    return <LoadingState />;
  }
  if (!session.user) {
    return <Redirect href={{ pathname: '/login', params: { returnTo } }} />;
  }
  return <>{children}</>;
}
