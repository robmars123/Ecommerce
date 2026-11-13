import { HubConnectionBuilder, LogLevel, type HubConnection } from '@microsoft/signalr';

import { getCurrentAccessToken } from './api-client';
import { getApiBaseUrl } from './config';

/** Called with the order that changed, or null after a reconnect (anything may have changed while the socket was down). */
export type OrderStatusListener = (orderId: string | null) => void;

const HUB_PATH = '/hubs/orders';
/** Called with true while the socket is connected, false otherwise (connecting, reconnecting, closed, failed). */
export type ConnectionStateListener = (connected: boolean) => void;

const listeners = new Set<OrderStatusListener>();
const stateListeners = new Set<ConnectionStateListener>();
let connection: HubConnection | undefined;
let connected = false;

function setConnected(value: boolean): void {
  if (connected === value) {
    return;
  }
  connected = value;
  stateListeners.forEach((listener) => listener(value));
}

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
  const built = new HubConnectionBuilder()
    .withUrl(`${base}${HUB_PATH}`, { accessTokenFactory: () => getCurrentAccessToken() ?? '', withCredentials: false })
    .withAutomaticReconnect()
    .configureLogging(LogLevel.None)
    .build();
  connection = built;
  built.on('orderStatusChanged', (message: { orderId?: string }) => notify(message?.orderId ?? null));
  // stop() of an old socket fires its events after a new one may be up: only the current connection may change the state.
  const whenCurrent = (action: () => void) => () => {
    if (connection === built) {
      action();
    }
  };
  built.onreconnected(
    whenCurrent(() => {
      setConnected(true);
      notify(null);
    }),
  );
  built.onreconnecting(whenCurrent(() => setConnected(false)));
  built.onclose(whenCurrent(() => setConnected(false)));
  // A hint only (arch 4.13): if the socket cannot connect the screen is still correct after a refetch.
  built.start().then(
    whenCurrent(() => setConnected(true)),
    whenCurrent(() => setConnected(false)),
  );
}

function stop(): void {
  const current = connection;
  connection = undefined;
  connected = false;
  void current?.stop();
}

function releaseIfUnused(): void {
  if (listeners.size === 0 && stateListeners.size === 0) {
    stop();
  }
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
    releaseIfUnused();
  };
}

/** Joins the same shared connection; reports whether the socket is connected now and on every change. */
export function subscribeToConnectionState(listener: ConnectionStateListener): () => void {
  stateListeners.add(listener);
  start();
  listener(connected);
  return () => {
    stateListeners.delete(listener);
    releaseIfUnused();
  };
}
