import { paymentTone, statusTone, totalsNote, type StatusTone } from './status-labels';
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

const TOTALS_NOTE_CANCELLED =
  'No payment is kept for this order. It was cancelled before payment was confirmed, and any payment that went through late is refunded automatically.';

describe('totalsNote', () => {
  const statuses: OrderStatus[] = ['PendingPayment', 'Paid', 'Processing', 'Shipped', 'Delivered', 'Cancelled', 'Refunded', 'Returned'];

  it.each(statuses)('U1 %s', (status) => {
    expect(totalsNote(status)).toBe(status === 'Cancelled' ? TOTALS_NOTE_CANCELLED : null);
  });

  it('U2 does not claim the order was never charged', () => {
    expect(totalsNote('Cancelled')).not.toMatch(/never charged|not charged/i);
  });
});
