import { ApiError, apiGet, apiSend, userMessage } from './api-client';
import { jsonResponse, mockFetchOnce, problemResponse } from '@/tests/fetch-mock';

const BASE = 'http://api.test.invalid:5080';

beforeEach(() => {
  process.env.EXPO_PUBLIC_API_BASE_URL = BASE;
});

afterEach(() => {
  jest.restoreAllMocks();
  jest.useRealTimers();
  delete process.env.EXPO_PUBLIC_API_BASE_URL;
});

async function catchError(promise: Promise<unknown>): Promise<ApiError> {
  try {
    await promise;
  } catch (e) {
    expect(e).toBeInstanceOf(ApiError);
    return e as ApiError;
  }
  throw new Error('expected apiGet to reject');
}

describe('apiGet request', () => {
  it('sends a 32-hex X-Correlation-ID, unique per call, and asks for JSON', async () => {
    const spy = jest.spyOn(globalThis, 'fetch').mockImplementation(async () => jsonResponse(200, { ok: true }));

    await apiGet('/api/v1/categories');
    await apiGet('/api/v1/categories');

    const headerOf = (i: number) => (spy.mock.calls[i][1]?.headers as Record<string, string>) ?? {};
    expect(headerOf(0)['X-Correlation-ID']).toMatch(/^[a-f0-9]{32}$/);
    expect(headerOf(1)['X-Correlation-ID']).toMatch(/^[a-f0-9]{32}$/);
    expect(headerOf(0)['X-Correlation-ID']).not.toEqual(headerOf(1)['X-Correlation-ID']);
    expect(headerOf(0).Accept).toBe('application/json');
  });

  it('builds base + path + query and omits undefined and empty params', async () => {
    const spy = jest.spyOn(globalThis, 'fetch').mockImplementation(async () => jsonResponse(200, {}));

    await apiGet('/api/v1/products', {
      params: { search: 'blue shirt', category: undefined, sort: '', page: 2, pageSize: 20 },
    });

    expect(String(spy.mock.calls[0][0])).toBe(`${BASE}/api/v1/products?search=blue%20shirt&page=2&pageSize=20`);
  });

  it('adds no query string when every param is omitted', async () => {
    const spy = jest.spyOn(globalThis, 'fetch').mockImplementation(async () => jsonResponse(200, {}));

    await apiGet('/api/v1/products', { params: { search: undefined } });

    expect(String(spy.mock.calls[0][0])).toBe(`${BASE}/api/v1/products`);
  });

  it('returns the parsed JSON body on success', async () => {
    mockFetchOnce(jsonResponse(200, { items: [1, 2] }));

    await expect(apiGet<{ items: number[] }>('/x')).resolves.toEqual({ items: [1, 2] });
  });

  it('never calls console', async () => {
    const consoleSpies = (['log', 'info', 'warn', 'error', 'debug'] as const).map((m) =>
      jest.spyOn(console, m).mockImplementation(() => undefined),
    );
    mockFetchOnce(problemResponse(500, { title: 'boom' }));
    await catchError(apiGet('/x'));
    jest.spyOn(globalThis, 'fetch').mockRejectedValueOnce(new TypeError('Network request failed'));
    await catchError(apiGet('/x'));

    consoleSpies.forEach((s) => expect(s).not.toHaveBeenCalled());
  });
});

