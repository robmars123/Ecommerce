// Wire types for the public catalog API. Money amounts are strings formatted by the server.

export type Money = { amount: string; currency: string };

export type StockStatus = 'Unknown' | 'InStock' | 'Low' | 'Out';

export type ProductSort = 'relevance' | 'price-asc' | 'price-desc' | 'newest';

export type CategoryRef = { slug: string; name: string };

export type ProductImage = { url: string | null; altText: string };

export type ProductListItem = {
  id: string;
  slug: string;
  name: string;
  category: CategoryRef;
  price: Money;
  compareAtPrice: Money | null;
  primaryImage: ProductImage | null;
  stockStatus: StockStatus;
};

export type ProductListPage = {
  items: ProductListItem[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
};

export type ProductVariant = {
  id: string;
  sku: string;
  name: string;
  size: string | null;
  colour: string | null;
  price: Money;
  compareAtPrice: Money | null;
  stockStatus: StockStatus;
};

export type ProductDetail = {
  id: string;
  slug: string;
  name: string;
  description: string;
  category: CategoryRef;
  images: ProductImage[];
  variants: ProductVariant[];
};

export type Category = { slug: string; name: string; parentSlug: string | null };

export type CategoryList = { items: Category[] };

export type ProductListParams = {
  search?: string;
  category?: string;
  sort?: ProductSort;
  page: number;
  pageSize: number;
};
