import { useMutation } from '@tanstack/react-query';

import { login } from '../api';
import { startSession } from '../session';

export function useLoginMutation() {
  return useMutation({ mutationFn: login, onSuccess: startSession });
}
