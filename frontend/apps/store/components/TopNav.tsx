import { Link, usePathname, type Href } from 'expo-router';
import { Pressable, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { AccountBar } from '@/features/auth/components/AccountBar';

type NavLinkProps = { href: Href; label: string; selected?: boolean; outlined?: boolean };

function NavLink({ href, label, selected = false, outlined = false }: NavLinkProps) {
  const shape = outlined
    ? 'border border-border'
    : selected
      ? 'bg-primary-subtle'
      : '';
  return (
    <Link href={href} asChild>
      <Pressable
        accessibilityRole="link"
        accessibilityState={{ selected }}
        className={`min-h-touch min-w-touch items-center justify-center rounded-control px-md ${shape}`}
      >
        <Text className={`text-body ${selected ? 'font-semibold text-primary-text' : 'text-foreground'}`}>{label}</Text>
      </Pressable>
    </Link>
  );
}

/** The store's top bar: brand, Shop, Orders, the account links and Cart. Mounted as the root Stack header (not on admin). */
export function TopNav() {
  const pathname = usePathname();
  const insets = useSafeAreaInsets();

  return (
    // The safe-area inset is a runtime value: an allowed style exception (arch §5.5).
    <View className="border-b border-border bg-surface" style={{ paddingTop: insets.top }}>
      <View className="w-full max-w-content flex-row flex-wrap items-center justify-between gap-md self-center px-lg py-sm">
        <View className="flex-row flex-wrap items-center gap-sm">
          <Link href="/" asChild>
            <Pressable accessibilityRole="link" className="min-h-touch min-w-touch justify-center pr-md">
              <Text className="text-title font-bold text-foreground">Northline.</Text>
            </Pressable>
          </Link>
          <NavLink href="/products" label="Shop" selected={pathname.startsWith('/products')} />
          <NavLink href="/orders" label="Orders" selected={pathname.startsWith('/orders')} />
        </View>
        <View className="flex-row flex-wrap items-center gap-md">
          <AccountBar />
          <NavLink href="/cart" label="Cart" selected={pathname.startsWith('/cart')} outlined />
        </View>
      </View>
    </View>
  );
}
