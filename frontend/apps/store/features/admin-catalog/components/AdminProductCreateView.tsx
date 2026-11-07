import { useRouter } from 'expo-router';
import { ScrollView } from 'react-native';

import { ScreenTitle } from '@/components/ScreenTitle';

import { emptyFormState, toCreateRequest } from '../form-model';
import { useCreateProductMutation } from '../hooks/useCreateProductMutation';
import { ProductForm } from './ProductForm';

export function AdminProductCreateView() {
  const router = useRouter();
  const create = useCreateProductMutation();

  return (
    <ScrollView contentContainerClassName="gap-md p-lg">
      <ScreenTitle title="Admin · New product" />
      <ProductForm
        mode="create"
        initial={emptyFormState()}
        onSubmit={(state) =>
          create.mutate(toCreateRequest(state), {
            onSuccess: (product) => router.replace({ pathname: '/admin/products/[id]', params: { id: product.id } }),
          })
        }
        pending={create.isPending}
        error={create.error}
      />
    </ScrollView>
  );
}
