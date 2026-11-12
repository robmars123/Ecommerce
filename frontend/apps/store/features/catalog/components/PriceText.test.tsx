import { render, screen } from '@testing-library/react-native';

import { PriceText } from './PriceText';

describe('PriceText', () => {
  it('formats the server price', async () => {
    await render(<PriceText price={{ amount: '24.50', currency: 'USD' }} />);
    expect(screen.getByText('$24.50')).toBeOnTheScreen();
    expect(screen.queryByLabelText(/^Was /)).toBeNull();
  });

  it('shows the compare-at price struck through with an accessible "Was" label', async () => {
    await render(<PriceText price={{ amount: '24.50', currency: 'USD' }} compareAtPrice={{ amount: '30.00', currency: 'USD' }} />);
    const was = screen.getByLabelText('Was $30.00');
    expect(was).toBeOnTheScreen();
    expect(was).toHaveStyle({ textDecorationLine: 'line-through' });
  });
});
