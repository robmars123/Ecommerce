import { render, screen } from '@testing-library/react-native';
import { Text } from 'react-native';

import { AdminPanel } from './AdminPanel';

describe('AdminPanel', () => {
  it('A1 shows its title as a header above the children', async () => {
    await render(
      <AdminPanel title="Items">
        <Text>Body</Text>
      </AdminPanel>,
    );

    expect(screen.getByRole('header', { name: 'Items' })).toBeOnTheScreen();
    expect(screen.getByText('Body')).toBeOnTheScreen();
  });

  it('A2 shows no header without a title', async () => {
    await render(
      <AdminPanel>
        <Text>Body</Text>
      </AdminPanel>,
    );

    expect(screen.queryByRole('header')).toBeNull();
    expect(screen.getByText('Body')).toBeOnTheScreen();
  });
});
