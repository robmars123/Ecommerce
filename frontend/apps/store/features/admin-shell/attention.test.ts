import { attentionItems } from '@/features/admin-shell/attention';
import { makeDashboard } from '@/tests/admin-dashboard-fixtures';

const busy = makeDashboard({
  lowStockCount: 2,
  cancelledCount: 1,
  needsAttention: {
    pendingReturns: 3,
    deadLetters: [
      { consumer: 'a', count: 2 },
      { consumer: 'b', count: 3 },
    ],
    laggingProjectors: [
      { name: 'p', lagSeconds: 75 },
      { name: 'q', lagSeconds: 130 },
    ],
  },
});

describe('attentionItems', () => {
  it('A1: Admin sees five items in order, links on low stock and returns only', () => {
    const items = attentionItems(busy, 'Admin');

    expect(items.map((i) => i.text)).toEqual([
      '2 low-stock variants',
      '1 cancelled today',
      '3 return requests waiting',
      '5 failed events',
      'Data 130 s behind',
    ]);
    expect(items.map((i) => i.href)).toEqual(['/admin/inventory', undefined, '/admin/returns', undefined, undefined]);
  });

  it('A2: Support sees cancelled, failed events and lag only', () => {
    expect(attentionItems(busy, 'Support').map((i) => i.text)).toEqual(['1 cancelled today', '5 failed events', 'Data 130 s behind']);
  });

  it('A3: all zero gives no items, and one of each is singular', () => {
    expect(attentionItems(makeDashboard({ lowStockCount: 0, cancelledCount: 0 }), 'Admin')).toEqual([]);

    const one = makeDashboard({
      lowStockCount: 1,
      cancelledCount: 0,
      needsAttention: { pendingReturns: 1, deadLetters: [{ consumer: 'a', count: 1 }], laggingProjectors: [] },
    });
    expect(attentionItems(one, 'Admin').map((i) => i.text)).toEqual(['1 low-stock variant', '1 return request waiting', '1 failed event']);
  });
});
