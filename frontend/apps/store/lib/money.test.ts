import { formatMoney } from './money';

describe('formatMoney', () => {
  it('formats the server amount for display without changing it', () => {
    expect(formatMoney({ amount: '24.50', currency: 'USD' })).toBe('$24.50');
    expect(formatMoney({ amount: '1234.5', currency: 'USD' })).toBe('$1,234.50');
    expect(formatMoney({ amount: '0.00', currency: 'USD' })).toBe('$0.00');
  });

  it.each(['abc', '', '  ', 'NaN', 'Infinity'])('falls back to "amount currency" for bad amount "%s"', (amount) => {
    expect(formatMoney({ amount, currency: 'USD' })).toBe(`${amount} USD`);
  });

  it('falls back when the currency code is not valid for Intl', () => {
    expect(formatMoney({ amount: '5.00', currency: 'not-a-code' })).toBe('5.00 not-a-code');
  });
});
