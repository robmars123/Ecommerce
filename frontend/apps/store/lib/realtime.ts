import { HubConnectionBuilder, LogLevel, type HubConnection } from '@microsoft/signalr';

import { getCurrentAccessToken } from './api-client';
import { getApiBaseUrl } from './config';

/** Called with the order that changed, or null after a reconnect (anything may have changed while the socket was down). */
export type OrderStatusListener = (orderId: string | null) => void;

const HUB_PATH = '/hubs/orders';
const listeners = new Set<OrderStatusListener>();
let connection: HubConnection | undefined;

function notify(orderId: string | null): void {
  listeners.forEach((listener) => listener(orderId));
}

function start(): void {
  const base = getApiBaseUrl();
  if (!base || connection) {
    return;
  }
  // The access token travels in the query string of a hub request only (browsers cannot set headers on a socket), so the
  // client logger stays off: its connection lines would print the URL (arch 10.1, no token in logs).
  connection = new HubConnectionBuilder()
    .withUrl(`${base}${HUB_PATH}`, { accessTokenFactory: () => getCurrentAccessToken() ?? '', withCredentials: false })
    .withAutomaticReconnect()
    .configureLogging(LogLevel.None)
    .build();
  connection.on('orderStatusChanged', (message: { orderId?: string }) => notify(message?.orderId ?? null));
  connection.onreconnected(() => notify(null));
  // A hint only (arch 4.13): if the socket cannot connect the screen is still correct after a refetch.
  connection.start().catch(() => undefined);
}

function stop(): void {
  const current = connection;
  connection = undefined;
  void current?.stop();
}

/**
 * One shared connection for every screen: the first subscriber opens it, the last one to leave closes it. Live status is a
 * hint to refetch, never the source of truth. Returns the unsubscribe function.
 */
export function subscribeToOrderStatus(listener: OrderStatusListener): () => void {
  listeners.add(listener);
  start();
  return () => {
    listeners.delete(listener);
    if (listeners.size === 0) {
      stop();
    }
  };
}
