import { act, render, screen } from '@testing-library/react-native';

import { LiveStorePill } from '@/features/admin-shell/components/LiveStorePill';
import { subscribeToConnectionState } from '@/lib/realtime';

const mockSubscribe = subscribeToConnectionState as jest.MockedFunction<typeof subscribeToConnectionState>;

describe('LiveStorePill', () => {
  it('P1: says connected or offline in text, not only colour', async () => {
    const unsubscribe = jest.fn();
    let push: (connected: boolean) => void = () => undefined;
    mockSubscribe.mockImplementationOnce((listener) => {
      push = listener;
      return unsubscribe;
    });

    await render(<LiveStorePill />);
    expect(screen.getByLabelText('Live store offline')).toBeOnTheScreen();
    expect(screen.getByText('Live store · offline')).toBeOnTheScreen();

    await act(() => push(true));
    expect(screen.getByLabelText('Live store connected')).toBeOnTheScreen();
    expect(screen.getByText('Live store')).toBeOnTheScreen();

    await act(() => push(false));
    expect(screen.getByLabelText('Live store offline')).toBeOnTheScreen();
  });

  it('P2: unmounting calls the unsubscribe function', async () => {
    const unsubscribe = jest.fn();
    mockSubscribe.mockImplementationOnce(() => unsubscribe);

    const view = await render(<LiveStorePill />);
    expect(unsubscribe).not.toHaveBeenCalled();
    await view.unmount();

    expect(unsubscribe).toHaveBeenCalledTimes(1);
  });
});
