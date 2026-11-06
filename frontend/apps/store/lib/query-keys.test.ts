import type { ProductListParams } from '@/features/catalog/types';

import { queryKeys } from './query-keys';

const base: ProductListParams = { page: 1, pageSize: 20 };

describe('queryKeys', () => {
  it('gives equal keys for equal normalized params, regardless of undefined or empty entries', () => {
    const a = queryKeys.products.list({ ...base, search: 'shirt' });
    const b = queryKeys.products.list({ pageSize: 20, page: 1, search: 'shirt', category: undefined, sort: undefined });
    expect(a).toEqual(b);
  });

  it.each<[string, Partial<ProductListParams>]>([
    ['search', { search: 'jeans' }],
    ['category', { category: 'tops' }],
    ['sort', { sort: 'price-asc' }],
    ['page', { page: 2 }],
    ['pageSize', { pageSize: 10 }],
  ])('differs when %s differs', (_name, change) => {
    expect(queryKeys.products.list({ ...base, ...change })).not.toEqual(queryKeys.products.list(base));
  });

  it('includes the slug in the detail key', () => {
    expect(queryKeys.products.detail('a')).not.toEqual(queryKeys.products.detail('b'));
    expect(queryKeys.products.detail('blue-shirt')).toContain('blue-shirt');
  });

  it('starts every product key with products.all so one invalidation covers them', () => {
    const all = queryKeys.products.all;
    expect(queryKeys.products.list(base).slice(0, all.length)).toEqual(all);
    expect(queryKeys.products.detail('x').slice(0, all.length)).toEqual(all);
  });
});

describe('queryKeys.admin', () => {
  it('starts every admin product key with admin.products.all and ignores empty filters', () => {
    const all = queryKeys.admin.products.all;
    const a = queryKeys.admin.products.list({ page: 1, pageSize: 20, status: 'all', search: undefined });
    const b = queryKeys.admin.products.list({ page: 1, pageSize: 20 });
    expect(a).toEqual(b);
    for (const key of [a, queryKeys.admin.products.detail('x'), queryKeys.admin.products.preview('x')]) {
      expect(key.slice(0, all.length)).toEqual([...all]);
    }
  });

  it('differs by status, id and between detail and preview', () => {
    expect(queryKeys.admin.products.list({ page: 1, pageSize: 20, status: 'active' })).not.toEqual(
      queryKeys.admin.products.list({ page: 1, pageSize: 20 }),
    );
    expect(queryKeys.admin.products.detail('a')).not.toEqual(queryKeys.admin.products.detail('b'));
    expect(queryKeys.admin.products.detail('a')).not.toEqual(queryKeys.admin.products.preview('a'));
    expect(queryKeys.auth.me).toEqual(['auth', 'me']);
  });
});
