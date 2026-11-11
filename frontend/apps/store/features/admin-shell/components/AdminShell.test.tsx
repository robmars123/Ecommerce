import { fireEvent, renderRouter, screen } from 'expo-router/testing-library';
import { Text } from 'react-native';

import { AdminShell } from '@/features/admin-shell/components/AdminShell';
import { ADMIN_NAV } from '@/features/admin-shell/nav';
import { resetSession } from '@/features/auth/session';
import { sidebarBreakpoint } from '@/lib/design-tokens';
import { signInAs } from '@/tests/session';

jest.mock('expo-secure-store');

const mockUseWindowDimensions = jest.fn();
jest.mock('react-native/Libraries/Utilities/useWindowDimensions', () => ({
  __esModule: true,
  default: () => mockUseWindowDimensions(),
}));

async function renderShell(width: number) {
  mockUseWindowDimensions.mockReturnValue({ width, height: 800, scale: 1, fontScale: 1 });
  return await renderRouter({ index: () => <AdminShell><Text>Body</Text></AdminShell> }, { initialUrl: '/' });
}

async function renderShellWithRoutes(width: number) {
  mockUseWindowDimensions.mockReturnValue({ width, height: 800, scale: 1, fontScale: 1 });
  const stub = (name: string) => () => <Text>{`${name} page`}</Text>;
  return await renderRouter(
    {
      index: () => <AdminShell><Text>Body</Text></AdminShell>,
      'admin/index': stub('Admin'),
      'admin/orders': stub('Orders'),
      'admin/products': stub('Products'),
      'admin/categories': stub('Categories'),
      'admin/inventory': stub('Inventory'),
      'admin/returns': stub('Returns'),
      'admin/customers': stub('Customers'),
    },
    { initialUrl: '/' },
  );
}

const readyItems = ADMIN_NAV.flatMap((group) => group.items).filter((item) => item.ready);
const notReadyLabels = ['User Stories', 'Analytics', 'Settings'];

afterEach(() => resetSession());

describe('AdminShell', () => {
  beforeEach(() => signInAs('Admin'));

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

  it('S8: Admin sees the three headers in order and 7 working links', async () => {
    const first = await renderShellWithRoutes(1024);

    expect(screen.getAllByRole('header').map((h) => h.props.children)).toEqual(['Overview', 'Manage', 'Insights']);
    expect(screen.getAllByRole('link')).toHaveLength(7);
    await first.unmount();

    // The shell sits on the index route in this harness, so each link is pressed on a fresh render.
    for (const item of readyItems) {
      const rendered = await renderShellWithRoutes(1024);
      await fireEvent.press(screen.getByRole('link', { name: item.label }));
      expect(screen).toHavePathname(item.href);
      await rendered.unmount();
    }
  });

  it('S9: Support sees Dashboard, Orders, Customers links and no admin-only items', async () => {
    await resetSession();
    await signInAs('Support');
    await renderShellWithRoutes(1024);

    for (const name of ['Dashboard', 'Orders', 'Customers']) {
      expect(screen.getByRole('link', { name })).toBeTruthy();
    }
    expect(screen.getAllByRole('link')).toHaveLength(3);
    expect(screen.getByText('User Stories')).toBeTruthy();
    for (const text of ['Products', 'Categories', 'Inventory', 'Returns', 'Analytics', 'Settings', 'Insights']) {
      expect(screen.queryByText(text)).toBeNull();
    }
  });

  it('S10: not-ready items are disabled, not links, and pressing leaves the path unchanged', async () => {
    await renderShellWithRoutes(1024);

    for (const label of notReadyLabels) {
      expect(screen.queryByRole('link', { name: label })).toBeNull();
      const item = screen.getByLabelText(label);
      expect(item).toBeDisabled();
      await fireEvent.press(item);
      expect(screen).toHavePathname('/');
    }
  });

  it('S11: narrow, pressing Orders navigates and closes the overlay', async () => {
    await renderShellWithRoutes(390);

    await fireEvent.press(screen.getByRole('button', { name: 'Open menu' }));
    await fireEvent.press(screen.getByRole('link', { name: 'Orders' }));
    expect(screen).toHavePathname('/admin/orders');
    expect(screen.queryByLabelText('Admin navigation')).toBeNull();
  });

  it('S12: a Customer session renders no nav items or headers, brand still renders', async () => {
    await resetSession();
    await signInAs('Customer');
    await renderShellWithRoutes(1024);

    expect(screen.queryAllByRole('link')).toHaveLength(0);
    expect(screen.queryAllByRole('header')).toHaveLength(0);
    expect(screen.getByText('Store Admin')).toBeTruthy();
  });
});