describe('apiGet errors', () => {
  it('maps a problem+json 404 to not-found with title and correlation id', async () => {
    mockFetchOnce(problemResponse(404, { title: 'Product not found', correlationId: 'corr-1' }));

    const error = await catchError(apiGet('/api/v1/products/nope'));

    expect(error.kind).toBe('not-found');
    expect(error.status).toBe(404);
    expect(error.title).toBe('Product not found');
    expect(error.correlationId).toBe('corr-1');
  });

  it('maps a 400 to validation and exposes fieldErrors', async () => {
    mockFetchOnce(
      problemResponse(400, {
        title: 'One or more validation errors occurred.',
        errors: { pageSize: ['pageSize must be between 1 and 100.'] },
      }),
    );

    const error = await catchError(apiGet('/api/v1/products'));

    expect(error.kind).toBe('validation');
    expect(error.fieldErrors).toEqual({ pageSize: ['pageSize must be between 1 and 100.'] });
  });

  it('maps 422 to validation, other 4xx to client, and 5xx to server', async () => {
    mockFetchOnce(problemResponse(422, {}));
    expect((await catchError(apiGet('/x'))).kind).toBe('validation');
    mockFetchOnce(problemResponse(429, {}));
    expect((await catchError(apiGet('/x'))).kind).toBe('client');
    mockFetchOnce(problemResponse(500, { title: 'x' }));
    expect((await catchError(apiGet('/x'))).kind).toBe('server');
  });

  it('falls back to the response header for the correlation id when the body has none', async () => {
    mockFetchOnce(problemResponse(500, {}, { 'X-Correlation-ID': 'from-header' }));

    expect((await catchError(apiGet('/x'))).correlationId).toBe('from-header');
  });

  it('maps a fetch rejection to network', async () => {
    jest.spyOn(globalThis, 'fetch').mockRejectedValueOnce(new TypeError('Network request failed'));

    const error = await catchError(apiGet('/x'));

    expect(error.kind).toBe('network');
    expect(error.status).toBeUndefined();
  });

  it('handles a non-JSON 502 body without crashing', async () => {
    mockFetchOnce(new Response('<html>Bad gateway</html>', { status: 502, headers: { 'content-type': 'text/html' } }));

    const error = await catchError(apiGet('/x'));

    expect(error.kind).toBe('server');
    expect(error.status).toBe(502);
  });

  it('treats an unparseable 200 body as a server error', async () => {
    mockFetchOnce(new Response('not json', { status: 200, headers: { 'content-type': 'application/json' } }));

    expect((await catchError(apiGet('/x'))).kind).toBe('server');
  });

  it('aborts after 15 seconds and reports timeout', async () => {
    jest.useFakeTimers();
    jest.spyOn(globalThis, 'fetch').mockImplementation(
      (_url, init) =>
        new Promise((_resolve, reject) => {
          init?.signal?.addEventListener('abort', () => reject(new Error('aborted')));
        }),
    );

    const pending = catchError(apiGet('/x'));
    await jest.advanceTimersByTimeAsync(14_999);
    expect(globalThis.fetch).toHaveBeenCalledTimes(1);
    await jest.advanceTimersByTimeAsync(1);

    expect((await pending).kind).toBe('timeout');
  });

  it('rethrows the original abort when the caller cancels, so the query library sees a cancellation', async () => {
    const controller = new AbortController();
    jest.spyOn(globalThis, 'fetch').mockImplementation(
      (_url, init) =>
        new Promise((_resolve, reject) => {
          init?.signal?.addEventListener('abort', () => reject(new Error('caller aborted')));
        }),
    );

    const pending = apiGet('/x', { signal: controller.signal });
    controller.abort();

    await expect(pending).rejects.toThrow('caller aborted');
  });

  it('reports config without calling fetch when the base URL is missing', async () => {
    delete process.env.EXPO_PUBLIC_API_BASE_URL;
    const spy = jest.spyOn(globalThis, 'fetch');

    const error = await catchError(apiGet('/x'));

    expect(error.kind).toBe('config');
    expect(spy).not.toHaveBeenCalled();
  });
});

describe('userMessage', () => {
  const kinds = ['network', 'timeout', 'not-found', 'validation', 'client', 'server', 'config'] as const;

  it.each(kinds)('gives plain-language copy for %s', (kind) => {
    const message = userMessage(new ApiError(kind, 'internal detail'));
    expect(message.length).toBeGreaterThan(10);
    expect(message).not.toContain('internal detail');
    expect(message).not.toMatch(/ApiError|fetch|TypeError|undefined/);
  });

  it('uses the specified copy for the key kinds', () => {
    expect(userMessage(new ApiError('network', ''))).toBe("Can't reach the store. Check your connection and try again.");
    expect(userMessage(new ApiError('server', ''))).toBe('Something went wrong on our side. Please try again.');
    expect(userMessage(new ApiError('not-found', ''))).toBe("We couldn't find that.");
    expect(userMessage(new ApiError('config', ''))).toBe("The app isn't configured to reach the store.");
  });

  it('falls back to a generic message for non-ApiError values', () => {
    expect(userMessage(new Error('x'))).toBe('Something went wrong. Please try again.');
    expect(userMessage('weird')).toBe('Something went wrong. Please try again.');
  });
});

