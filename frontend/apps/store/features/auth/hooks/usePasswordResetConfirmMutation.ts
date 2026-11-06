import { useMutation } from '@tanstack/react-query';

import { confirmPasswordReset } from '../api';

/** Success does not sign in: the server revoked every session. The caller sends the user to log in. */
export function usePasswordResetConfirmMutation() {
  return useMutation({ mutationFn: confirmPasswordReset });
}
