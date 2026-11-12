import { parseOrderListParams, STATUSES } from './order-list-params';

describe('parseOrderListParams (arch 13.2 "Admin order list URL contract")', () => {
  it('P1 defaults to All, page 1', () => {
    expect(parseOrderListParams({})).toEqual({ status: 'all', page: 1 });
  });

  it.each(STATUSES)('P2 round-trips %s', (status) => {
    expect(parseOrderListParams({ status })).toEqual({ status, page: 1 });
  });

  it.each(['paid', 'Bogus', ''])('P3 treats %p as All', (status) => {
    expect(parseOrderListParams({ status }).status).toBe('all');
  });

  it('P3 treats a repeated status as All', () => {
    expect(parseOrderListParams({ status: ['Paid', 'Shipped'] }).status).toBe('all');
  });

  it('P4 reads a page of 2 or more', () => {
    expect(parseOrderListParams({ page: '2' }).page).toBe(2);
  });

  it.each(['0', '-1', 'abc', '1.5', ''])('P4 treats page %p as 1', (page) => {
    expect(parseOrderListParams({ page }).page).toBe(1);
  });

  it('P4 treats a repeated page as 1', () => {
    expect(parseOrderListParams({ page: ['2', '3'] }).page).toBe(1);
  });

  it('P5 reads status and page independently', () => {
    expect(parseOrderListParams({ status: 'Shipped', page: '3' })).toEqual({
      status: 'Shipped',
      page: 3,
    });
  });
});
