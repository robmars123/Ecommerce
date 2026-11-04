import { fireEvent, render, screen } from '@testing-library/react-native';

import { EmptyState } from './EmptyState';

describe('EmptyState', () => {
  it('shows the message and no button without an action', async () => {
    await render(<EmptyState message="No products match your search." />);

    expect(screen.getByText('No products match your search.')).toBeOnTheScreen();
    expect(screen.queryByRole('button')).toBeNull();
  });

  it('runs the action when its button is pressed', async () => {
    const onPress = jest.fn();
    await render(<EmptyState message="Nothing" action={{ label: 'Clear filters', onPress }} />);

    await fireEvent.press(screen.getByRole('button', { name: 'Clear filters' }));

    expect(onPress).toHaveBeenCalledTimes(1);
  });
});
