import { makeAdminProduct } from '@/tests/admin-fixtures';

import { emptyFormState, hasChanges, toCreateRequest, toFormState, toUpdateRequest } from './form-model';

describe('form model', () => {
  it('create: trims text, upper-cases the currency, keeps prices as typed and omits isActive', () => {
    const state = emptyFormState();
    state.slug = ' linen-shirt ';
    state.name = ' Linen shirt ';
    state.currency = 'usd';
    state.variants[0] = {
      ...state.variants[0],
      sku: ' LS-1 ',
      name: 'Default',
      price: ' 1.50 ',
      compareAt: '',
      size: ' ',
      weightGrams: '250',
    };

    const request = toCreateRequest(state);

    expect(request.slug).toBe('linen-shirt');
    expect(request.name).toBe('Linen shirt');
    expect(request.variants[0]).toEqual({
      sku: 'LS-1',
      name: 'Default',
      size: null,
      colour: null,
      price: { amount: '1.50', currency: 'USD' },
      compareAtPrice: null,
      weightGrams: 250,
    });
  });

  it('edit: round-trips a product, keeps variant ids and isActive, and carries the version', () => {
    const product = makeAdminProduct({ version: 77 });

    const request = toUpdateRequest(toFormState(product), product.version);

    expect(request.version).toBe(77);
    expect(request.variants[0]).toMatchObject({ id: 'av-1', sku: 'LS-1', isActive: true, price: { amount: '49.00', currency: 'USD' } });
    expect(request.categorySlug).toBe('shirts');
  });

  it('sends a compare-at price only when one is typed', () => {
    const state = emptyFormState();
    state.variants[0] = { ...state.variants[0], compareAt: '59.00' };

    expect(toCreateRequest(state).variants[0].compareAtPrice).toEqual({ amount: '59.00', currency: 'USD' });
  });
});

describe('hasChanges', () => {
  const img = (n: string, url: string | null = null) => ({ storageKey: `products/a-1/${n}.png`, altText: n, sortOrder: 1, url });
  const base = () => toFormState(makeAdminProduct({ images: [img('a', '/u/a'), img('b', '/u/b')] }));

  it('is false for the same product loaded twice (different row keys)', () => {
    expect(hasChanges(base(), base())).toBe(false);
  });

  it('flags a changed name but not surrounding spaces', () => {
    const baseline = base();
    expect(hasChanges(baseline, { ...base(), name: 'Other' })).toBe(true);
    expect(hasChanges(baseline, { ...base(), name: ` ${baseline.name} ` })).toBe(false);
  });

  it('compares prices as text and flags an isActive toggle', () => {
    const baseline = base();
    const price = base();
    price.variants[0] = { ...price.variants[0], price: '49.0' };
    const active = base();
    active.variants[0] = { ...active.variants[0], isActive: !active.variants[0].isActive };

    expect(hasChanges(baseline, price)).toBe(true);
    expect(hasChanges(baseline, active)).toBe(true);
  });

  it('flags image reorder and removal but ignores the thumbnail url', () => {
    const baseline = base();
    const swapped = base();
    swapped.images = [swapped.images[1], swapped.images[0]];
    const urlOnly = base();
    urlOnly.images = urlOnly.images.map((i) => ({ ...i, url: '/other' }));
    const removed = base();
    removed.images = removed.images.slice(1);

    expect(hasChanges(baseline, swapped)).toBe(true);
    expect(hasChanges(baseline, urlOnly)).toBe(false);
    expect(hasChanges(baseline, removed)).toBe(true);
  });
});
