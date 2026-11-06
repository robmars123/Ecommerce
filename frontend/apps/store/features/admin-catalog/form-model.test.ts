import { makeAdminProduct } from '@/tests/admin-fixtures';

import { emptyFormState, toCreateRequest, toFormState, toUpdateRequest } from './form-model';

describe('form model', () => {
  it('create: trims text, upper-cases the currency, keeps prices as typed and omits isActive', () => {
    const state = emptyFormState();
    state.slug = ' linen-shirt ';
    state.name = ' Linen shirt ';
    state.currency = 'usd';
    state.variants[0] = { ...state.variants[0], sku: ' LS-1 ', name: 'Default', price: ' 1.50 ', compareAt: '', size: ' ', weightGrams: '250' };

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
