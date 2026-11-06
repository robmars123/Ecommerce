import { useMutation } from '@tanstack/react-query';

import { requestPasswordReset } from '../api';

export function usePasswordResetRequestMutation() {
  return useMutation({ mutationFn: requestPasswordReset });
}
