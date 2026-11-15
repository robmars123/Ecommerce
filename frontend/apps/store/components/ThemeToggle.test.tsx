import { act, fireEvent, render, screen } from '@testing-library/react-native';

import { useThemeStore } from '@/lib/theme';

import { ThemeToggle } from './ThemeToggle';

describe('ThemeToggle', () => {
  beforeEach(() => {
    useThemeStore.setState({ mode: 'dark' });
  });

  it('TG1: in Dark shows a checked Dark mode switch labelled Light', async () => {
    await render(<ThemeToggle />);
    const toggle = screen.getByRole('switch', { name: 'Dark mode' });
    expect(toggle).toBeChecked();
    expect(screen.getByText('Light')).toBeOnTheScreen();
    expect(screen.queryByText('Dark')).toBeNull();
  });

  it('TG2: pressing it flips to light and back', async () => {
    await render(<ThemeToggle />);
    await fireEvent.press(screen.getByRole('switch', { name: 'Dark mode' }));
    expect(screen.getByRole('switch', { name: 'Dark mode' })).not.toBeChecked();
    expect(screen.getByText('Dark')).toBeOnTheScreen();
    expect(useThemeStore.getState().mode).toBe('light');

    await fireEvent.press(screen.getByRole('switch', { name: 'Dark mode' }));
    expect(screen.getByRole('switch', { name: 'Dark mode' })).toBeChecked();
    expect(screen.getByText('Light')).toBeOnTheScreen();
  });
});
