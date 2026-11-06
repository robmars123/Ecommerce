import { QueryClientProvider } from '@tanstack/react-query';
import { Stack } from 'expo-router';
import { StatusBar } from 'expo-status-bar';
import { useEffect, useState } from 'react';

import { restoreSession, setOnSignedOut } from '@/features/auth/session';
import { createQueryClient } from '@/lib/query-client';

export default function RootLayout() {
  const [queryClient] = useState(createQueryClient);

  useEffect(() => {
    // When the session ends, nothing the last user could see may stay cached.
    setOnSignedOut(() => queryClient.clear());
    void restoreSession();
    return () => setOnSignedOut(undefined);
  }, [queryClient]);

  return (
    <QueryClientProvider client={queryClient}>
      <StatusBar style="auto" />
      <Stack>
        {/* The admin area has its own Stack (and header) inside AdminGate. */}
        <Stack.Screen name="admin" options={{ headerShown: false }} />
      </Stack>
    </QueryClientProvider>
  );
}
