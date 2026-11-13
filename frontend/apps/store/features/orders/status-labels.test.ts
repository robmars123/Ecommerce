import { paymentTone, statusTone, type StatusTone } from './status-labels';
import type { OrderStatus, PaymentStatus } from './types';

describe('statusTone', () => {
  it.each<[OrderStatus, StatusTone]>([
    ['PendingPayment', 'warning'],
    ['Paid', 'neutral'],
    ['Processing', 'neutral'],
    ['Shipped', 'neutral'],
    ['Delivered', 'success'],
    ['Cancelled', 'danger'],
    ['Refunded', 'neutral'],
    ['Returned', 'neutral'],
  ])('O1 %s is %s', (status, tone) => {
    expect(statusTone(status)).toBe(tone);
  });

  it('O1 Cancelled is danger', () => {
    expect(statusTone('Cancelled')).toBe('danger');
  });
});

describe('paymentTone', () => {
  it.each<[PaymentStatus, StatusTone]>([
    ['Pending', 'warning'],
    ['Succeeded', 'success'],
    ['Failed', 'danger'],
    ['PartiallyRefunded', 'neutral'],
    ['Refunded', 'neutral'],
  ])('O6 %s is %s', (status, tone) => {
    expect(paymentTone(status)).toBe(tone);
  });
});
