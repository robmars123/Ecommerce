import { ApiError, REQUEST_TIMEOUT_MS, apiGet, apiSend, setAuthHandlers } from './api-client';
import { createQueryClient } from './query-client';
import { jsonResponse, problemResponse } from '@/tests/fetch-mock';

// F2b: bearer tokens, the single refresh-and-retry, write requests, and what an ApiError may hold.

const SECRET = 'secret-access-token-value';

function headerOf(call: unknown[], name: string): string | undefined {
  const init = call[1] as RequestInit;
  return (init.headers as Record<string, string>)[name];
}

let token: string | undefined;
let refresh: jest.Mock<Promise<boolean>, []>;

beforeEach(() => {
  process.env.EXPO_PUBLIC_API_BASE_URL = 'http://api.test.invalid:5080';
  token = SECRET;
  refresh = jest.fn(async () => {
    token = 'fresh-token';
    return true;
  });
  setAuthHandlers({ getAccessToken: () => token, refresh });
});

afterEach(() => {
  jest.restoreAllMocks();
  jest.useRealTimers();
  setAuthHandlers(undefined);
  delete process.env.EXPO_PUBLIC_API_BASE_URL;
});

async function catchError(promise: Promise<unknown>): Promise<ApiError> {
  try {
    await promise;
  } catch (error) {
    return error as ApiError;
  }
  throw new Error('expected a rejection');
}

describe('bearer token', () => {
  it('is sent by default and not sent with auth: none', async () => {
    const spy = jest.spyOn(globalThis, 'fetch').mockImplementation(async () => jsonResponse(200, {}));

    await apiGet('/a');
    await apiGet('/b', { auth: 'none' });

    expect(headerOf(spy.mock.calls[0], 'Authorization')).toBe(`Bearer ${SECRET}`);
    expect(headerOf(spy.mock.calls[1], 'Authorization')).toBeUndefined();
  });

  it('is not sent when there is no token, and a 401 then does not refresh', async () => {
    token = undefined;
    const spy = jest.spyOn(globalThis, 'fetch').mockImplementation(async () => problemResponse(401, {}));

    const error = await catchError(apiGet('/a'));

    expect(error.kind).toBe('unauthorized');
    expect(refresh).not.toHaveBeenCalled();
    expect(spy).toHaveBeenCalledTimes(1);
  });
});

describe('401 handling', () => {
  it('refreshes once and retries once with the new token', async () => {
    const spy = jest
      .spyOn(globalThis, 'fetch')
      .mockImplementation(async (_url, init) =>
        (init!.headers as Record<string, string>).Authorization === 'Bearer fresh-token'
          ? jsonResponse(200, { ok: true })
          : problemResponse(401, {}),
      );

    await expect(apiGet('/a')).resolves.toEqual({ ok: true });

    expect(refresh).toHaveBeenCalledTimes(1);
    expect(spy).toHaveBeenCalledTimes(2);
  });

  it('retries each of three parallel 401s exactly once', async () => {
    const spy = jest
      .spyOn(globalThis, 'fetch')
      .mockImplementation(async (_url, init) =>
        (init!.headers as Record<string, string>).Authorization === 'Bearer fresh-token'
          ? jsonResponse(200, { ok: true })
          : problemResponse(401, {}),
      );

    const results = await Promise.all([apiGet('/a'), apiGet('/b'), apiGet('/c')]);

    expect(results).toHaveLength(3);
    expect(spy).toHaveBeenCalledTimes(6);
  });

  it('is unauthorized when the refresh fails, with no retry', async () => {
    refresh.mockResolvedValue(false);
    const spy = jest.spyOn(globalThis, 'fetch').mockImplementation(async () => problemResponse(401, {}));

    expect((await catchError(apiGet('/a'))).kind).toBe('unauthorized');
    expect(spy).toHaveBeenCalledTimes(1);
  });

  it('does not retry a second time when the retry is also 401', async () => {
    const spy = jest.spyOn(globalThis, 'fetch').mockImplementation(async () => problemResponse(401, {}));

    expect((await catchError(apiGet('/a'))).kind).toBe('unauthorized');
    expect(refresh).toHaveBeenCalledTimes(1);
    expect(spy).toHaveBeenCalledTimes(2);
  });
});

