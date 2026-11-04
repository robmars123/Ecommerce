import { render, screen } from '@testing-library/react-native';

import { StockBadge } from './StockBadge';

describe('StockBadge', () => {
  it('renders nothing for Unknown', async () => {
    await render(<StockBadge status="Unknown" />);
    expect(screen.queryByText(/stock/i)).toBeNull();
    expect(screen.toJSON()).toBeNull();
  });

  it.each([
    ['InStock', 'In stock'],
    ['Low', 'Low stock'],
    ['Out', 'Out of stock'],
  ] as const)('renders text for %s', async (status, text) => {
    await render(<StockBadge status={status} />);
    expect(screen.getByText(text)).toBeOnTheScreen();
  });
});