describe('apiGet correlation id on failures without a response', () => {
  function sentCorrelationId(spy: jest.SpyInstance): string {
    const init = spy.mock.calls[0][1] as RequestInit;
    return (init.headers as Record<string, string>)['X-Correlation-ID'];
  }

  it('attaches the sent X-Correlation-ID to a network error', async () => {
    const spy = jest.spyOn(globalThis, 'fetch').mockRejectedValueOnce(new TypeError('Network request failed'));

    const error = await catchError(apiGet('/x'));

    expect(error.kind).toBe('network');
    expect(error.correlationId).toBe(sentCorrelationId(spy));
    expect(error.correlationId).toMatch(/^[0-9a-f]{32}$/);
  });

  it('attaches the sent X-Correlation-ID to a timeout error', async () => {
    jest.useFakeTimers();
    const spy = jest.spyOn(globalThis, 'fetch').mockImplementation(
      (_url, init) =>
        new Promise((_resolve, reject) => {
          init?.signal?.addEventListener('abort', () => reject(new Error('aborted')));
        }),
    );

    const pending = catchError(apiGet('/x'));
    await jest.advanceTimersByTimeAsync(15_000);
    const error = await pending;

    expect(error.kind).toBe('timeout');
    expect(error.correlationId).toBe(sentCorrelationId(spy));
  });

  it('still prefers the id the server echoed', async () => {
    mockFetchOnce(problemResponse(500, { correlationId: 'from-server' }));

    expect((await catchError(apiGet('/x'))).correlationId).toBe('from-server');
  });
});

describe('ApiError carries the checkout causes (Slice 4)', () => {
  it('keeps the problem type, the rejected lines and the order id of a reply', async () => {
    mockFetchOnce(
      problemResponse(409, {
        title: 'Some items cannot be ordered',
        type: 'checkout-lines-rejected',
        lines: [{ variantId: 'v-1', reason: 'OutOfStock', available: 0 }, { variantId: 'v-2', reason: 'PriceChanged' }, { notALine: true }],
      }),
    );

    const conflict = await catchError(apiGet('/api/v1/cart'));

    expect(conflict.kind).toBe('conflict');
    expect(conflict.problemType).toBe('checkout-lines-rejected');
    expect(conflict.lines).toEqual([
      { variantId: 'v-1', reason: 'OutOfStock', available: 0 },
      { variantId: 'v-2', reason: 'PriceChanged' },
    ]);

    mockFetchOnce(problemResponse(503, { type: 'payment-provider-unavailable', orderId: 'o-1' }));
    const unavailable = await catchError(apiGet('/api/v1/cart'));

    expect(unavailable.kind).toBe('server');
    expect(unavailable.problemType).toBe('payment-provider-unavailable');
    expect(unavailable.orderId).toBe('o-1');
    expect(unavailable.lines).toBeUndefined();
  });
});

describe('ApiError carries the on-hand count of a stock conflict (Slice 10.4)', () => {
  it('keeps onHand next to available, and leaves it undefined when the reply has none', async () => {
    mockFetchOnce(problemResponse(409, { title: 'Conflict', onHand: 7, reserved: 5, available: 2 }));
    const withCount = await catchError(apiGet('/api/v1/admin/inventory'));

    expect(withCount.onHand).toBe(7);
    expect(withCount.available).toBe(2);

    mockFetchOnce(problemResponse(409, { title: 'Conflict' }));
    expect((await catchError(apiGet('/api/v1/admin/inventory'))).onHand).toBeUndefined();
  });
});

describe('apiSend with a FormData body', () => {
  it('sends the form as is with no JSON Content-Type, so fetch sets the multipart boundary', async () => {
    const spy = jest.spyOn(globalThis, 'fetch').mockImplementation(async () => jsonResponse(200, { storageKey: 'k', url: '/u' }));
    const form = new FormData();
    form.append('file', new Blob(['x']), 'a.png');

    await apiSend('POST', '/api/v1/admin/products/p-1/images', { body: form });

    const init = spy.mock.calls[0][1] as RequestInit;
    expect(init.body).toBe(form);
    expect(init.headers as Record<string, string>).not.toHaveProperty('Content-Type');
  });

  it('timeoutMs overrides the default timeout', async () => {
    jest.useFakeTimers();
    jest.spyOn(globalThis, 'fetch').mockImplementation(
      (_url, init) =>
        new Promise((_resolve, reject) => {
          (init?.signal as AbortSignal).addEventListener('abort', () => reject(new Error('aborted')));
        }),
    );

    const pending = catchError(apiSend('POST', '/x', { body: new FormData(), timeoutMs: 60_000 }));
    let settled = false;
    void pending.then(() => (settled = true));
    await jest.advanceTimersByTimeAsync(15_001);
    expect(settled).toBe(false);

    await jest.advanceTimersByTimeAsync(45_000);
    expect((await pending).kind).toBe('timeout');
  });
});
