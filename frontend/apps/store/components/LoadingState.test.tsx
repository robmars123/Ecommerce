import { act, render, screen } from '@testing-library/react-native';

import { themes } from '@/lib/design-tokens';
import { useThemeStore } from '@/lib/theme';

import { LoadingState } from './LoadingState';

describe('LoadingState', () => {
  beforeEach(() => {
    useThemeStore.setState({ mode: 'dark' });
  });

  it('TL1: the spinner colour follows the theme', async () => {
    await render(<LoadingState />);
    expect(screen.getByLabelText('Loading').props.color).toBe(themes.dark.primary);

    await act(async () => useThemeStore.getState().toggle());
    expect(screen.getByLabelText('Loading').props.color).toBe(themes.light.primary);
  });
});
