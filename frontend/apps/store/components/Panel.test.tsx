import { render, screen } from '@testing-library/react-native';
import { Text } from 'react-native';

import { Panel } from './Panel';

describe('Panel', () => {
  it('A1 shows its title as a header above the children', async () => {
    await render(
      <Panel title="Items">
        <Text>Body</Text>
      </Panel>,
    );

    expect(screen.getByRole('header', { name: 'Items' })).toBeOnTheScreen();
    expect(screen.getByText('Body')).toBeOnTheScreen();
  });

  it('A2 shows no header without a title', async () => {
    await render(
      <Panel>
        <Text>Body</Text>
      </Panel>,
    );

    expect(screen.queryByRole('header')).toBeNull();
    expect(screen.getByText('Body')).toBeOnTheScreen();
  });

  it('A4 shows the subtitle beside the title', async () => {
    await render(
      <Panel title="Timeline" subtitle="Newest last">
        <Text>Body</Text>
      </Panel>,
    );

    expect(screen.getByRole('header', { name: 'Timeline' })).toBeOnTheScreen();
    expect(screen.getByText('Newest last')).toBeOnTheScreen();
  });
});
