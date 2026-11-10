import { useLocalSearchParams } from 'expo-router';

import { AdminCustomerDetailView } from '@/features/admin-customers/components/AdminCustomerDetailView';

export default function AdminCustomerDetailScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  return <AdminCustomerDetailView id={id} />;
}
