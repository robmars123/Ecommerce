import { fireEvent, render, screen } from '@testing-library/react-native';

import { ErrorState } from './ErrorState';

describe('ErrorState', () => {
  it('announces the message as an alert', async () => {
    await render(<ErrorState message="Something went wrong on our side. Please try again." />);

    expect(screen.getByRole('alert')).toBeOnTheScreen();
    expect(screen.getByText('Something went wrong on our side. Please try again.')).toBeOnTheScreen();
  });

  it('has no retry button without onRetry', async () => {
    await render(<ErrorState message="x" />);
    expect(screen.queryByRole('button', { name: 'Try again' })).toBeNull();
  });

  it('calls onRetry when Try again is pressed, with a 44pt target', async () => {
    const onRetry = jest.fn();
    await render(<ErrorState message="x" onRetry={onRetry} />);

    const button = screen.getByRole('button', { name: 'Try again' });
    expect(button).toHaveStyle({ minHeight: 44 });
    await fireEvent.press(button);

    expect(onRetry).toHaveBeenCalledTimes(1);
  });
});
