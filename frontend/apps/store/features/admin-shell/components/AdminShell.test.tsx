import { fireEvent, renderRouter, screen } from 'expo-router/testing-library';
import { Text } from 'react-native';

import { AdminShell } from '@/features/admin-shell/components/AdminShell';
import { sidebarBreakpoint } from '@/lib/design-tokens';

const mockUseWindowDimensions = jest.fn();
jest.mock('react-native/Libraries/Utilities/useWindowDimensions', () => ({
  __esModule: true,
  default: () => mockUseWindowDimensions(),
}));

async function renderShell(width: number) {
  mockUseWindowDimensions.mockReturnValue({ width, height: 800, scale: 1, fontScale: 1 });
  return await renderRouter({ index: () => <AdminShell><Text>Body</Text></AdminShell> }, { initialUrl: '/' });
}

describe('AdminShell', () => {
  it('S1: wide shows the sidebar, brand and Dashboard link, no toggle', async () => {
    await renderShell(1024);

    expect(screen.getByLabelText('Admin navigation')).toBeTruthy();
    expect(screen.getByText('Store Admin')).toBeTruthy();
    expect(screen.getByText('Commerce Suite')).toBeTruthy();
    expect(screen.getByRole('link', { name: 'Dashboard' })).toBeTruthy();
    expect(screen.getByText('Body')).toBeTruthy();
    expect(screen.queryByRole('button', { name: 'Open menu' })).toBeNull();
  });

  it('S2: narrow shows the toggle collapsed and no sidebar', async () => {
    await renderShell(390);

    expect(screen.getByText('Body')).toBeTruthy();
    expect(screen.getByRole('button', { name: 'Open menu' })).toBeCollapsed();
    expect(screen.queryByLabelText('Admin navigation')).toBeNull();
  });

  it('S3: the toggle opens the sidebar and the panel close button closes it', async () => {
    await renderShell(390);

    await fireEvent.press(screen.getByRole('button', { name: 'Open menu' }));
    expect(screen.getByLabelText('Admin navigation')).toBeTruthy();
    expect(screen.getByRole('button', { name: 'Open menu' })).toBeExpanded();

    const closers = screen.getAllByLabelText('Close menu');
    await fireEvent.press(closers[closers.length - 1]);
    expect(screen.queryByLabelText('Admin navigation')).toBeNull();
  });

  it('S4: pressing the Dashboard link closes the overlay', async () => {
    await renderShell(390);

    await fireEvent.press(screen.getByRole('button', { name: 'Open menu' }));
    await fireEvent.press(screen.getByRole('link', { name: 'Dashboard' }));
    expect(screen.queryByLabelText('Admin navigation')).toBeNull();
  });

  it('S5: both close controls (backdrop and panel) close the overlay', async () => {
    await renderShell(390);

    for (const index of [0, 1]) {
      await fireEvent.press(screen.getByRole('button', { name: 'Open menu' }));
      const closers = screen.getAllByLabelText('Close menu');
      expect(closers).toHaveLength(2);
      await fireEvent.press(closers[index]);
      expect(screen.queryByLabelText('Admin navigation')).toBeNull();
    }
  });

  it('S6: the breakpoint decides between toggle and sidebar', async () => {
    const narrow = await renderShell(sidebarBreakpoint - 1);
    expect(screen.getByRole('button', { name: 'Open menu' })).toBeTruthy();
    expect(screen.queryByLabelText('Admin navigation')).toBeNull();
    await narrow.unmount();

    await renderShell(sidebarBreakpoint);
    expect(screen.getByLabelText('Admin navigation')).toBeTruthy();
    expect(screen.queryByRole('button', { name: 'Open menu' })).toBeNull();
  });

  it('S7: the overlay panel keeps its sidebar width (no flex grow that collapses it beside the scrim)', async () => {
    await renderShell(390);

    await fireEvent.press(screen.getByRole('button', { name: 'Open menu' }));
    const { className } = screen.getByLabelText('Admin navigation').props;
    expect(className).toContain('w-sidebar');
    expect(className.split(' ')).not.toContain('flex-1');
  });
});
