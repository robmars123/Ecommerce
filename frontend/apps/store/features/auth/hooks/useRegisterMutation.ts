import { useMutation } from '@tanstack/react-query';

import { register } from '../api';
import { startSession } from '../session';

export function useRegisterMutation() {
  return useMutation({ mutationFn: register, onSuccess: startSession });
}
