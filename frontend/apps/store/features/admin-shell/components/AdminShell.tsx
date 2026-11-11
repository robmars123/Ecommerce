import { Link } from 'expo-router';
import { useState, type ReactNode } from 'react';
import { Pressable, ScrollView, Text, useWindowDimensions, View } from 'react-native';

import { useSession } from '@/features/auth/hooks/useSession';
import { visibleNav, type NavGroup } from '@/features/admin-shell/nav';
import { sidebarBreakpoint } from '@/lib/design-tokens';

type AdminShellProps = { children: ReactNode };

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

type SidebarProps = { groups: NavGroup[]; onNavigate?: () => void; onClose?: () => void };

function Sidebar({ groups, onNavigate, onClose }: SidebarProps) {
  return (
    <View accessibilityLabel="Admin navigation" className="w-sidebar max-w-full gap-xl bg-surface p-lg">
      <View className="flex-row items-center justify-between gap-md">
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
      <ScrollView contentContainerClassName="gap-lg">
        {groups.map((group) => (
          <View key={group.title} className="gap-xs">
            <Text accessibilityRole="header" className="px-md text-caption text-muted">
              {group.title}
            </Text>
            {group.items.map((item) =>
              item.ready ? (
                <Link key={item.label} href={item.href as never} asChild>
                  <Pressable
                    accessibilityRole="link"
                    onPress={onNavigate}
                    className="min-h-touch min-w-touch justify-center rounded-control px-md"
                  >
                    <Text className="text-body text-foreground">{item.label}</Text>
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
              ),
            )}
          </View>
        ))}
      </ScrollView>
    </View>
  );
}

/** Frames the admin Stack: fixed sidebar at tablet/desktop width, a top bar with a menu overlay on phones. */
export function AdminShell({ children }: AdminShellProps) {
  const { width } = useWindowDimensions();
  const { user } = useSession();
  const groups = visibleNav(user?.role ?? null);
  const [isOpen, setOpen] = useState(false);
  const close = () => setOpen(false);

  if (width >= sidebarBreakpoint) {
    return (
      <View className="flex-1 flex-row bg-background">
        <View className="border-r border-border bg-surface">
          <Sidebar groups={groups} />
        </View>
        <View className="flex-1">{children}</View>
      </View>
    );
  }

  return (
    <View className="flex-1 bg-background">
      <View className="flex-row items-center gap-md border-b border-border bg-surface px-lg">
        <Pressable
          accessibilityRole="button"
          accessibilityLabel="Open menu"
          accessibilityState={{ expanded: isOpen }}
          onPress={() => setOpen(true)}
          className="min-h-touch min-w-touch items-center justify-center"
        >
          <Text className="text-body text-foreground">Menu</Text>
        </Pressable>
        <Text className="text-body font-bold text-foreground">Store Admin</Text>
      </View>
      <View className="flex-1">{children}</View>
      {isOpen ? (
        <View className="absolute inset-0 flex-row">
          <Sidebar groups={groups} onNavigate={close} onClose={close} />
          <Pressable accessibilityRole="button" accessibilityLabel="Close menu" onPress={close} className="flex-1 bg-scrim" />
        </View>
      ) : null}
    </View>
  );
}
