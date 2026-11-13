import { fireEvent, screen, waitFor, within } from 'expo-router/testing-library';

import AdminIndexScreen from '@/app/admin/index';
import { DEFINITION_OF_DONE, storyStats, USER_STORIES } from '@/features/admin-dashboard/stories';
import { resetSession } from '@/features/auth/session';
import { makeDashboard, makeRecentOrder, makeRecentOrdersPage } from '@/tests/admin-dashboard-fixtures';
import { calledUrls, jsonResponse } from '@/tests/fetch-mock';
import { renderRouterWithQuery } from '@/tests/render-with-query';
import { signInAs } from '@/tests/session';

jest.mock('expo-secure-store');

const mockUseWindowDimensions = jest.fn();
jest.mock('react-native/Libraries/Utilities/useWindowDimensions', () => ({
  __esModule: true,
  default: () => mockUseWindowDimensions(),
}));

beforeEach(() => {
  process.env.EXPO_PUBLIC_API_BASE_URL = 'http://api.test.invalid:5080';
  mockUseWindowDimensions.mockReturnValue({ width: 1024, height: 800, scale: 1, fontScale: 1 });
});

afterEach(() => {
  resetSession();
  jest.restoreAllMocks();
  delete process.env.EXPO_PUBLIC_API_BASE_URL;
});

// A small route map, not the whole app: the screen under test and nothing else.
const render = (initialUrl: string) => renderRouterWithQuery({ 'admin/index': AdminIndexScreen }, { initialUrl });

function mockApi() {
  return jest.spyOn(globalThis, 'fetch').mockImplementation(async (input) => {
    const { pathname } = new URL(String(input));
    if (pathname === '/api/v1/admin/dashboard') return jsonResponse(200, makeDashboard());
    if (pathname === '/api/v1/admin/dashboard/recent-orders') return jsonResponse(200, makeRecentOrdersPage([makeRecentOrder()]));
    return new Response('not mocked', { status: 599 });
  });
}

const dashboardCalls = (spy: jest.SpyInstance) => calledUrls(spy).filter((u) => u.includes('/api/v1/admin/dashboard'));

describe('Dashboard tabs', () => {
  it('DT1: /admin opens Overview, selected, with the dashboard figures', async () => {
    await signInAs('Admin');
    mockApi();

    await render('/admin');

    expect(await screen.findByLabelText('Revenue')).toBeOnTheScreen();
    expect(screen.getByRole('tab', { name: 'Overview' })).toBeSelected();
    expect(screen.getByRole('tab', { name: 'User Stories' })).not.toBeSelected();
    expect(screen.getByRole('tab', { name: 'Acceptance Criteria' })).not.toBeSelected();
  });

  it('DT2: pressing User Stories puts tab=stories in the URL and shows the 8 cards and the hero stats', async () => {
    await signInAs('Admin');
    mockApi();
    await render('/admin');
    await screen.findByLabelText('Revenue');

    await fireEvent.press(screen.getByRole('tab', { name: 'User Stories' }));

    await waitFor(() => expect(screen).toHaveSearchParams({ tab: 'stories' }));
    expect(screen.getAllByLabelText(/^US-0\d /)).toHaveLength(8);
    for (const story of USER_STORIES) {
      expect(screen.getByLabelText(`${story.id} ${story.title}`)).toBeOnTheScreen();
    }
    expect(within(screen.getByLabelText('Stories')).getByText('8')).toBeOnTheScreen();
    expect(within(screen.getByLabelText('Criteria')).getByText(String(storyStats(USER_STORIES).criteria))).toBeOnTheScreen();
    expect(screen.getByRole('tab', { name: 'User Stories' })).toBeSelected();
  });

  it('DT2: a fresh /admin?tab=stories render, and switching to Criteria, fetch no dashboard data', async () => {
    await signInAs('Admin');
    const spy = mockApi();

    await render('/admin?tab=stories');
    expect(await screen.findByLabelText('US-01 Admin shell and role-based navigation')).toBeOnTheScreen();
    await fireEvent.press(screen.getByRole('tab', { name: 'Acceptance Criteria' }));
    await waitFor(() => expect(screen).toHaveSearchParams({ tab: 'criteria' }));

    expect(dashboardCalls(spy)).toEqual([]);
  });

  it('DT3: /admin?tab=criteria lists every story heading with its criteria and the Definition of Done', async () => {
    await signInAs('Admin');
    mockApi();

    await render('/admin?tab=criteria');

    for (const story of USER_STORIES) {
      expect(await screen.findByText(`${story.id} · ${story.title}`)).toBeOnTheScreen();
      for (const criterion of story.criteria) expect(screen.getByText(criterion)).toBeOnTheScreen();
    }
    expect(screen.getByText('Definition of Done')).toBeOnTheScreen();
    for (const rule of DEFINITION_OF_DONE) expect(screen.getByText(rule)).toBeOnTheScreen();
  });

  it('DT4: /admin?period=7d, User Stories, then Overview keeps period=7d', async () => {
    await signInAs('Admin');
    mockApi();
    await render('/admin?period=7d');
    await screen.findByLabelText('Revenue');

    await fireEvent.press(screen.getByRole('tab', { name: 'User Stories' }));
    await waitFor(() => expect(screen).toHaveSearchParams({ period: '7d', tab: 'stories' }));
    await fireEvent.press(screen.getByRole('tab', { name: 'Overview' }));

    await waitFor(() => expect(screen).toHaveSearchParams({ period: '7d' }));
    expect(await screen.findByLabelText('Revenue')).toBeOnTheScreen();
  });

  it('DT5: an unknown tab is Overview', async () => {
    await signInAs('Admin');
    mockApi();

    await render('/admin?tab=bogus');

    expect(await screen.findByLabelText('Revenue')).toBeOnTheScreen();
    expect(screen.getByRole('tab', { name: 'Overview' })).toBeSelected();
  });

  it('DT6: Support sees the stories (static, no role data)', async () => {
    await signInAs('Support');
    const spy = mockApi();

    await render('/admin?tab=stories');

    expect(await screen.findAllByLabelText(/^US-0\d /)).toHaveLength(8);
    expect(dashboardCalls(spy)).toEqual([]);
  });

  it('DT7: at 390 px each story card takes a full row (one column)', async () => {
    await signInAs('Admin');
    mockApi();
    mockUseWindowDimensions.mockReturnValue({ width: 390, height: 800, scale: 1, fontScale: 1 });

    await render('/admin?tab=stories');

    const card = await screen.findByLabelText('US-01 Admin shell and role-based navigation');
    expect(card.parent?.props.className).toContain('basis-full');
    expect(card.parent?.props.className).not.toContain('basis-[45%]');
  });

  it('DT7: at 1024 px the cards sit in two columns', async () => {
    await signInAs('Admin');
    mockApi();

    await render('/admin?tab=stories');

    const card = await screen.findByLabelText('US-01 Admin shell and role-based navigation');
    expect(card.parent?.props.className).toContain('basis-[45%]');
  });
});
