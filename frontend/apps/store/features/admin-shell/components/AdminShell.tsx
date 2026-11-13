import { Link, usePathname } from 'expo-router';
import { useState, type ReactNode } from 'react';
import { Pressable, ScrollView, Text, useWindowDimensions, View } from 'react-native';

import { useSession } from '@/features/auth/hooks/useSession';
import type { SessionUser } from '@/features/auth/types';
import { AdminSearch } from '@/features/admin-shell/components/AdminSearch';
import { breadcrumbFor } from '@/features/admin-shell/breadcrumb';
import type { AttentionItem, AttentionState } from '@/features/admin-shell/attention';
import type { NavBadges } from '@/features/admin-shell/badges';
import { visibleNav, type NavGroup } from '@/features/admin-shell/nav';
import { AdminPanel } from '@/components/AdminPanel';
import { ThemeToggle } from '@/components/ThemeToggle';
import { sidebarBreakpoint } from '@/lib/design-tokens';

type AdminShellProps = { children: ReactNode; badges?: NavBadges; attention?: AttentionState };

function Brand() {
  return (
    <View className="flex-row items-center gap-md">
      <View className="items-center justify-center rounded-control bg-primary px-md py-xs">
        <Text className="text-title font-bold text-primary-foreground">S</Text>
      </View>
      <View>
        <Text className="text-body font-bold text-foreground">Store Admin</Text>
        <Text className="text-caption text-muted">Commerce Suite</Text>
      </View>
    </View>
  );
}

function SidebarFooter({ user }: { user: SessionUser }) {
  const name = user.displayName.trim() || user.email;
  return (
    <View accessible accessibilityLabel={`Signed in as ${name}, ${user.role}`} className="border-t border-border pt-md">
      <Text numberOfLines={1} className="text-body font-bold text-foreground">
        {name}
      </Text>
      <Text numberOfLines={1} className="text-caption text-muted">
        {user.role}
      </Text>
    </View>
  );
}

const toneClass = { warning: 'text-warning', danger: 'text-danger' } as const;

function AttentionRow({ item, onNavigate }: { item: AttentionItem; onNavigate?: () => void }) {
  const text = <Text className={`text-body ${item.tone ? toneClass[item.tone] : 'text-foreground'}`}>{item.text}</Text>;
  if (!item.href) return text;
  return (
    <Link href={item.href as never} asChild>
      <Pressable accessibilityRole="link" onPress={onNavigate} className="min-h-touch justify-center">
        {text}
      </Pressable>
    </Link>
  );
}

function AttentionCard({ attention, onNavigate }: { attention: AttentionState; onNavigate?: () => void }) {
  return (
    <AdminPanel title="Needs attention">
      {attention.kind === 'unavailable' ? (
        <Text className="text-body text-muted">Alerts unavailable right now.</Text>
      ) : attention.items.length === 0 ? (
        <Text className="text-body text-muted">All clear.</Text>
      ) : (
        attention.items.map((item) => <AttentionRow key={item.text} item={item} onNavigate={onNavigate} />)
      )}
    </AdminPanel>
  );
}

function Breadcrumb() {
  const crumbs = breadcrumbFor(usePathname());
  if (crumbs.length === 0) return null;
  return (
    <View accessibilityLabel="Breadcrumb" className="flex-row flex-wrap items-center gap-xs">
      {crumbs.map((crumb, index) => {
        const isLast = index === crumbs.length - 1;
        return (
          <View key={crumb.href} className="flex-row items-center gap-xs">
            {index > 0 ? (
              <Text accessibilityElementsHidden importantForAccessibility="no-hide-descendants" className="text-caption text-muted">
                ›
              </Text>
            ) : null}
            {isLast ? (
              <Text numberOfLines={1} className="text-body font-bold text-foreground">
                {crumb.label}
              </Text>
            ) : (
              <Link href={crumb.href as never} asChild>
                <Pressable accessibilityRole="link" className="min-h-touch justify-center">
                  <Text className="text-body text-muted">{crumb.label}</Text>
                </Pressable>
              </Link>
            )}
          </View>
        );
      })}
    </View>
  );
}

type SidebarProps = {
  groups: NavGroup[];
  user: SessionUser | null;
  badges?: NavBadges;
  attention?: AttentionState;
  onNavigate?: () => void;
  onClose?: () => void;
};

