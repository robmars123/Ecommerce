import { Redirect, usePathname } from 'expo-router';
import type { ReactNode } from 'react';

import { LoadingState } from '@/components/LoadingState';

import { adminRouteDecision } from '../admin-access';
import { useSession } from '../hooks/useSession';

/**
 * Hides /admin pages from people who cannot use them. Only the UI: every admin API call is checked by the server.
 * While the session is still being restored nothing renders and nothing is fetched, so a page refresh does not bounce
 * a signed-in admin to the login screen.
 */
export function AdminGate({ children }: { children: ReactNode }) {
  const session = useSession();
  const pathname = usePathname();

  if (session.status === 'restoring') {
    return <LoadingState />;
  }
  const redirectTo = adminRouteDecision(session.user?.role ?? null, pathname);
  return redirectTo === null ? <>{children}</> : <Redirect href={redirectTo as never} />;
}
