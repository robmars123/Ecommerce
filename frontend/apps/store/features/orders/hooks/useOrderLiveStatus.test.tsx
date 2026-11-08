import { renderHook } from '@testing-library/react-native';

import { subscribeToOrderStatus } from '@/lib/realtime';
import { createTestQueryClient, queryWrapper } from '@/tests/render-with-query';

import { useOrderLiveStatus } from './useOrderLiveStatus';

const subscribe = jest.mocked(subscribeToOrderStatus);

afterEach(() => {
  subscribe.mockClear();
});

function listenerOf(): (orderId: string | null) => void {
  return subscribe.mock.calls[subscribe.mock.calls.length - 1][0];
}

describe('useOrderLiveStatus', () => {
  it('refetches this order and the list when a message names this order', async () => {
    const client = createTestQueryClient();
    const invalidate = jest.spyOn(client, 'invalidateQueries');
    await renderHook(() => useOrderLiveStatus('o-1'), { wrapper: queryWrapper(client) });

    listenerOf()('o-1');

    expect(invalidate).toHaveBeenCalledWith({ queryKey: ['orders', 'detail', 'o-1'] });
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ['orders', 'list'] });
  });

  it('ignores a message about another order', async () => {
    const client = createTestQueryClient();
    const invalidate = jest.spyOn(client, 'invalidateQueries');
    await renderHook(() => useOrderLiveStatus('o-1'), { wrapper: queryWrapper(client) });

    listenerOf()('o-2');

    expect(invalidate).not.toHaveBeenCalled();
  });

  it('refetches after a reconnect (null): anything may have changed while the socket was down', async () => {
    const client = createTestQueryClient();
    const invalidate = jest.spyOn(client, 'invalidateQueries');
    await renderHook(() => useOrderLiveStatus('o-1'), { wrapper: queryWrapper(client) });

    listenerOf()(null);

    expect(invalidate).toHaveBeenCalledWith({ queryKey: ['orders', 'detail', 'o-1'] });
  });

  it('unsubscribes when the screen goes away', async () => {
    const off = jest.fn();
    subscribe.mockReturnValueOnce(off);
    const { unmount } = await renderHook(() => useOrderLiveStatus('o-1'), { wrapper: queryWrapper(createTestQueryClient()) });

    await unmount();

    expect(off).toHaveBeenCalledTimes(1);
  });
});
