import { fireEvent, renderRouter, screen, within } from 'expo-router/testing-library';
import { Text } from 'react-native';

import { AdminShell } from '@/features/admin-shell/components/AdminShell';
import { ADMIN_NAV } from '@/features/admin-shell/nav';
import { resetSession } from '@/features/auth/session';
import { sidebarBreakpoint } from '@/lib/design-tokens';
import { makeTokens, signInAs } from '@/tests/session';

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

describe('AdminShell footer', () => {
  it('S13: wide, Admin footer shows name and role as one labelled group', async () => {
    await signInAs('Admin');
    await renderShell(1024);

    const footer = screen.getByLabelText('Signed in as Admin User, Admin');
    expect(footer).toBeTruthy();
    expect(screen.getByText('Admin User')).toBeTruthy();
    expect(screen.getByText('Admin')).toBeTruthy();
    expect(within(screen.getByLabelText('Admin navigation')).getByLabelText('Signed in as Admin User, Admin')).toBeTruthy();
  });

  it('S14: wide, Support footer shows the Support name and role, no Admin role text', async () => {
    await signInAs('Support');
    await renderShell(1024);

    expect(screen.getByText('Support User')).toBeTruthy();
    expect(screen.getByText('Support')).toBeTruthy();
    expect(screen.queryByText('Admin')).toBeNull();
  });

  it('S15: a 100-char name stays on one line and the sidebar keeps its width', async () => {
    const longName = 'N'.repeat(100);
    const tokens = makeTokens('Admin');
    await signInAs('Admin', { user: { ...tokens.user, displayName: longName } });
    await renderShell(1024);

    expect(screen.getByText(longName).props.numberOfLines).toBe(1);
    expect(screen.getByText('Admin').props.numberOfLines).toBe(1);
    const { className } = screen.getByLabelText('Admin navigation').props;
    expect(className).toContain('w-sidebar');
    expect(className.split(' ')).not.toContain('flex-1');
  });

  it.each([['blank', '   '], ['empty', '']])('S16: %s name falls back to the email', async (_case, displayName) => {
    const tokens = makeTokens('Admin');
    await signInAs('Admin', { user: { ...tokens.user, displayName } });
    await renderShell(1024);

    expect(screen.getByLabelText(`Signed in as ${tokens.user.email}, Admin`)).toBeTruthy();
    expect(screen.getByText(tokens.user.email)).toBeTruthy();
    const empties = screen.queryAllByText(/^\s*$/);
    expect(empties).toHaveLength(0);
  });

  it('S17: narrow, the footer appears only while the menu is open', async () => {
    await signInAs('Admin');
    await renderShell(390);

    expect(screen.queryByText('Admin User')).toBeNull();
    expect(screen.getByText('Store Admin')).toBeTruthy();

    await fireEvent.press(screen.getByRole('button', { name: 'Open menu' }));
    expect(screen.getByText('Admin User')).toBeTruthy();
    expect(screen.getByText('Admin')).toBeTruthy();

    const closers = screen.getAllByLabelText('Close menu');
    await fireEvent.press(closers[closers.length - 1]);
    expect(screen.queryByText('Admin User')).toBeNull();
  });

  it('S18: with no user there is no footer, brand and body still render', async () => {
    await renderShell(1024);

    expect(screen.queryByLabelText(/Signed in as/)).toBeNull();
    expect(screen.queryByText('Admin User')).toBeNull();
    expect(screen.getByText('Store Admin')).toBeTruthy();
    expect(screen.getByText('Body')).toBeTruthy();
  });
});

async function renderShellAt(width: number, url: string) {
  mockUseWindowDimensions.mockReturnValue({ width, height: 800, scale: 1, fontScale: 1 });
  const shell = () => <AdminShell><Text>Body</Text></AdminShell>;
  const stub = (name: string) => () => <Text>{`${name} page`}</Text>;
  return await renderRouter(
    {
      index: shell,
      'admin/index': shell,
      'admin/orders/index': shell,
      'admin/orders/[id]': shell,
      'admin/products/index': stub('Products'),
      'admin/products/new': shell,
      'admin/customers/[id]': shell,
    },
    { initialUrl: url },
  );
}

