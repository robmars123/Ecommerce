import { act } from '@testing-library/react-native';

// Captured at import, before a test installs fake timers, so a response body can finish on the real clock.
const realSetTimeout = setTimeout;

/** Under fake timers: lets a mocked fetch, its body read and TanStack's zero-delay notify finish. */
export async function settleFetch(): Promise<void> {
  for (let i = 0; i < 3; i += 1) {
    await act(() => new Promise<void>((resolve) => realSetTimeout(resolve, 10)));
    await act(() => jest.advanceTimersByTimeAsync(0));
  }
}
