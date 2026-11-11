import * as SecureStore from 'expo-secure-store';
import { renderRouter, screen, waitFor } from 'expo-router/testing-library';

import { resetSession } from '@/features/auth/session';
import { jsonResponse } from '@/tests/fetch-mock';
import { categories } from '@/tests/fixtures';
import { signInAs } from '@/tests/session';

jest.mock('expo-secure-store');
jest.mock('react-native/Libraries/Utilities/useWindowDimensions', () => ({
  __esModule: true,
  default: () => ({ width: 1280, height: 800, scale: 1, fontScale: 1 }),
}));

const store = SecureStore as jest.Mocked<typeof SecureStore>;

function mockApi() {
  return jest.spyOn(globalThis, 'fetch').mockImplementation(async (input) => {
    const { pathname } = new URL(String(input));
    if (pathname === '/api/v1/categories') return jsonResponse(200, { items: categories });
    return new Response('not mocked', { status: 599 });
  });
}

const adminCalls = (spy: jest.SpyInstance) => spy.mock.calls.filter((c) => String(c[0]).includes('/api/v1/admin'));

beforeEach(() => {
  process.env.EXPO_PUBLIC_API_BASE_URL = 'http://api.test.invalid:5080';
  store.getItemAsync.mockResolvedValue(null);
});

afterEach(() => {
  resetSession();
  jest.restoreAllMocks();
  delete process.env.EXPO_PUBLIC_API_BASE_URL;
});

describe('admin shell on real routes', () => {
  it('R1: Admin at /admin/orders sees the sidebar and the orders screen inside it', async () => {
    mockApi();
    await signInAs('Admin');
    await renderRouter('./app', { initialUrl: '/admin/orders' });

    expect(screen).toHavePathname('/admin/orders');
    expect(screen.getByLabelText('Admin navigation')).toBeTruthy();
    expect(screen.getAllByRole('header', { name: 'Orders' }).length).toBeGreaterThanOrEqual(1);
  });

  it('R2: Customer at /admin/orders is redirected before the shell renders', async () => {
    const spy = mockApi();
    await signInAs('Customer');
    await renderRouter('./app', { initialUrl: '/admin/orders' });

    await waitFor(() => expect(screen).toHavePathname('/products'));
    expect(screen.queryByLabelText('Admin navigation')).toBeNull();
    expect(adminCalls(spy)).toHaveLength(0);
  });
});
