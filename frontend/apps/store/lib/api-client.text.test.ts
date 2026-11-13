import { ApiError, apiGetText, setAuthHandlers } from './api-client';
import { problemResponse } from '@/tests/fetch-mock';

// P2-tier4 D17: apiGetText shares the one HTTP path (bearer, single 401 refresh, error kinds) and returns the raw body.

let token: string | undefined;
let refresh: jest.Mock<Promise<boolean>, []>;

beforeEach(() => {
  process.env.EXPO_PUBLIC_API_BASE_URL = 'http://api.test.invalid:5080';
  token = 'access-token';
  refresh = jest.fn(async () => {
    token = 'fresh-token';
    return true;
  });
  setAuthHandlers({ getAccessToken: () => token, refresh });
});

afterEach(() => {
  jest.restoreAllMocks();
  setAuthHandlers(undefined);
  delete process.env.EXPO_PUBLIC_API_BASE_URL;
});

const csv = () => new Response('a,b\r\n1,2\r\n', { status: 200, headers: { 'content-type': 'text/csv' } });

describe('apiGetText', () => {
  it('returns the raw text and sends the bearer token and Accept: text/csv', async () => {
    const spy = jest.spyOn(globalThis, 'fetch').mockImplementation(async () => csv());

    await expect(apiGetText('/api/v1/admin/dashboard/export', { params: { period: '7d' } })).resolves.toBe('a,b\r\n1,2\r\n');

    const headers = spy.mock.calls[0][1]!.headers as Record<string, string>;
    expect(headers.Authorization).toBe('Bearer access-token');
    expect(headers.Accept).toBe('text/csv');
    expect(String(spy.mock.calls[0][0])).toContain('?period=7d');
  });

  it('turns a 403 into a forbidden error', async () => {
    jest.spyOn(globalThis, 'fetch').mockImplementation(async () => problemResponse(403, {}));

    const error = (await apiGetText('/x').catch((e: unknown) => e)) as ApiError;

    expect(error).toBeInstanceOf(ApiError);
    expect(error.kind).toBe('forbidden');
  });

  it('refreshes once on a 401 and retries with the new token', async () => {
    const spy = jest
      .spyOn(globalThis, 'fetch')
      .mockImplementation(async (_url, init) =>
        (init!.headers as Record<string, string>).Authorization === 'Bearer fresh-token' ? csv() : problemResponse(401, {}),
      );

    await expect(apiGetText('/x')).resolves.toBe('a,b\r\n1,2\r\n');

    expect(refresh).toHaveBeenCalledTimes(1);
    expect(spy).toHaveBeenCalledTimes(2);
  });
});
