import { makeDashboard, makeRecentOrdersPage } from '@/tests/admin-dashboard-fixtures';
import { calledUrls, jsonResponse } from '@/tests/fetch-mock';

import { fetchDashboard, fetchRecentOrders } from './api';

beforeEach(() => {
  process.env.EXPO_PUBLIC_API_BASE_URL = 'http://api.test.invalid:5080';
});

afterEach(() => {
  jest.restoreAllMocks();
  delete process.env.EXPO_PUBLIC_API_BASE_URL;
});

describe('admin dashboard api', () => {
  it('asks the server for the chosen period', async () => {
    const spy = jest.spyOn(globalThis, 'fetch').mockImplementation(async () => jsonResponse(200, makeDashboard({ period: '7d' })));

    const dashboard = await fetchDashboard('7d');

    expect(calledUrls(spy)).toEqual(['http://api.test.invalid:5080/api/v1/admin/dashboard?period=7d']);
    expect(dashboard.period).toBe('7d');
  });

  it('pages recent orders ten at a time', async () => {
    const spy = jest.spyOn(globalThis, 'fetch').mockImplementation(async () => jsonResponse(200, makeRecentOrdersPage([])));

    await fetchRecentOrders(2);

    expect(calledUrls(spy)).toEqual(['http://api.test.invalid:5080/api/v1/admin/dashboard/recent-orders?page=2&pageSize=10']);
  });
});
