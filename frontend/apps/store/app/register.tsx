import { FormScreen } from '@/components/FormScreen';
import { RegisterForm } from '@/features/auth/components/RegisterForm';

export default function RegisterScreen() {
  return (
    <FormScreen title="Register">
      <RegisterForm />
    </FormScreen>
  );
}
