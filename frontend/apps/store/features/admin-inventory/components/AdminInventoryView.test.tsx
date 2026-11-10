import { fireEvent, screen, waitFor } from 'expo-router/testing-library';

import { jsonResponse, problemResponse } from '@/tests/fetch-mock';
import { makeInventoryItem, makeInventoryPage } from '@/tests/fixtures';
import { createTestQueryClient, renderRouterWithQuery } from '@/tests/render-with-query';

import type { AdminInventoryItem, AdminInventoryPage } from '../types';
import { AdminInventoryView } from './AdminInventoryView';

beforeEach(() => {
  process.env.EXPO_PUBLIC_API_BASE_URL = 'http://api.test.invalid:5080';
});

afterEach(() => {
  jest.restoreAllMocks();
  delete process.env.EXPO_PUBLIC_API_BASE_URL;
});

const ROW = makeInventoryItem({ variantId: 'v-1', sku: 'TS-1', onHand: 10, reserved: 2, available: 8 });
const LIST_PATH = '/api/v1/admin/inventory';
const ADJUST_PATH = '/api/v1/admin/inventory/adjust';

type Mock = { list: () => AdminInventoryPage | Response; adjust?: () => Response | Promise<Response> };

function mockApi({ list, adjust }: Mock) {
  return jest.spyOn(globalThis, 'fetch').mockImplementation(async (input, init) => {
    const { pathname } = new URL(String(input));
    const method = init?.method ?? 'GET';
    if (method === 'GET' && pathname === LIST_PATH) {
      const result = list();
      return result instanceof Response ? result : jsonResponse(200, result);
    }
    if (method === 'POST' && pathname === ADJUST_PATH && adjust) return adjust();
    return new Response('not mocked', { status: 599 });
  });
}

const isPost = (call: unknown[]) => (call[1] as RequestInit | undefined)?.method === 'POST';
const gets = (spy: jest.SpyInstance) => spy.mock.calls.filter((c) => !isPost(c) && new URL(String(c[0])).pathname === LIST_PATH);
const posts = (spy: jest.SpyInstance) => spy.mock.calls.filter(isPost);
const postBody = (spy: jest.SpyInstance, index = 0) => JSON.parse(String((posts(spy)[index][1] as RequestInit).body));

async function render() {
  await renderRouterWithQuery({ index: AdminInventoryView }, { initialUrl: '/', client: createTestQueryClient() });
}

async function openAdjust(sku: string, quantity: string, reason: string) {
  await fireEvent.press(await screen.findByLabelText(`Adjust ${sku}`));
  await fireEvent.changeText(screen.getByLabelText(`Quantity for ${sku}`), quantity);
  await fireEvent.changeText(screen.getByLabelText(`Reason for ${sku}`), reason);
}

const updated = (over: Partial<AdminInventoryItem>): AdminInventoryItem => ({ ...ROW, ...over });

