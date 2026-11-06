import { safeReturnTo } from './return-to';

describe('safeReturnTo', () => {
  it.each(['//evil.test', 'https://x', '/\\evil', 'javascript:x', '', undefined, 'admin', '/a\\b', '/a\nb'])(
    'falls back to / for %p',
    (value) => {
      expect(safeReturnTo(value)).toBe('/');
    },
  );

  it('keeps a same-app path, with its query', () => {
    expect(safeReturnTo('/admin/products')).toBe('/admin/products');
    expect(safeReturnTo('/products?search=a')).toBe('/products?search=a');
  });

  it('takes the first value of an array', () => {
    expect(safeReturnTo(['/admin', '//evil.test'])).toBe('/admin');
    expect(safeReturnTo(['//evil.test', '/admin'])).toBe('/');
  });
});
