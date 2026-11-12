import { Link, usePathname, type Href } from 'expo-router';
import { Pressable, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { AccountBar } from '@/features/auth/components/AccountBar';
import { useSession } from '@/features/auth/hooks/useSession';
import { useCartCount } from '@/features/cart/hooks/useCartCount';

type NavLinkProps = { href: Href; label: string; selected?: boolean; outlined?: boolean; badgeCount?: number | null };

function NavLink({ href, label, selected = false, outlined = false, badgeCount = null }: NavLinkProps) {
  const hasBadge = badgeCount !== null && badgeCount > 0;
  const shape = outlined ? 'border border-border' : selected ? 'bg-primary-subtle' : '';
  return (
    <Link href={href} asChild>
      <Pressable
        accessibilityRole="link"
        accessibilityLabel={hasBadge ? `${label}, ${badgeCount} ${badgeCount === 1 ? 'item' : 'items'}` : undefined}
        accessibilityState={{ selected }}
        className={`min-h-touch min-w-touch items-center justify-center rounded-control px-md ${shape}`}
      >
        <Text className={`text-body ${selected ? 'font-semibold text-primary-text' : 'text-foreground'}`}>{label}</Text>
        {hasBadge ? (
          <View className="absolute -right-xs -top-xs min-w-[20px] items-center rounded-pill bg-primary px-xs">
            <Text className="text-small font-semibold text-primary-foreground">{badgeCount > 99 ? '99+' : badgeCount}</Text>
          </View>
        ) : null}
      </Pressable>
    </Link>
  );
}

/** The store's top bar: brand, Shop, Orders, the account links and Cart. Mounted as the root Stack header (not on admin). */
export function TopNav() {
  const pathname = usePathname();
  const insets = useSafeAreaInsets();
  // Orders belong to a signed-in customer; hidden while the session restores so a guest never sees it flash.
  const session = useSession();
  const cartCount = useCartCount();

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
          {session.status === 'signed-in' ? <NavLink href="/orders" label="Orders" selected={pathname.startsWith('/orders')} /> : null}
        </View>
        <View className="flex-row flex-wrap items-center gap-md">
          <AccountBar />
          <NavLink href="/cart" label="Cart" selected={pathname.startsWith('/cart')} outlined badgeCount={cartCount} />
        </View>
      </View>
    </View>
  );
}
