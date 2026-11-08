import { useQueryClient } from '@tanstack/react-query';
import { useEffect } from 'react';

import { queryKeys } from '@/lib/query-keys';
import { subscribeToOrderStatus } from '@/lib/realtime';

/**
 * A live status message for this order (or a reconnect) refetches the order and the list: the message is only a hint, the
 * server's answer is what the screen shows (arch 4.13). Without a connection nothing changes: the screen is still correct.
 */
export function useOrderLiveStatus(orderId: string): void {
  const client = useQueryClient();
  useEffect(
    () =>
      subscribeToOrderStatus((changed) => {
        if (changed === null || changed === orderId) {
          void client.invalidateQueries({ queryKey: queryKeys.orders.detail(orderId) });
          void client.invalidateQueries({ queryKey: ['orders', 'list'] });
        }
      }),
    [client, orderId],
  );
}
