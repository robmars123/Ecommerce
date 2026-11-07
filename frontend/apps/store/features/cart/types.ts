// Wire types for the cart API (arch 9, Cart contract). Money strings are formatted by the server; the client never adds them up.
import type { Money, StockStatus } from '@/features/catalog/types';

export type CartIssue = 'PriceChanged' | 'ExceedsStock' | 'Unavailable';

export type CartImage = { url: string; altText: string };

export type CartLine = {
  variantId: string;
  productSlug: string;
  productName: string;
  variantName: string;
  image: CartImage | null;
  quantity: number;
  unitPrice: Money;
  lineTotal: Money;
  stockStatus: StockStatus;
  issues: CartIssue[];
};

export type Cart = { items: CartLine[]; subtotal: Money };

export type MergeOutcome = 'Kept' | 'Capped' | 'Removed';
export type MergeReason = 'Unavailable' | 'OutOfStock' | 'LimitReached';

export type MergeLineResult = {
  variantId: string;
  outcome: MergeOutcome;
  quantity: number;
  reason: MergeReason | null;
};

export type MergeResponse = { cart: Cart; results: MergeLineResult[] };

export type MergeLineInput = { variantId: string; quantity: number };

/** A device-cart line. The snapshot is an estimate for display only; the merge re-checks everything. */
export type GuestLine = {
  variantId: string;
  productSlug: string;
  quantity: number;
  snapshot: { productName: string; variantName: string; price: Money; image: CartImage | null };
};
