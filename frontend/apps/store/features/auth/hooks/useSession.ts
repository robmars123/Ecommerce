import { useSyncExternalStore } from 'react';

import { getSessionSnapshot, subscribeToSession, type SessionSnapshot } from '../session';

export function useSession(): SessionSnapshot {
  return useSyncExternalStore(subscribeToSession, getSessionSnapshot, getSessionSnapshot);
}
