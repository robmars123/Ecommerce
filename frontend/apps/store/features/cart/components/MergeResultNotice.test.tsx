import { fireEvent, renderRouter, screen } from 'expo-router/testing-library';
import { Stack } from 'expo-router';
import { act } from '@testing-library/react-native';
import { Text } from 'react-native';

import { useGuestCartStore } from '../guest-cart-store';
import { useMergeResultStore } from '../merge-result-store';
import { GuestCartNotice } from './GuestCartNotice';
import { MergeResultNotice } from './MergeResultNotice';

afterEach(() => {
  useMergeResultStore.getState().dismiss();
  useGuestCartStore.setState({ lines: [], mergeKey: null });
});

const routes = (ui: () => React.JSX.Element) => ({
  _layout: () => <Stack screenOptions={{ headerShown: false }} />,
  index: ui,
  cart: () => <Text>Cart screen</Text>,
});

// F3: the merge result is shown, never dropped silently.
describe('MergeResultNotice', () => {
  it('lists every result with its outcome and reason, and Dismiss hides it', async () => {
    useMergeResultStore.getState().publish([
      { variantId: 'a', outcome: 'Kept', quantity: 5, reason: null, name: 'Blue Shirt - Medium' },
      { variantId: 'b', outcome: 'Capped', quantity: 4, reason: 'OutOfStock', name: 'Red Hat - One size' },
      { variantId: 'c', outcome: 'Removed', quantity: 0, reason: 'Unavailable', name: 'Old Scarf - One size' },
      { variantId: 'd', outcome: 'Capped', quantity: 10, reason: 'LimitReached', name: 'Mug - Blue' },
    ]);

    await renderRouter(routes(() => <MergeResultNotice />), { initialUrl: '/' });

    expect(screen.getByText('Blue Shirt - Medium: 5 in your cart')).toBeOnTheScreen();
    expect(screen.getByText('Red Hat - One size: reduced to 4 (not enough in stock)')).toBeOnTheScreen();
    expect(screen.getByText('Old Scarf - One size: not added (no longer available)')).toBeOnTheScreen();
    expect(screen.getByText('Mug - Blue: reduced to 10 (limit per item reached)')).toBeOnTheScreen();

    await fireEvent.press(screen.getByRole('button', { name: 'Dismiss' }));
    expect(screen.queryByText(/Blue Shirt - Medium/)).toBeNull();
  });

  it('View cart goes to /cart', async () => {
    useMergeResultStore.getState().publish([{ variantId: 'a', outcome: 'Kept', quantity: 1, reason: null, name: 'Blue Shirt - Medium' }]);
    await renderRouter(routes(() => <MergeResultNotice />), { initialUrl: '/' });

    await fireEvent.press(screen.getByRole('link', { name: 'View cart' }));

    expect(screen).toHavePathname('/cart');
  });

  it('a failure says the cart is still on this device and Try again calls the retry', async () => {
    const retry = jest.fn();
    useMergeResultStore.getState().fail(retry);
    await renderRouter(routes(() => <MergeResultNotice />), { initialUrl: '/' });

    expect(screen.getByText(/still saved on this device/)).toBeOnTheScreen();
    await fireEvent.press(screen.getByRole('button', { name: 'Try again' }));

    expect(retry).toHaveBeenCalledTimes(1);
  });

  it('shows nothing when there is no result', async () => {
    await renderRouter(routes(() => <MergeResultNotice />), { initialUrl: '/' });

    expect(screen.queryByRole('button', { name: 'Dismiss' })).toBeNull();
  });
});

describe('GuestCartNotice', () => {
  it('appears only while this device holds cart lines', async () => {
    await renderRouter(routes(() => <GuestCartNotice />), { initialUrl: '/' });
    expect(screen.queryByText(/will be added to your account/)).toBeNull();

    await act(async () => {
      useGuestCartStore.getState().add(
        { variantId: 'v', productSlug: 's', snapshot: { productName: 'P', variantName: 'V', price: { amount: '1.00', currency: 'USD' }, image: null } },
        1,
      );
    });

    expect(screen.getByText("Items in this device's cart will be added to your account.")).toBeOnTheScreen();
  });
});
