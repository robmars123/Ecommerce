import type { OrderStatus } from '@/features/orders/types';

import { actionsFor, validateFilters, validateReason, validateShip } from './order-actions';

describe('actionsFor', () => {
  const expected: Record<OrderStatus, string[]> = {
    PendingPayment: [],
    Paid: ['process', 'override-processing'],
    Processing: ['ship'],
    Shipped: ['deliver', 'override-delivered'],
    Delivered: [],
    Cancelled: [],
    Refunded: [],
    Returned: [],
  };

  it.each(Object.keys(expected) as OrderStatus[])('offers the planned buttons for %s', (status) => {
    expect(actionsFor(status)).toEqual(expected[status]);
  });
});

describe('validateFilters', () => {
  it('accepts empty and real dates', () => {
    expect(validateFilters({ from: '', to: '' })).toEqual({});
    expect(validateFilters({ from: '2026-03-01', to: '2026-03-01' })).toEqual({});
  });

  it('rejects a malformed date, Feb 30 and from after to', () => {
    expect(validateFilters({ from: '03/01/2026', to: '' }).from).toBeDefined();
    expect(validateFilters({ from: '', to: '2026-02-30' }).to).toBeDefined();
    expect(validateFilters({ from: '2026-03-02', to: '2026-03-01' }).from).toBeDefined();
  });
});

describe('validateShip and validateReason', () => {
  it('requires a carrier and a tracking number of 1 to 64 characters', () => {
    expect(validateShip('', 'T1').carrier).toBeDefined();
    expect(validateShip('c', '  ').tracking).toBeDefined();
    expect(validateShip('c', 'x'.repeat(65)).tracking).toBeDefined();
    expect(validateShip('c', 'x'.repeat(64))).toEqual({});
  });

  it('requires a reason of 1 to 500 characters', () => {
    expect(validateReason(' ')).toBeDefined();
    expect(validateReason('x'.repeat(501))).toBeDefined();
    expect(validateReason('x'.repeat(500))).toBeUndefined();
  });
});