describe('AdminInventoryView', () => {
  it('lists each row with its numbers and status text', async () => {
    mockApi({
      list: () =>
        makeInventoryPage([
          ROW,
          makeInventoryItem({ variantId: 'v-2', sku: 'TS-2', variantName: 'Red / S', stockStatus: 'Low', onHand: 3, reserved: 0, available: 3 }),
          makeInventoryItem({ variantId: 'v-3', sku: 'TS-3', stockStatus: 'Out', onHand: 0, reserved: 0, available: 0 }),
          makeInventoryItem({ variantId: 'v-4', sku: 'TS-4', isActive: false, onHand: 5, reserved: 0, available: 5 }),
        ]),
    });
    await render();

    expect(await screen.findByText('Admin · Inventory')).toBeOnTheScreen();
    expect(await screen.findByText('SKU TS-1')).toBeOnTheScreen();
    expect(screen.getAllByText('Test Shirt · Blue / M')).toHaveLength(3);
    expect(screen.getByText('On hand 10 · Reserved 2 · Available 8')).toBeOnTheScreen();
    expect(screen.getAllByText('In stock').length).toBeGreaterThan(0);
    expect(screen.getByText('Low stock')).toBeOnTheScreen();
    expect(screen.getByText('Out of stock')).toBeOnTheScreen();
    expect(screen.getByText('Inactive')).toBeOnTheScreen();
  });

  it('shows an empty state for an empty page', async () => {
    mockApi({ list: () => makeInventoryPage([]) });
    await render();

    expect(await screen.findByText('No variants yet.')).toBeOnTheScreen();
  });

  it('shows an error state and Try again refetches', async () => {
    let fail = true;
    const spy = mockApi({ list: () => (fail ? problemResponse(500, { title: 'Server error' }) : makeInventoryPage([ROW])) });
    await render();

    const retry = await screen.findByText('Try again');
    fail = false;
    await fireEvent.press(retry);

    expect(await screen.findByText('SKU TS-1')).toBeOnTheScreen();
    expect(gets(spy)).toHaveLength(2);
  });

  it('requests page 2 on Next page', async () => {
    const spy = mockApi({ list: () => makeInventoryPage([ROW], { totalPages: 2, totalCount: 30 }) });
    await render();

    await fireEvent.press(await screen.findByText('Next'));

    await waitFor(() => expect(gets(spy).some((c) => String(c[0]).includes('page=2'))).toBe(true));
  });

  it('adjust patches only that row from the 200 body and never rereads the list', async () => {
    const spy = mockApi({
      list: () => makeInventoryPage([ROW, makeInventoryItem({ variantId: 'v-2', sku: 'TS-2' })]),
      adjust: () => jsonResponse(200, updated({ onHand: 15, available: 13, stockStatus: 'Low' })),
    });
    await render();

    await openAdjust('TS-1', '5', 'Restock');
    await fireEvent.press(screen.getByLabelText('Add stock'));

    expect(await screen.findByText('On hand 15 · Reserved 2 · Available 13')).toBeOnTheScreen();
    expect(screen.getByText('Low stock')).toBeOnTheScreen();
    expect(postBody(spy)).toEqual({ variantId: 'v-1', delta: 5, expectedOnHand: 10, reason: 'Restock' });
    expect(screen.queryByLabelText('Quantity for TS-1')).toBeNull();
    expect(screen.getByLabelText('Adjust TS-2')).toBeOnTheScreen();
    expect(gets(spy)).toHaveLength(1);
  });

  it('Remove stock sends a negative delta', async () => {
    const spy = mockApi({ list: () => makeInventoryPage([ROW]), adjust: () => jsonResponse(200, updated({ onHand: 7, available: 5 })) });
    await render();

    await openAdjust('TS-1', '3', 'Damaged');
    await fireEvent.press(screen.getByLabelText('Remove stock'));

    expect(await screen.findByText('On hand 7 · Reserved 2 · Available 5')).toBeOnTheScreen();
    expect(postBody(spy).delta).toBe(-3);
  });

  it('shows field errors and sends nothing for a zero quantity or an empty reason', async () => {
    const spy = mockApi({ list: () => makeInventoryPage([ROW]), adjust: () => jsonResponse(200, ROW) });
    await render();

    await openAdjust('TS-1', '0', '');
    await fireEvent.press(screen.getByLabelText('Add stock'));

    expect(await screen.findByText('Enter a whole number from 1 to 1,000,000.')).toBeOnTheScreen();
    expect(screen.getByText('Enter a reason of 1 to 500 characters.')).toBeOnTheScreen();
    expect(posts(spy)).toHaveLength(0);
  });

  it('409 stale: names both numbers, Reload rereads that page once, and the next adjust sends the new expectedOnHand', async () => {
    let current = ROW;
    let calls = 0;
    const spy = mockApi({
      list: () => makeInventoryPage([current]),
      adjust: () => {
        calls += 1;
        return calls === 1
          ? problemResponse(409, { title: 'Conflict', onHand: 14, reserved: 2, available: 12 })
          : jsonResponse(200, updated({ onHand: 19, available: 17 }));
      },
    });
    await render();

    await openAdjust('TS-1', '5', 'Restock');
    await fireEvent.press(screen.getByLabelText('Add stock'));

    expect(await screen.findByText('On hand is now 14, not 10. Someone else changed it. Reload to see the current numbers.')).toBeOnTheScreen();
    expect(gets(spy)).toHaveLength(1);
    expect(screen.getByLabelText('Quantity for TS-1').props.value).toBe('5');

    current = updated({ onHand: 14, available: 12 });
    await fireEvent.press(screen.getByLabelText('Reload'));
    expect(await screen.findByText('On hand 14 · Reserved 2 · Available 12')).toBeOnTheScreen();
    expect(gets(spy)).toHaveLength(2);
    expect(String(gets(spy)[1][0])).toContain('page=1');

    await fireEvent.press(screen.getByLabelText('Add stock'));
    await waitFor(() => expect(posts(spy)).toHaveLength(2));
    expect(postBody(spy, 1).expectedOnHand).toBe(14);
  });

  it('409 insufficient: names available, offers no Reload and rereads nothing', async () => {
    const spy = mockApi({
      list: () => makeInventoryPage([ROW]),
      adjust: () => problemResponse(409, { title: 'Conflict', onHand: 10, reserved: 2, available: -1 }),
    });
    await render();

    await openAdjust('TS-1', '11', 'Shrinkage');
    await fireEvent.press(screen.getByLabelText('Remove stock'));

    expect(await screen.findByText('Only -1 available; reserved stock cannot be removed.')).toBeOnTheScreen();
    expect(screen.queryByLabelText('Reload')).toBeNull();
    expect(gets(spy)).toHaveLength(1);
  });

  it('400 shows the reason error under Reason', async () => {
    mockApi({
      list: () => makeInventoryPage([ROW]),
      adjust: () => problemResponse(400, { title: 'Bad request', errors: { reason: ['Reason is required.'] } }),
    });
    await render();

    await openAdjust('TS-1', '5', 'x');
    await fireEvent.press(screen.getByLabelText('Add stock'));

    expect(await screen.findByText('Reason is required.')).toBeOnTheScreen();
  });

  it('404 shows an alert with Reload', async () => {
    mockApi({ list: () => makeInventoryPage([ROW]), adjust: () => problemResponse(404, { title: 'Not found' }) });
    await render();

    await openAdjust('TS-1', '5', 'x');
    await fireEvent.press(screen.getByLabelText('Add stock'));

    expect(await screen.findByText('This variant no longer exists. Reload the list.')).toBeOnTheScreen();
    expect(screen.getByLabelText('Reload')).toBeOnTheScreen();
  });

  it('a double tap on Add stock sends one request', async () => {
    let release: (r: Response) => void = () => undefined;
    const spy = mockApi({ list: () => makeInventoryPage([ROW]), adjust: () => new Promise<Response>((resolve) => (release = resolve)) });
    await render();

    await openAdjust('TS-1', '5', 'Restock');
    await fireEvent.press(screen.getByLabelText('Add stock'));
    await fireEvent.press(screen.getByLabelText('Add stock'));

    await waitFor(() => expect(posts(spy)).toHaveLength(1));
    release(jsonResponse(200, updated({ onHand: 15, available: 13 })));
    expect(await screen.findByText('On hand 15 · Reserved 2 · Available 13')).toBeOnTheScreen();
    expect(posts(spy)).toHaveLength(1);
  });

  it('opening Adjust on a second row closes the first', async () => {
    mockApi({ list: () => makeInventoryPage([ROW, makeInventoryItem({ variantId: 'v-2', sku: 'TS-2' })]) });
    await render();

    await fireEvent.press(await screen.findByLabelText('Adjust TS-1'));
    expect(screen.getByLabelText('Quantity for TS-1')).toBeOnTheScreen();
    await fireEvent.press(screen.getByLabelText('Adjust TS-2'));

    expect(screen.queryByLabelText('Quantity for TS-1')).toBeNull();
    expect(screen.getByLabelText('Quantity for TS-2')).toBeOnTheScreen();
  });
});
