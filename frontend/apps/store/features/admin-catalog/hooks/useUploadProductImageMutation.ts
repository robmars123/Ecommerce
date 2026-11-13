import { useMutation } from '@tanstack/react-query';

import { uploadProductImage } from '../api';

/** No cache invalidation: the product does not change until the admin saves it with the new key. */
export function useUploadProductImageMutation() {
  return useMutation({ mutationFn: ({ id, form }: { id: string; form: FormData }) => uploadProductImage(id, form) });
}
