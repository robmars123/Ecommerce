import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render } from '@testing-library/react-native';
import { Stack } from 'expo-router';
import { renderRouter } from 'expo-router/testing-library';
import type { ComponentType, ReactElement, ReactNode } from 'react';

import { shouldRetry } from '@/lib/query-client';

/**
 * A fresh client per test. gcTime Infinity keeps no timers alive. Retries are off unless the test is about the
 * real retry policy, in which case pass `useRealRetryPolicy`.
 */
export function createTestQueryClient(options: { useRealRetryPolicy?: boolean } = {}) {
  return new QueryClient({
    defaultOptions: { queries: { retry: options.useRealRetryPolicy ? shouldRetry : false, gcTime: Infinity } },
  });
}

export function renderWithQuery(ui: ReactElement, client: QueryClient = createTestQueryClient()) {
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
}

export function queryWrapper(client: QueryClient) {
  return function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={client}>{children}</QueryClientProvider>;
  };
}

/** Renders routes under a fresh QueryClient and a header-less Stack (the routes under test render their own headers). */
export function renderRouterWithQuery(
  routes: Record<string, ComponentType>,
  options: { initialUrl: string; client?: QueryClient },
) {
  const client = options.client ?? createTestQueryClient();
  const layout = () => (
    <QueryClientProvider client={client}>
      <Stack screenOptions={{ headerShown: false }} />
    </QueryClientProvider>
  );
  return renderRouter({ _layout: layout, ...routes }, { initialUrl: options.initialUrl });
}
