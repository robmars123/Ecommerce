import '@/global.css';

import { QueryClientProvider } from '@tanstack/react-query';
import { Stack } from 'expo-router';
import { useEffect, useState } from 'react';

import { ThemeRoot } from '@/components/ThemeRoot';
import { TopNav } from '@/components/TopNav';
import { restoreSession, setOnSignedOut } from '@/features/auth/session';
import { GuestCartMergeEffect } from '@/features/cart/components/GuestCartMergeEffect';
import { MergeResultNotice } from '@/features/cart/components/MergeResultNotice';
import { createQueryClient } from '@/lib/query-client';
import { useThemeColors } from '@/lib/theme';

export default function RootLayout() {
  const [queryClient] = useState(createQueryClient);
  const colors = useThemeColors();

  useEffect(() => {
    // When the session ends, nothing the last user could see may stay cached.
    setOnSignedOut(() => queryClient.clear());
    void restoreSession();
    return () => setOnSignedOut(undefined);
  }, [queryClient]);

  return (
    <QueryClientProvider client={queryClient}>
      <ThemeRoot>
        <GuestCartMergeEffect />
        {/* contentStyle takes a colour value with no className mapping: token-sourced (arch §5.5). */}
        <Stack
          screenOptions={{
            // The merge result sits right under the header, so it shows wherever sign-in returns to.
            header: () => (
              <>
                <TopNav />
                <MergeResultNotice />
              </>
            ),
            contentStyle: { backgroundColor: colors.background },
          }}
        >
          {/* The admin area has its own Stack (and header) inside AdminGate. */}
          <Stack.Screen name="admin" options={{ headerShown: false }} />
          <Stack.Screen
            name="login"
            options={{ presentation: 'transparentModal', headerShown: false, contentStyle: { backgroundColor: 'transparent' } }}
          />
        </Stack>
      </ThemeRoot>
    </QueryClientProvider>
  );
}
