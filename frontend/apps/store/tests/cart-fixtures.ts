import type { Cart, CartLine } from '@/features/cart/types';

export const usd = (amount: string) => ({ amount, currency: 'USD' });

export function makeCartLine(overrides: Partial<CartLine> = {}): CartLine {
  return {
    variantId: 'v-1',
    productSlug: 'blue-shirt',
    productName: 'Blue Shirt',
    variantName: 'Medium',
    image: null,
    quantity: 2,
    unitPrice: usd('24.50'),
    lineTotal: usd('49.00'),
    stockStatus: 'InStock',
    issues: [],
    ...overrides,
  };
}

export function makeCart(items: CartLine[] = [makeCartLine()], subtotal = '49.00'): Cart {
  return { items, subtotal: usd(subtotal) };
}
