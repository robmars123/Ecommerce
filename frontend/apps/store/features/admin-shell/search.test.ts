import { adminRouteDecision } from '@/features/auth/admin-access';
import { makeSearchResult } from '@/tests/admin-search-fixtures';

import { searchGroups, searchTerm } from './search';

describe('searchTerm', () => {
  it.each([
    ['', null],
    [' ', null],
    ['a', null],
    [' a ', null],
    ['ab', 'ab'],
    ['  ab  ', 'ab'],
    ['x'.repeat(101), 'x'.repeat(100)],
    [' ' + 'x'.repeat(100), 'x'.repeat(100)],
  ])('Q1 %j -> %j', (raw, expected) => {
    expect(searchTerm(raw)).toBe(expected);
  });
});

describe('searchGroups', () => {
  it('Q2 Admin gets Orders, Products, SKUs with labels and encoded hrefs', () => {
    expect(searchGroups('Admin', makeSearchResult())).toEqual([
      { title: 'Orders', items: [{ key: 'o1', label: '1001 · Paid', href: '/admin/orders/o1' }] },
      { title: 'Products', items: [{ key: 'p1', label: 'Blue Mug', href: '/admin/products/p1' }] },
      { title: 'SKUs', items: [{ key: 'MUG-BLU-L', label: 'MUG-BLU-L · Blue Mug', href: '/admin/products/p1' }] },
    ]);
    const encoded = searchGroups('Admin', makeSearchResult({ orders: [{ id: 'a/b', orderNumber: '1', status: 'Paid' }] }));
    expect(encoded[0].items[0].href).toBe('/admin/orders/a%2Fb');
  });

  it('Q3 Support only ever gets Orders; Customer and null get nothing', () => {
    const groups = searchGroups('Support', makeSearchResult());
    expect(groups.map((g) => g.title)).toEqual(['Orders']);
    expect(groups.flatMap((g) => g.items).some((i) => i.href.startsWith('/admin/products'))).toBe(false);
    expect(searchGroups('Support', makeSearchResult({ orders: [] }))).toEqual([]);
    expect(searchGroups('Customer', makeSearchResult())).toEqual([]);
    expect(searchGroups(null, makeSearchResult())).toEqual([]);
  });

  it('Q4 drops empty groups and keeps the server order', () => {
    expect(searchGroups('Admin', makeSearchResult({ orders: [], products: [] })).map((g) => g.title)).toEqual(['SKUs']);
    expect(searchGroups('Admin', makeSearchResult({ orders: [], products: [], skus: [] }))).toEqual([]);
    const two = searchGroups(
      'Admin',
      makeSearchResult({
        orders: [
          { id: 'o2', orderNumber: '1002', status: 'Paid' },
          { id: 'o1', orderNumber: '1001', status: 'Paid' },
        ],
      }),
    );
    expect(two[0].items.map((i) => i.key)).toEqual(['o2', 'o1']);
  });

  it.each(['Admin', 'Support'] as const)('Q5 every %s href passes adminRouteDecision', (role) => {
    const hrefs = searchGroups(role, makeSearchResult()).flatMap((g) => g.items.map((i) => i.href));
    expect(hrefs.length).toBeGreaterThan(0);
    for (const href of hrefs) expect(adminRouteDecision(role, href)).toBeNull();
  });
});
