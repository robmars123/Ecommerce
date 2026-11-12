import { act, render, screen } from '@testing-library/react-native';
import { Text } from 'react-native';

import { useThemeStore } from '@/lib/theme';

import { ThemeRoot } from './ThemeRoot';

const mockStatusBar = jest.fn((_props: { style?: string }) => null);
jest.mock('expo-status-bar', () => ({ StatusBar: (props: { style?: string }) => mockStatusBar(props) }));

describe('ThemeRoot', () => {
  beforeEach(() => {
    mockStatusBar.mockClear();
    useThemeStore.setState({ mode: 'dark' });
  });

  it('TR1: renders children; the status bar is light on dark and dark on light', async () => {
    await render(
      <ThemeRoot>
        <Text>Inside</Text>
      </ThemeRoot>,
    );
    expect(screen.getByText('Inside')).toBeOnTheScreen();
    expect(mockStatusBar).toHaveBeenLastCalledWith({ style: 'light' });

    await act(async () => useThemeStore.getState().toggle());
    expect(mockStatusBar).toHaveBeenLastCalledWith({ style: 'dark' });
  });
});
