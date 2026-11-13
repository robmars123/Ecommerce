import { fireEvent, render, screen } from '@testing-library/react-native';

import { ActionButton } from './ActionButton';

describe('ActionButton', () => {
  it('AP3: the pill look still disables and marks busy while pending, and does not fire', async () => {
    const onPress = jest.fn();
    await render(<ActionButton label="Refresh" onPress={onPress} pending pill />);

    const button = screen.getByRole('button', { name: 'Refresh' });
    expect(button).toBeDisabled();
    expect(button).toBeBusy();
    fireEvent.press(button);
    expect(onPress).not.toHaveBeenCalled();
  });

  it('AP3: the pill look fires when idle', async () => {
    const onPress = jest.fn();
    await render(<ActionButton label="Refresh" onPress={onPress} pill />);

    fireEvent.press(screen.getByRole('button', { name: 'Refresh' }));
    expect(onPress).toHaveBeenCalledTimes(1);
  });
});
