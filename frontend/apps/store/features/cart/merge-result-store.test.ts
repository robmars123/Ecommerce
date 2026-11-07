import { useMergeResultStore } from './merge-result-store';

describe('merge result store', () => {
  afterEach(() => useMergeResultStore.getState().dismiss());

  it('holds results until dismissed and a failure with its retry', () => {
    const retry = jest.fn();
    useMergeResultStore.getState().publish([{ variantId: 'v', outcome: 'Kept', quantity: 2, reason: null, name: 'Blue Shirt' }]);
    expect(useMergeResultStore.getState().results).toHaveLength(1);

    useMergeResultStore.getState().fail(retry);
    expect(useMergeResultStore.getState().failed).toBe(true);
    useMergeResultStore.getState().retry?.();
    expect(retry).toHaveBeenCalledTimes(1);

    useMergeResultStore.getState().dismiss();
    expect(useMergeResultStore.getState()).toMatchObject({ results: null, failed: false, retry: null });
  });
});
