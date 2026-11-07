import { estimateTotal } from './estimate';

const usd = (amount: string) => ({ amount, currency: 'USD' });

describe('estimateTotal', () => {
  it('adds the subtotal and the shipping price exactly, in cents', () => {
    expect(estimateTotal(usd('49.00'), usd('5.00'))).toEqual(usd('54.00'));
    expect(estimateTotal(usd('0.10'), usd('0.20'))).toEqual(usd('0.30'));
    expect(estimateTotal(usd('10.05'), usd('5'))).toEqual(usd('15.05'));
    expect(estimateTotal(usd('1234.5'), usd('0.07'))).toEqual(usd('1234.57'));
  });

  it('gives no estimate for different currencies or an unreadable amount', () => {
    expect(estimateTotal(usd('49.00'), { amount: '5.00', currency: 'EUR' })).toBeNull();
    expect(estimateTotal(usd('abc'), usd('5.00'))).toBeNull();
    expect(estimateTotal(usd('49.00'), usd(''))).toBeNull();
  });
});
