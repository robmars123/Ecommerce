import type { ProductListParams, ProductSort } from './types';

export const PRODUCT_PAGE_SIZE = 20;
export const MAX_SEARCH_LENGTH = 100;

const SORTS: readonly ProductSort[] = ['relevance', 'price-asc', 'price-desc', 'newest'];

type RawParams = Record<string, string | string[] | undefined>;
type ListChange = Partial<Pick<ProductListParams, 'search' | 'category' | 'sort' | 'page'>>;

function first(value: string | string[] | undefined): string | undefined {
  return Array.isArray(value) ? value[0] : value;
}

function parsePage(value: string | undefined): number {
  if (value === undefined || !/^\d+$/.test(value)) {
    return 1;
  }
  const page = Number(value);
  return Number.isSafeInteger(page) && page >= 1 && page <= 1_000_000 ? page : 1;
}

function cleanText(value: string | undefined, maxLength: number): string | undefined {
  const trimmed = value?.trim();
  return trimmed ? trimmed.slice(0, maxLength) : undefined;
}

/** Normalizes URL params into a request the server will accept. Never produces a 400 on its own. */
export function parseListParams(raw: RawParams): ProductListParams {
  const search = cleanText(first(raw.search), MAX_SEARCH_LENGTH);
  const category = cleanText(first(raw.category), 100);
  const sortRaw = first(raw.sort);
  const sort = SORTS.find((s) => s === sortRaw);
  const validSort = sort === 'relevance' && !search ? undefined : sort;

  return {
    ...(search ? { search } : {}),
    ...(category ? { category } : {}),
    ...(validSort ? { sort: validSort } : {}),
    page: parsePage(first(raw.page)),
    pageSize: PRODUCT_PAGE_SIZE,
  };
}

/** Applies one UI change. Changing search, category or sort resets to page 1; clearing search drops relevance. */
export function nextListParams(current: ProductListParams, change: ListChange): ProductListParams {
  const next: ProductListParams = { ...current };

  if ('search' in change) {
    const search = cleanText(change.search, MAX_SEARCH_LENGTH);
    if (search !== current.search) next.page = 1;
    if (search) next.search = search;
    else delete next.search;
  }
  if ('category' in change) {
    const category = cleanText(change.category, 100);
    if (category !== current.category) next.page = 1;
    if (category) next.category = category;
    else delete next.category;
  }
  if ('sort' in change) {
    if (change.sort !== current.sort) next.page = 1;
    if (change.sort) next.sort = change.sort;
    else delete next.sort;
  }
  if (next.sort === 'relevance' && !next.search) {
    delete next.sort;
  }
  if ('page' in change && change.page !== undefined) {
    next.page = change.page;
  }
  return next;
}
