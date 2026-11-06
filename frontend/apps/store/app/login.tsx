import { FormScreen } from '@/components/FormScreen';
import { LoginForm } from '@/features/auth/components/LoginForm';

export default function LoginScreen() {
  return (
    <FormScreen title="Log in">
      <LoginForm />
    </FormScreen>
  );
}
