import { Link } from 'expo-router';
import { useState, type ReactNode } from 'react';
import { Pressable, Text, useWindowDimensions, View } from 'react-native';

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

function Sidebar({ onNavigate, onClose }: { onNavigate?: () => void; onClose?: () => void }) {
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
      <Link href="/admin" asChild>
        <Pressable
          accessibilityRole="link"
          onPress={onNavigate}
          className="min-h-touch min-w-touch justify-center rounded-control px-md"
        >
          <Text className="text-body text-foreground">Dashboard</Text>
        </Pressable>
      </Link>
    </View>
  );
}

/** Frames the admin Stack: fixed sidebar at tablet/desktop width, a top bar with a menu overlay on phones. */
export function AdminShell({ children }: AdminShellProps) {
  const { width } = useWindowDimensions();
  const [isOpen, setOpen] = useState(false);
  const close = () => setOpen(false);

  if (width >= sidebarBreakpoint) {
    return (
      <View className="flex-1 flex-row bg-background">
        <View className="border-r border-border bg-surface">
          <Sidebar />
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
          <Sidebar onNavigate={close} onClose={close} />
          <Pressable accessibilityRole="button" accessibilityLabel="Close menu" onPress={close} className="flex-1 bg-scrim" />
        </View>
      ) : null}
    </View>
  );
}
