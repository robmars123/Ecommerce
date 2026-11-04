import { fireEvent, render, screen } from '@testing-library/react-native';

import { Pagination } from './Pagination';

describe('Pagination', () => {
  it('renders nothing when there is one page or none', async () => {
    await render(<Pagination page={1} totalPages={1} onPrevious={jest.fn()} onNext={jest.fn()} />);
    expect(screen.toJSON()).toBeNull();
    await screen.rerender(<Pagination page={1} totalPages={0} onPrevious={jest.fn()} onNext={jest.fn()} />);
    expect(screen.toJSON()).toBeNull();
  });

  it('disables Previous on the first page and calls onNext', async () => {
    const onPrevious = jest.fn();
    const onNext = jest.fn();
    await render(<Pagination page={1} totalPages={3} onPrevious={onPrevious} onNext={onNext} />);

    expect(screen.getByText('Page 1 of 3')).toBeOnTheScreen();
    expect(screen.getByRole('button', { name: 'Previous' })).toBeDisabled();
    await fireEvent.press(screen.getByRole('button', { name: 'Previous' }));
    expect(onPrevious).not.toHaveBeenCalled();
    await fireEvent.press(screen.getByRole('button', { name: 'Next' }));
    expect(onNext).toHaveBeenCalledTimes(1);
  });

  it('disables Next on the last page and calls onPrevious', async () => {
    const onPrevious = jest.fn();
    const onNext = jest.fn();
    await render(<Pagination page={3} totalPages={3} onPrevious={onPrevious} onNext={onNext} />);

    expect(screen.getByRole('button', { name: 'Next' })).toBeDisabled();
    await fireEvent.press(screen.getByRole('button', { name: 'Next' }));
    expect(onNext).not.toHaveBeenCalled();
    await fireEvent.press(screen.getByRole('button', { name: 'Previous' }));
    expect(onPrevious).toHaveBeenCalledTimes(1);
  });
});
