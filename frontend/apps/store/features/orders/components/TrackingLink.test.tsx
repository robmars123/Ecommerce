import { fireEvent, render, screen } from '@testing-library/react-native';
import { Linking } from 'react-native';

import { TrackingLink } from './TrackingLink';

afterEach(() => {
  jest.restoreAllMocks();
});

describe('TrackingLink', () => {
  it('renders an https URL as a link and opens it', async () => {
    const open = jest.spyOn(Linking, 'openURL').mockResolvedValue();
    await render(<TrackingLink trackingNumber="T-1" trackingUrl="https://tracking.example.com/T-1" />);

    await fireEvent.press(screen.getByRole('link', { name: 'Track shipment' }));

    expect(open).toHaveBeenCalledWith('https://tracking.example.com/T-1');
  });

  it.each([['http://tracking.example.com/T-1'], ['javascript:alert(1)'], ['//evil.test/x'], [null]])(
    'shows only the plain tracking number for %p',
    async (url) => {
      await render(<TrackingLink trackingNumber="T-1" trackingUrl={url} />);

      expect(screen.getByText('T-1')).toBeOnTheScreen();
      expect(screen.queryByRole('link')).toBeNull();
    },
  );
});
