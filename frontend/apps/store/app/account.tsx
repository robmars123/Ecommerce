import { AccountView } from '@/features/account/components/AccountView';
import { RequireSignIn } from '@/features/auth/components/RequireSignIn';

export default function AccountScreen() {
  return (
    <RequireSignIn returnTo="/account">
      <AccountView />
    </RequireSignIn>
  );
}
