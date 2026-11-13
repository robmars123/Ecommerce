import { fireEvent, screen, waitFor } from 'expo-router/testing-library';

import { makeAdminProduct } from '@/tests/admin-fixtures';
import { jsonResponse, problemResponse } from '@/tests/fetch-mock';
import { makeDetail } from '@/tests/fixtures';
import { renderRouterWithQuery } from '@/tests/render-with-query';

import * as platform from '@/lib/platform';

import { AdminProductDetailView, NOT_VISIBLE_COPY } from './AdminProductDetailView';
import { STALE_VERSION_COPY } from './ProductForm';

jest.mock('@/lib/platform', () => ({ ...jest.requireActual('@/lib/platform'), pickImageFile: jest.fn() }));
const pickImageFile = platform.pickImageFile as jest.Mock;

beforeEach(() => {
  process.env.EXPO_PUBLIC_API_BASE_URL = 'http://api.test.invalid:5080';
});

afterEach(() => {
  jest.restoreAllMocks();
  delete process.env.EXPO_PUBLIC_API_BASE_URL;
});

const render = () =>
  renderRouterWithQuery({ 'admin/products/[id]': () => <AdminProductDetailView id="a-1" /> }, { initialUrl: '/admin/products/a-1' });

type Handler = (method: string, path: string) => Response | undefined;

function mockApi(handler: Handler) {
  return jest.spyOn(globalThis, 'fetch').mockImplementation(async (input, init) => {
    const path = new URL(String(input)).pathname;
    const method = (init as RequestInit | undefined)?.method ?? 'GET';
    return handler(method, path) ?? new Response('not mocked', { status: 599 });
  });
}

const calls = (spy: jest.SpyInstance, method: string, suffix: string) =>
  spy.mock.calls.filter((c) => (c[1] as RequestInit).method === method && String(c[0]).endsWith(suffix));