describe('apiSend', () => {
  it('sends a JSON body and surfaces 403, and 409 with field errors', async () => {
    const spy = jest.spyOn(globalThis, 'fetch').mockImplementation(async () => jsonResponse(200, { id: 1 }));
    await apiSend('POST', '/x', { body: { name: 'a' } });
    const init = spy.mock.calls[0][1] as RequestInit;
    expect(init.method).toBe('POST');
    expect(init.body).toBe('{"name":"a"}');
    expect(headerOf(spy.mock.calls[0], 'Content-Type')).toBe('application/json');

    spy.mockImplementationOnce(async () => problemResponse(403, {}));
    expect((await catchError(apiSend('PUT', '/x', { body: {} }))).kind).toBe('forbidden');

    spy.mockImplementationOnce(async () => problemResponse(409, { errors: { version: ['changed'] } }));
    const conflict = await catchError(apiSend('PUT', '/x', { body: {} }));
    expect(conflict.kind).toBe('conflict');
    expect(conflict.fieldErrors).toEqual({ version: ['changed'] });
  });

  it('adds the cookie transport header and credentials only when asked', async () => {
    const spy = jest.spyOn(globalThis, 'fetch').mockImplementation(async () => jsonResponse(200, {}));

    await apiSend('POST', '/a', { body: {}, refreshTransport: 'cookie' });
    await apiSend('POST', '/b', { body: {}, refreshTransport: 'body' });
    await apiSend('POST', '/c', { body: {} });

    expect(headerOf(spy.mock.calls[0], 'X-Auth-Transport')).toBe('cookie');
    expect((spy.mock.calls[0][1] as RequestInit).credentials).toBe('include');
    for (const i of [1, 2]) {
      expect(headerOf(spy.mock.calls[i], 'X-Auth-Transport')).toBeUndefined();
      expect((spy.mock.calls[i][1] as RequestInit).credentials).toBeUndefined();
    }
  });

  it('treats an empty 204 body as success', async () => {
    jest.spyOn(globalThis, 'fetch').mockImplementation(async () => new Response(null, { status: 204 }));

    await expect(apiSend('POST', '/x', { body: {} })).resolves.toEqual({});
  });
});

describe('secrecy', () => {
  it('never writes to the console and never leaves the token in an error', async () => {
    const consoleSpies = (['log', 'info', 'warn', 'error', 'debug'] as const).map((m) => jest.spyOn(console, m));
    refresh.mockResolvedValue(false);
    const spy = jest.spyOn(globalThis, 'fetch');
    const responses: Array<() => Promise<Response>> = [
      async () => problemResponse(401, {}),
      async () => problemResponse(403, {}),
      async () => problemResponse(500, { title: 'x' }),
      async () => {
        throw new TypeError('Network request failed');
      },
    ];

    const errors: ApiError[] = [];
    for (const respond of responses) {
      spy.mockImplementationOnce(respond);
      errors.push(await catchError(apiGet('/a')));
    }
    jest.useFakeTimers();
    spy.mockImplementationOnce(
      (_url, init) => new Promise((_, reject) => init!.signal!.addEventListener('abort', () => reject(new Error('aborted')))),
    );
    const pending = catchError(apiGet('/a'));
    await jest.advanceTimersByTimeAsync(REQUEST_TIMEOUT_MS);
    errors.push(await pending);

    expect(errors.map((e) => e.kind)).toEqual(['unauthorized', 'forbidden', 'server', 'network', 'timeout']);
    for (const error of errors) {
      expect(error.message).not.toContain(SECRET);
      expect(String(error)).not.toContain(SECRET);
      expect(JSON.stringify(error)).not.toContain(SECRET);
    }
    consoleSpies.forEach((s) => expect(s).not.toHaveBeenCalled());
  });
});

describe('carry-ins (D23)', () => {
  it('does not call fetch when the signal is already aborted', async () => {
    const spy = jest.spyOn(globalThis, 'fetch');
    const controller = new AbortController();
    controller.abort();

    await expect(apiGet('/a', { signal: controller.signal })).rejects.toBeDefined();

    expect(spy).not.toHaveBeenCalled();
  });

  it('is a timeout when the body read is what takes too long', async () => {
    jest.useFakeTimers();
    jest.spyOn(globalThis, 'fetch').mockImplementation(async (_url, init) => {
      const slow = {
        ok: true,
        status: 200,
        headers: new Headers(),
        text: () => new Promise<string>((_, reject) => init!.signal!.addEventListener('abort', () => reject(new Error('aborted')))),
      };
      return slow as unknown as Response;
    });

    const pending = catchError(apiGet('/a'));
    await jest.advanceTimersByTimeAsync(REQUEST_TIMEOUT_MS);

    expect((await pending).kind).toBe('timeout');
  });
});

describe('retry policy (Q-1)', () => {
  it.each(['unauthorized', 'forbidden', 'conflict'] as const)('never retries %s', (kind) => {
    const retry = createQueryClient().getDefaultOptions().queries!.retry as (n: number, e: unknown) => boolean;
    expect(retry(0, new ApiError(kind, ''))).toBe(false);
  });
});
