import { STATUS_BUCKETS, statusBuckets } from './status-buckets';

const ALL = ['PendingPayment', 'Paid', 'Processing', 'Shipped', 'Delivered', 'Cancelled', 'Refunded', 'Returned'];

describe('statusBuckets', () => {
  it('places each of the 8 statuses in exactly one bucket', () => {
    const placed = STATUS_BUCKETS.flatMap((b) => b.statuses);

    expect([...placed].sort()).toEqual([...ALL].sort());
  });

  it('counts and rounds the percent of the total per bucket', () => {
    const { total, buckets } = statusBuckets([
      { status: 'PendingPayment', count: 1 },
      { status: 'Paid', count: 2 },
      { status: 'Shipped', count: 1 },
      { status: 'Delivered', count: 4 },
      { status: 'Cancelled', count: 2 },
    ]);

    expect(total).toBe(10);
    expect(buckets.map((b) => [b.label, b.count, b.percent])).toEqual([
      ['Awaiting payment', 1, 10],
      ['In progress', 3, 30],
      ['Completed', 4, 40],
      ['Cancelled or refunded', 2, 20],
    ]);
    expect(buckets[1].statuses).toEqual([
      { status: 'Paid', count: 2 },
      { status: 'Shipped', count: 1 },
    ]);
  });

  it('is empty with a zero total and zero percents when there are no orders, and ignores unknown statuses', () => {
    expect(statusBuckets([]).total).toBe(0);
    expect(statusBuckets([]).buckets.map((b) => b.percent)).toEqual([0, 0, 0, 0]);
    expect(statusBuckets([{ status: 'Mystery', count: 9 }]).total).toBe(0);
  });

  it('gives an orders link only to single-status buckets', () => {
    const { buckets } = statusBuckets([{ status: 'PendingPayment', count: 1 }]);

    expect(buckets[0].href).toEqual({ pathname: '/admin/orders', params: { status: 'PendingPayment' } });
    expect(buckets[2].href).toEqual({ pathname: '/admin/orders', params: { status: 'Delivered' } });
    expect(buckets[1].href).toBeUndefined();
    expect(buckets[3].href).toBeUndefined();
  });
});
