import { useMutation, useQueryClient } from '@tanstack/react-query';

import { refreshTokenStorage } from '@/lib/platform';

import { logout } from '../api';
import { endSession } from '../session';

/** Tells the API best-effort, then always ends the session locally and drops every cached query. */
export function useLogoutMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async () => {
      try {
        await logout(await refreshTokenStorage.get());
      } catch {
        // The API call is best effort: the local session ends either way.
      }
    },
    onSettled: async () => {
      await endSession();
      queryClient.clear();
    },
  });
}
