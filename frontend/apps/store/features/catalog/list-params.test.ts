import { nextListParams, parseListParams, PRODUCT_PAGE_SIZE, showingRange } from './list-params';
import type { ProductListPage, ProductListParams } from './types';

const base: ProductListParams = { page: 1, pageSize: PRODUCT_PAGE_SIZE };

describe('parseListParams', () => {
  it('defaults to page 1 and the fixed page size', () => {
    expect(parseListParams({})).toEqual({ page: 1, pageSize: 20 });
  });

  it('reads search, category, sort and page', () => {
    expect(parseListParams({ search: 'shirt', category: 'tops', sort: 'price-asc', page: '3' })).toEqual({
      search: 'shirt',
      category: 'tops',
      sort: 'price-asc',
      page: 3,
      pageSize: 20,
    });
  });

  it.each(['0', '-2', 'abc', '1.5', '', '99999999999'])('turns invalid page "%s" into 1', (page) => {
    expect(parseListParams({ page }).page).toBe(1);
  });

  it('takes the first value when a param is repeated', () => {
    expect(parseListParams({ search: ['a', 'b'] }).search).toBe('a');
  });

  it('drops an unknown sort', () => {
    expect(parseListParams({ sort: 'bogus' }).sort).toBeUndefined();
  });

  it('drops relevance when there is no search, and keeps it when there is', () => {
    expect(parseListParams({ sort: 'relevance' }).sort).toBeUndefined();
    expect(parseListParams({ sort: 'relevance', search: '   ' }).sort).toBeUndefined();
    expect(parseListParams({ sort: 'relevance', search: 'shirt' }).sort).toBe('relevance');
  });

  it('trims search, drops blank search and blank category, and caps search at 100 characters', () => {
    expect(parseListParams({ search: '  shirt  ' }).search).toBe('shirt');
    expect(parseListParams({ search: '   ' }).search).toBeUndefined();
    expect(parseListParams({ category: '  ' }).category).toBeUndefined();
    expect(parseListParams({ search: 'a'.repeat(150) }).search).toHaveLength(100);
  });
});

describe('nextListParams', () => {
  const current: ProductListParams = { ...base, search: 'shirt', category: 'tops', sort: 'price-asc', page: 4 };

  it('resets to page 1 when search, category or sort changes', () => {
    expect(nextListParams(current, { search: 'jeans' }).page).toBe(1);
    expect(nextListParams(current, { category: 'bottoms' }).page).toBe(1);
    expect(nextListParams(current, { sort: 'newest' }).page).toBe(1);
  });

  it('keeps the other filters when one changes', () => {
    expect(nextListParams(current, { category: 'bottoms' })).toEqual({
      ...current,
      category: 'bottoms',
      page: 1,
    });
  });

  it('changes only the page for a page change', () => {
    expect(nextListParams(current, { page: 5 })).toEqual({ ...current, page: 5 });
  });

  it('clearing search drops relevance sort', () => {
    const withRelevance: ProductListParams = { ...base, search: 'shirt', sort: 'relevance', page: 2 };

    const next = nextListParams(withRelevance, { search: '' });

    expect(next.search).toBeUndefined();
    expect(next.sort).toBeUndefined();
    expect(next.page).toBe(1);
  });

  it('clearing the category removes it', () => {
    expect(nextListParams(current, { category: undefined }).category).toBeUndefined();
  });

  it('does not reset the page when the same value is set again', () => {
    expect(nextListParams(current, { search: 'shirt' }).page).toBe(4);
    expect(nextListParams(current, { sort: 'price-asc' }).page).toBe(4);
  });
});

describe('showingRange', () => {
  function page(pageNumber: number, count: number): ProductListPage {
    const items = Array.from({ length: count }, () => ({}) as ProductListPage['items'][number]);
    return { items, page: pageNumber, pageSize: 8, totalCount: 12, totalPages: 2 };
  }

  it('covers a full first page', () => {
    expect(showingRange(page(1, 8))).toEqual({ first: 1, last: 8 });
  });

  it('covers a short last page', () => {
    expect(showingRange(page(2, 4))).toEqual({ first: 9, last: 12 });
  });

  it('covers a single item', () => {
    expect(showingRange(page(1, 1))).toEqual({ first: 1, last: 1 });
  });
});
