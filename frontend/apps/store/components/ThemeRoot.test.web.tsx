// Web Jest project only: renders into jsdom with react-dom (RNTL cannot render react-native-web output).
import { act } from 'react';
import { createRoot, type Root } from 'react-dom/client';

import { themes } from '@/lib/design-tokens';
import { useThemeStore } from '@/lib/theme';

import { ThemeRoot } from './ThemeRoot';

jest.mock('expo-status-bar', () => ({ StatusBar: () => null }));

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

let container: HTMLElement;
let root: Root;

beforeEach(() => {
  useThemeStore.setState({ mode: 'dark' });
  container = document.createElement('div');
  document.body.appendChild(container);
  root = createRoot(container);
});

afterEach(() => {
  act(() => root.unmount());
  container.remove();
});

function rootBackground(): string {
  return (container.firstElementChild as HTMLElement).style.getPropertyValue('--color-background');
}

describe('ThemeRoot (web)', () => {
  it('TR2: the root view carries the --color-background variable of the current mode', async () => {
    await act(async () => root.render(<ThemeRoot>{null}</ThemeRoot>));
    expect(rootBackground()).toBe(themes.dark.background);

    await act(async () => useThemeStore.getState().toggle());
    expect(rootBackground()).toBe(themes.light.background);
  });
});
