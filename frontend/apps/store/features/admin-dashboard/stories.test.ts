import { DEFINITION_OF_DONE, storyStats, USER_STORIES } from './stories';

const NOT_SHIPPED = new Set(['P2.9', 'P2.18', 'P2.19', 'P2.20', ...Array.from({ length: 8 }, (_, i) => `P2.${48 + i}`)]);

describe('user stories', () => {
  it('S1: ids are exactly US-01 to US-08 in order', () => {
    expect(USER_STORIES.map((s) => s.id)).toEqual(Array.from({ length: 8 }, (_, i) => `US-0${i + 1}`));
  });

  it('S2: every story has a title, a quote, tracker rows and at least 2 criteria', () => {
    for (const story of USER_STORIES) {
      expect(story.title.trim()).not.toBe('');
      expect(story.quote.trim()).not.toBe('');
      expect(story.rows.length).toBeGreaterThanOrEqual(1);
      for (const row of story.rows) expect(row).toMatch(/^P2\.\d+$/);
      expect(story.criteria.length).toBeGreaterThanOrEqual(2);
    }
  });

  it('S3: no story cites a row twice, and none cites unshipped work', () => {
    for (const story of USER_STORIES) {
      expect(new Set(story.rows).size).toBe(story.rows.length);
      for (const row of story.rows) expect(NOT_SHIPPED.has(row)).toBe(false);
    }
  });

  it('S4: storyStats totals the stories and the criteria', () => {
    expect(storyStats(USER_STORIES)).toEqual({
      stories: 8,
      criteria: USER_STORIES.reduce((total, s) => total + s.criteria.length, 0),
    });
    expect(storyStats([])).toEqual({ stories: 0, criteria: 0 });
  });

  it('S5: the Definition of Done is not empty', () => {
    expect(DEFINITION_OF_DONE.length).toBeGreaterThan(0);
  });
});