describe('AdminProductDetailView', () => {
  it('shows the not-visible banner for an unpublished product and Publish posts then refetches', async () => {
    let published = false;
    const spy = mockApi((method, path) => {
      if (method === 'POST' && path.endsWith('/publish')) {
        published = true;
        return jsonResponse(200, makeAdminProduct({ isActive: true }));
      }
      if (path === '/api/v1/admin/products/a-1') return jsonResponse(200, makeAdminProduct({ isActive: published }));
      return undefined;
    });
    await render();

    expect(await screen.findByText(NOT_VISIBLE_COPY)).toBeOnTheScreen();
    await fireEvent.press(screen.getByRole('button', { name: 'Publish' }));

    await waitFor(() => expect(screen.queryByText(NOT_VISIBLE_COPY)).toBeNull());
    expect(calls(spy, 'POST', '/a-1/publish')).toHaveLength(1);
    expect(screen.getByRole('button', { name: 'Unpublish' })).toBeOnTheScreen();
  });

  it('shows the server reason when publishing is refused with a 409', async () => {
    mockApi((method, path) => {
      if (method === 'POST') return problemResponse(409, { errors: { '': ['A product needs an active variant to be published.'] } });
      if (path === '/api/v1/admin/products/a-1') return jsonResponse(200, makeAdminProduct());
      return undefined;
    });
    await render();

    await fireEvent.press(await screen.findByRole('button', { name: 'Publish' }));

    expect(await screen.findByText('A product needs an active variant to be published.')).toBeOnTheScreen();
  });

  it('saves with the loaded version and offers Reload when it is stale', async () => {
    let detailFetches = 0;
    const spy = mockApi((method, path) => {
      if (method === 'PUT') return problemResponse(409, { errors: { version: ['changed'] } });
      if (path === '/api/v1/admin/products/a-1') {
        detailFetches += 1;
        return jsonResponse(
          200,
          makeAdminProduct({ version: detailFetches === 1 ? 100 : 101, name: detailFetches === 1 ? 'Linen shirt' : 'Renamed elsewhere' }),
        );
      }
      return undefined;
    });
    await render();

    await fireEvent.changeText(await screen.findByLabelText('Name'), 'My edit');
    await fireEvent.press(screen.getByRole('button', { name: 'Save changes' }));

    expect(await screen.findByText(STALE_VERSION_COPY)).toBeOnTheScreen();
    const put = calls(spy, 'PUT', '/a-1')[0][1] as RequestInit;
    const body = JSON.parse(put.body as string);
    expect(body.version).toBe(100);
    expect(body.variants[0]).toMatchObject({ id: 'av-1', isActive: true });

    await fireEvent.press(screen.getByRole('button', { name: 'Reload' }));

    await waitFor(() => expect(screen.getByLabelText('Name').props.value).toBe('Renamed elsewhere'));
    expect(screen.queryByText(STALE_VERSION_COPY)).toBeNull();
  });

  it('Preview renders the customer product page from the preview endpoint', async () => {
    const spy = mockApi((method, path) => {
      if (path.endsWith('/preview'))
        return jsonResponse(200, { product: makeDetail({ name: 'Previewed shirt' }), isActive: false, isVisibleToCustomers: false });
      if (path === '/api/v1/admin/products/a-1') return jsonResponse(200, makeAdminProduct());
      return undefined;
    });
    await render();

    await fireEvent.press(await screen.findByRole('button', { name: 'Preview' }));

    expect((await screen.findAllByText('Previewed shirt')).length).toBeGreaterThanOrEqual(1);
    expect(spy.mock.calls.some((c) => String(c[0]).endsWith('/a-1/preview'))).toBe(true);
    expect(screen.getByText(NOT_VISIBLE_COPY)).toBeOnTheScreen();
  });

  it('a variant can be switched off but a saved variant has no Remove button', async () => {
    mockApi((_method, path) => (path === '/api/v1/admin/products/a-1' ? jsonResponse(200, makeAdminProduct()) : undefined));
    await render();

    expect(await screen.findByLabelText('Variant 1 active')).toBeOnTheScreen();
    expect(screen.queryByRole('button', { name: 'Remove variant 1' })).toBeNull();
  });

  describe('images', () => {
    const KEY = 'products/a-1/new.png';
    const pickedForm = () => {
      const form = new FormData();
      form.append('file', new Blob(['x']), 'a.png');
      return form;
    };
    const savedImage = { storageKey: 'products/a-1/old.png', altText: 'Old', sortOrder: 1, url: '/api/v1/images/products/a-1/old.png' };

    function api(upload: () => Response | Promise<Response>, product = makeAdminProduct({ images: [savedImage] })) {
      return jest.spyOn(globalThis, 'fetch').mockImplementation(async (input, init) => {
        const path = new URL(String(input)).pathname;
        const method = (init as RequestInit | undefined)?.method ?? 'GET';
        if (method === 'POST' && path.endsWith('/a-1/images')) return upload();
        if (method === 'PUT') return jsonResponse(200, product);
        if (path === '/api/v1/admin/products/a-1') return jsonResponse(200, product);
        return new Response('not mocked', { status: 599 });
      });
    }

    it('uploads a picked image, shows Uploading while it runs, adds a row with a thumbnail, and Save sends the new key', async () => {
      pickImageFile.mockResolvedValue(pickedForm());
      let release: (r: Response) => void = () => {};
      const gate = new Promise<Response>((resolve) => (release = resolve));
      const spy = api(() => gate);
      await render();

      await fireEvent.press(await screen.findByRole('button', { name: 'Add image' }));
      expect(await screen.findByRole('button', { name: 'Uploading…' })).toBeDisabled();
      release(jsonResponse(200, { storageKey: KEY, url: `/api/v1/images/${KEY}` }));

      expect(await screen.findByLabelText('Image 2 description')).toBeOnTheScreen();
      expect(screen.getByRole('button', { name: 'Add image' })).toBeEnabled();
      expect(screen.getAllByRole('image')).toHaveLength(2);
      expect(calls(spy, 'POST', '/a-1/images')).toHaveLength(1);
      expect((calls(spy, 'POST', '/a-1/images')[0][1] as RequestInit).body).toBeInstanceOf(FormData);

      await fireEvent.press(screen.getByRole('button', { name: 'Save changes' }));
      await waitFor(() => expect(calls(spy, 'PUT', '/a-1')).toHaveLength(1));
      const body = JSON.parse((calls(spy, 'PUT', '/a-1')[0][1] as RequestInit).body as string);
      expect(body.images).toEqual([
        { storageKey: savedImage.storageKey, altText: 'Old' },
        { storageKey: KEY, altText: '' },
      ]);
    });

    it('shows the server message for a refused file and adds no row', async () => {
      pickImageFile.mockResolvedValue(pickedForm());
      api(() => problemResponse(400, { errors: { file: ['Only JPEG, PNG or WebP images are allowed.'] } }));
      await render();

      await fireEvent.press(await screen.findByRole('button', { name: 'Add image' }));

      expect(await screen.findByText('Only JPEG, PNG or WebP images are allowed.')).toBeOnTheScreen();
      expect(screen.queryByLabelText('Image 2 description')).toBeNull();
    });

    it('sends no request when the pick is cancelled', async () => {
      pickImageFile.mockResolvedValue(null);
      const spy = api(() => jsonResponse(200, {}));
      await render();

      await fireEvent.press(await screen.findByRole('button', { name: 'Add image' }));

      await waitFor(() => expect(pickImageFile).toHaveBeenCalled());
      expect(calls(spy, 'POST', '/a-1/images')).toHaveLength(0);
    });

    it('Remove drops the row from the saved request', async () => {
      const spy = api(() => jsonResponse(200, {}));
      await render();

      await fireEvent.press(await screen.findByRole('button', { name: 'Remove image 1' }));
      await fireEvent.press(screen.getByRole('button', { name: 'Save changes' }));

      await waitFor(() => expect(calls(spy, 'PUT', '/a-1')).toHaveLength(1));
      expect(JSON.parse((calls(spy, 'PUT', '/a-1')[0][1] as RequestInit).body as string).images).toEqual([]);
    });
  });
});
