import { Link, usePathname } from 'expo-router';
import { useState, type ReactNode } from 'react';
import { Pressable, ScrollView, Text, useWindowDimensions, View } from 'react-native';

import { useSession } from '@/features/auth/hooks/useSession';
import type { SessionUser } from '@/features/auth/types';
import { AdminSearch } from '@/features/admin-shell/components/AdminSearch';
import { LiveStorePill } from '@/features/admin-shell/components/LiveStorePill';
import { breadcrumbFor } from '@/features/admin-shell/breadcrumb';
import type { AttentionItem, AttentionState } from '@/features/admin-shell/attention';
import type { NavBadges } from '@/features/admin-shell/badges';
import { visibleNav, type NavGroup } from '@/features/admin-shell/nav';
import { ThemeToggle } from '@/components/ThemeToggle';
import { sidebarBreakpoint } from '@/lib/design-tokens';

type AdminShellProps = { children: ReactNode; badges?: NavBadges; attention?: AttentionState };

function Brand() {
  return (
    <View className="flex-row items-center gap-md">
      <View className="h-[34px] w-[34px] items-center justify-center rounded-nav bg-selected">
        <Text className="text-[15px] font-bold text-selected-foreground">S</Text>
      </View>
      <View>
        <Text className="text-small font-bold text-foreground">Store Admin</Text>
        <Text className="mt-[2px] text-eyebrow font-medium uppercase text-subtle">Commerce Suite</Text>
      </View>
    </View>
  );
}

function SidebarFooter({ user }: { user: SessionUser }) {
  const name = user.displayName.trim() || user.email;
  return (
    <View accessible accessibilityLabel={`Signed in as ${name}, ${user.role}`} className="mt-[14px] border-t border-border pt-[14px]">
      <Text numberOfLines={1} className="text-ui-sm font-bold text-foreground">
        {name}
      </Text>
      <Text numberOfLines={1} className="text-caption text-subtle">
        {user.role}
      </Text>
    </View>
  );
}

const toneClass = { warning: 'text-warning', danger: 'text-danger' } as const;

function AttentionRow({ item, onNavigate }: { item: AttentionItem; onNavigate?: () => void }) {
  const text = <Text className={`text-meta ${item.tone ? toneClass[item.tone] : 'text-muted'}`}>{item.text}</Text>;
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
    <View className="gap-sm rounded-card border border-border bg-background p-md">
      <Text accessibilityRole="header" className="text-ui-sm font-semibold text-foreground">
        Needs attention
      </Text>
      {attention.kind === 'unavailable' ? (
        <Text className="text-meta text-muted">Alerts unavailable right now.</Text>
      ) : attention.items.length === 0 ? (
        <Text className="text-meta text-muted">All clear.</Text>
      ) : (
        attention.items.map((item) => <AttentionRow key={item.text} item={item} onNavigate={onNavigate} />)
      )}
    </View>
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
              <Text accessibilityElementsHidden importantForAccessibility="no-hide-descendants" className="text-ui text-muted">
                ›
              </Text>
            ) : null}
            {isLast ? (
              <Text numberOfLines={1} className="text-ui font-semibold text-foreground">
                {crumb.label}
              </Text>
            ) : (
              <Link href={crumb.href as never} asChild>
                <Pressable accessibilityRole="link" className="min-h-touch justify-center">
                  <Text className="text-ui text-muted">{crumb.label}</Text>
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
    <View accessibilityLabel="Admin navigation" className="h-full w-sidebar max-w-full gap-[6px] bg-surface px-[14px] py-[20px]">
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
      <ScrollView className="flex-1" contentContainerClassName="gap-[6px]">
        {groups.map((group) => (
          <View key={group.title} className="gap-xs">
            <Text accessibilityRole="header" className="px-[10px] pb-[6px] pt-[14px] text-nav-label font-semibold uppercase text-subtle">
              {group.title}
            </Text>
            {group.items.map((item) => {
              const badge = badges?.[item.href];
              const inline = badge && !badge.href ? badge.text : null;
              return (
                <View key={item.label} className="flex-row items-center gap-xs">
                  <Link href={item.href as never} asChild>
                    <Pressable
                      accessibilityRole="link"
                      {...(inline ? { accessibilityLabel: `${item.label}, ${inline}` } : {})}
                      onPress={onNavigate}
                      className="min-h-touch min-w-touch flex-1 flex-row items-center justify-between gap-[10px] rounded-nav border border-transparent px-[10px] hover:bg-background"
                    >
                      <Text className="text-ui font-medium text-muted">{item.label}</Text>
                      {inline ? (
                        <Text className="rounded-pill border border-warning-border bg-warning-subtle px-[7px] py-[2px] text-badge font-bold text-warning">
                          {inline}
                        </Text>
                      ) : null}
                    </Pressable>
                  </Link>
                  {badge?.href ? (
                    <Link href={badge.href as never} asChild>
                      <Pressable
                        accessibilityRole="link"
                        accessibilityLabel={`${badge.text} orders`}
                        onPress={onNavigate}
                        className="min-h-touch min-w-touch items-center justify-center"
                      >
                        <View className="min-h-[20px] min-w-[20px] items-center justify-center rounded-pill bg-danger px-[6px]">
                          <Text className="text-badge font-bold text-danger-foreground">{badge.text}</Text>
                        </View>
                      </Pressable>
                    </Link>
                  ) : null}
                </View>
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
          <View className="min-h-[60px] flex-row items-center justify-between gap-md border-b border-border bg-background px-page">
            <View className="min-h-touch flex-1 justify-center">
              <Breadcrumb />
            </View>
            {user ? (
              <View className="ml-[20px] max-w-[340px] flex-1">
                <AdminSearch role={user.role} />
              </View>
            ) : null}
            {user ? <LiveStorePill /> : null}
            <ThemeToggle />
          </View>
          {children}
        </View>
      </View>
    );
  }

  return (
    <View className="flex-1 bg-background">
      <View className="border-b border-border bg-background px-lg">
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
          <Text className="flex-1 text-small font-bold text-foreground">Store Admin</Text>
          {user ? <LiveStorePill /> : null}
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
