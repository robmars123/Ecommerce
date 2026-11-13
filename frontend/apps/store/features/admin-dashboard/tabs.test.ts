import { DASHBOARD_TABS, parseDashboardTab } from './tabs';

describe('dashboard tabs', () => {
  it.each([undefined, '', 'x', 'Stories'])('T1: %p is Overview', (raw) => {
    expect(parseDashboardTab(raw)).toBe('overview');
  });

  it('T1: stories, criteria and the first value of an array are parsed', () => {
    expect(parseDashboardTab('stories')).toBe('stories');
    expect(parseDashboardTab('criteria')).toBe('criteria');
    expect(parseDashboardTab(['criteria', 'x'])).toBe('criteria');
  });

  it('T2: the labels and their order', () => {
    expect(DASHBOARD_TABS.map((t) => t.label)).toEqual(['Overview', 'User Stories', 'Acceptance Criteria']);
    expect(DASHBOARD_TABS.map((t) => t.tab)).toEqual(['overview', 'stories', 'criteria']);
  });
});
