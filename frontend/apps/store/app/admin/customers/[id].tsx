import { useLocalSearchParams } from 'expo-router';
import { ScreenStub } from '@/components/ScreenStub';

export default function AdminCustomerDetailScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  return <ScreenStub title="Admin · Customer" detail={id} />;
}