describe('AdminShell breadcrumb', () => {
  it('S19: wide, order detail shows linked ancestors and a plain current crumb, never the id', async () => {
    await signInAs('Admin');
    await renderShellAt(1024, '/admin/orders/abc-123');

    const crumbs = within(screen.getByLabelText('Breadcrumb'));
    expect(crumbs.getByRole('link', { name: 'Dashboard' })).toBeTruthy();
    expect(crumbs.getByRole('link', { name: 'Orders' })).toBeTruthy();
    expect(crumbs.getByText('Order details')).toBeTruthy();
    expect(crumbs.queryByRole('link', { name: 'Order details' })).toBeNull();
    expect(screen.queryByText(/abc-123/)).toBeNull();
    expect(screen.getByLabelText('Admin navigation')).toBeTruthy();
    expect(screen.getByText('Body')).toBeTruthy();
  });

  it('S20: wide, pressing the Orders crumb navigates and Orders becomes the current text', async () => {
    await signInAs('Admin');
    await renderShellAt(1024, '/admin/orders/abc-123');

    await fireEvent.press(within(screen.getByLabelText('Breadcrumb')).getByRole('link', { name: 'Orders' }));
    expect(screen).toHavePathname('/admin/orders');
    const crumbs = within(screen.getByLabelText('Breadcrumb'));
    expect(crumbs.getByRole('link', { name: 'Dashboard' })).toBeTruthy();
    expect(crumbs.queryByRole('link', { name: 'Orders' })).toBeNull();
    expect(crumbs.getByText('Orders')).toBeTruthy();
  });

  it('S21: wide, /admin shows only Dashboard and no links', async () => {
    await signInAs('Admin');
    await renderShellAt(1024, '/admin');

    const crumbs = within(screen.getByLabelText('Breadcrumb'));
    expect(crumbs.getByText('Dashboard')).toBeTruthy();
    expect(crumbs.queryAllByRole('link')).toHaveLength(0);
  });

  it('S22: narrow, the breadcrumb is visible with the menu closed and wraps', async () => {
    await signInAs('Admin');
    await renderShellAt(390, '/admin/customers/xyz');

    expect(screen.getByRole('button', { name: 'Open menu' })).toBeCollapsed();
    expect(screen.getByText('Store Admin')).toBeTruthy();
    expect(screen.queryByLabelText('Admin navigation')).toBeNull();
    const container = screen.getByLabelText('Breadcrumb');
    expect(container.props.className).toContain('flex-wrap');
    expect(within(container).getByText('Customer details').props.numberOfLines).toBe(1);
  });

  it('S23: narrow, pressing the Products crumb navigates and the overlay stays closed', async () => {
    await signInAs('Admin');
    await renderShellAt(390, '/admin/products/new');

    await fireEvent.press(within(screen.getByLabelText('Breadcrumb')).getByRole('link', { name: 'Products' }));
    expect(screen).toHavePathname('/admin/products');
    expect(screen.queryByLabelText('Admin navigation')).toBeNull();
  });

  it('S24: Support, wide, order detail shows the same crumbs', async () => {
    await signInAs('Support');
    await renderShellAt(1024, '/admin/orders/abc');

    const crumbs = within(screen.getByLabelText('Breadcrumb'));
    expect(crumbs.getByRole('link', { name: 'Dashboard' })).toBeTruthy();
    expect(crumbs.getByRole('link', { name: 'Orders' })).toBeTruthy();
    expect(crumbs.getByText('Order details')).toBeTruthy();
  });

  it('S25: a non-admin path renders no breadcrumb, brand and body still render', async () => {
    await signInAs('Admin');
    await renderShell(1024);

    expect(screen.queryByLabelText('Breadcrumb')).toBeNull();
    expect(screen.getByText('Store Admin')).toBeTruthy();
    expect(screen.getByText('Body')).toBeTruthy();
  });
});