function Sidebar({ groups, user, badges, attention, onNavigate, onClose }: SidebarProps) {
  return (
    <View accessibilityLabel="Admin navigation" className="h-full w-sidebar max-w-full gap-xl bg-surface p-lg">
      <View className="flex-row flex-wrap items-center justify-between gap-md">
        <Brand />
        {onClose ? (
          <Pressable
            accessibilityRole="button"
            accessibilityLabel="Close menu"
            onPress={onClose}
            className="min-h-touch min-w-touch items-center justify-center"
          >
            <Text className="text-body text-foreground">Close</Text>
          </Pressable>
        ) : null}
      </View>
      <ScrollView className="flex-1" contentContainerClassName="gap-lg">
        {groups.map((group) => (
          <View key={group.title} className="gap-xs">
            <Text accessibilityRole="header" className="px-md text-caption text-muted">
              {group.title}
            </Text>
            {group.items.map((item) => {
              const badge = badges?.[item.href];
              return item.ready ? (
                <Link key={item.label} href={item.href as never} asChild>
                  <Pressable
                    accessibilityRole="link"
                    {...(badge ? { accessibilityLabel: `${item.label}, ${badge}` } : {})}
                    onPress={onNavigate}
                    className="min-h-touch min-w-touch flex-row items-center justify-between gap-md rounded-control px-md"
                  >
                    <Text className="text-body text-foreground">{item.label}</Text>
                    {badge ? <Text className="rounded-control border border-warning px-sm text-caption text-warning">{badge}</Text> : null}
                  </Pressable>
                </Link>
              ) : (
                <Pressable
                  key={item.label}
                  accessibilityLabel={item.label}
                  accessibilityState={{ disabled: true }}
                  disabled
                  className="min-h-touch min-w-touch flex-row items-center justify-between gap-md rounded-control px-md"
                >
                  <Text className="text-body text-muted">{item.label}</Text>
                  <Text className="text-caption text-muted">Soon</Text>
                </Pressable>
              );
            })}
          </View>
        ))}
        {attention ? <AttentionCard attention={attention} onNavigate={onNavigate} /> : null}
      </ScrollView>
      {user ? <SidebarFooter user={user} /> : null}
    </View>
  );
}

/** Frames the admin Stack: fixed sidebar at tablet/desktop width, a top bar with a menu overlay on phones. */
export function AdminShell({ children, badges, attention }: AdminShellProps) {
  const { width } = useWindowDimensions();
  const { user } = useSession();
  const groups = visibleNav(user?.role ?? null);
  const [isOpen, setOpen] = useState(false);
  const close = () => setOpen(false);

  if (width >= sidebarBreakpoint) {
    return (
      <View className="flex-1 flex-row bg-background">
        <View className="border-r border-border bg-surface">
          <Sidebar groups={groups} user={user} badges={badges} attention={attention} />
        </View>
        <View className="flex-1">
          <View className="min-h-touch flex-row items-start justify-between gap-md border-b border-border bg-surface px-lg">
            <View className="min-h-touch flex-1 justify-center">
              <Breadcrumb />
            </View>
            {user ? (
              <View className="w-sidebar max-w-full">
                <AdminSearch role={user.role} />
              </View>
            ) : null}
            <ThemeToggle />
          </View>
          {children}
        </View>
      </View>
    );
  }

  return (
    <View className="flex-1 bg-background">
      <View className="border-b border-border bg-surface px-lg">
        <View className="flex-row items-center gap-md">
          <Pressable
            accessibilityRole="button"
            accessibilityLabel="Open menu"
            accessibilityState={{ expanded: isOpen }}
            onPress={() => setOpen(true)}
            className="min-h-touch min-w-touch items-center justify-center"
          >
            <Text className="text-body text-foreground">Menu</Text>
          </Pressable>
          <Text className="flex-1 text-body font-bold text-foreground">Store Admin</Text>
          <ThemeToggle />
        </View>
        <Breadcrumb />
        {user ? <AdminSearch role={user.role} /> : null}
      </View>
      <View className="flex-1">{children}</View>
      {isOpen ? (
        <View className="absolute inset-0 flex-row">
          <Sidebar groups={groups} user={user} badges={badges} attention={attention} onNavigate={close} onClose={close} />
          <Pressable accessibilityRole="button" accessibilityLabel="Close menu" onPress={close} className="flex-1 bg-scrim" />
        </View>
      ) : null}
    </View>
  );
}
