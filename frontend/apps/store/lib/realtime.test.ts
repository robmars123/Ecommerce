jest.unmock('@/lib/realtime');

import { setAuthHandlers } from './api-client';

type Handler = (...args: unknown[]) => void;

let mockStartFails = false;

const created: {
  url: string;
  options: { accessTokenFactory: () => string; withCredentials: boolean };
  handlers: Record<string, Handler>;
  reconnected?: Handler;
  start: jest.Mock;
  stop: jest.Mock;
  logLevel?: unknown;
  reconnect: boolean;
}[] = [];

jest.mock('@microsoft/signalr', () => ({
  LogLevel: { None: 6 },
  HubConnectionBuilder: class {
    private state: (typeof created)[number] = {
      url: '',
      options: { accessTokenFactory: () => '', withCredentials: true },
      handlers: {},
      start: jest.fn(() => (mockStartFails ? Promise.reject(new Error('offline')) : Promise.resolve())),
      stop: jest.fn(() => Promise.resolve()),
      reconnect: false,
    };
    withUrl(url: string, options: (typeof created)[number]['options']) {
      this.state.url = url;
      this.state.options = options;
      return this;
    }
    withAutomaticReconnect() {
      this.state.reconnect = true;
      return this;
    }
    configureLogging(level: unknown) {
      this.state.logLevel = level;
      return this;
    }
    build() {
      const state = this.state;
      created.push(state);
      return {
        on: (name: string, handler: Handler) => {
          state.handlers[name] = handler;
        },
        onreconnected: (handler: Handler) => {
          state.reconnected = handler;
        },
        start: state.start,
        stop: state.stop,
      };
    }
  },
}));

// eslint-disable-next-line import/first
import { subscribeToOrderStatus } from './realtime';

beforeEach(() => {
  created.length = 0;
  mockStartFails = false;
  process.env.EXPO_PUBLIC_API_BASE_URL = 'http://api.test.invalid:5080';
  setAuthHandlers({ getAccessToken: () => 'tok-1', refresh: async () => true });
});

afterEach(() => {
  setAuthHandlers(undefined);
  delete process.env.EXPO_PUBLIC_API_BASE_URL;
});

describe('subscribeToOrderStatus', () => {
  it('opens one connection to the hub with the access token factory, no client logging and automatic reconnect', () => {
    const off = subscribeToOrderStatus(jest.fn());

    expect(created).toHaveLength(1);
    expect(created[0].url).toBe('http://api.test.invalid:5080/hubs/orders');
    expect(created[0].options.accessTokenFactory()).toBe('tok-1');
    expect(created[0].options.withCredentials).toBe(false);
    expect(created[0].logLevel).toBe(6);
    expect(created[0].reconnect).toBe(true);
    expect(created[0].start).toHaveBeenCalledTimes(1);
    off();
  });

  it('gives an empty token when signed out and shares one connection between subscribers', () => {
    setAuthHandlers(undefined);
    const offA = subscribeToOrderStatus(jest.fn());
    const offB = subscribeToOrderStatus(jest.fn());

    expect(created).toHaveLength(1);
    expect(created[0].options.accessTokenFactory()).toBe('');
    offA();
    expect(created[0].stop).not.toHaveBeenCalled();
    offB();
    expect(created[0].stop).toHaveBeenCalledTimes(1);
  });

  it('passes the order id of a status message to every listener and null after a reconnect', () => {
    const a = jest.fn();
    const b = jest.fn();
    const offA = subscribeToOrderStatus(a);
    const offB = subscribeToOrderStatus(b);

    created[0].handlers.orderStatusChanged({ orderId: 'o-1', status: 'Shipped' });
    created[0].handlers.orderStatusChanged({});
    created[0].reconnected?.();

    expect(a.mock.calls).toEqual([['o-1'], [null], [null]]);
    expect(b).toHaveBeenCalledTimes(3);
    offA();
    created[0].handlers.orderStatusChanged({ orderId: 'o-2' });
    expect(a).toHaveBeenCalledTimes(3);
    expect(b).toHaveBeenLastCalledWith('o-2');
    offB();
  });

  it('opens a new connection after the last subscriber left', () => {
    subscribeToOrderStatus(jest.fn())();
    const off = subscribeToOrderStatus(jest.fn());

    expect(created).toHaveLength(2);
    off();
  });

  it('does nothing without an API base URL', () => {
    delete process.env.EXPO_PUBLIC_API_BASE_URL;

    const off = subscribeToOrderStatus(jest.fn());

    expect(created).toHaveLength(0);
    off();
  });

  it('swallows a failed start: the screen is still correct after a refetch', async () => {
    mockStartFails = true;

    const off = subscribeToOrderStatus(jest.fn());
    await Promise.resolve();

    expect(created[0].start).toHaveBeenCalledTimes(1);
    off();
  });
});
