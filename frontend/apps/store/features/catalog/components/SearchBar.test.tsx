import { act, fireEvent, render, screen } from '@testing-library/react-native';

import { SearchBar } from './SearchBar';

beforeEach(() => {
  jest.useFakeTimers();
});

afterEach(() => {
  jest.useRealTimers();
});

describe('SearchBar', () => {
  it('has an accessible label and starts with the given value', async () => {
    await render(<SearchBar value="shirt" />);

    expect(screen.getByLabelText('Search products').props.value).toBe('shirt');
  });

  it('emits the trimmed text once, only after typing has paused for 300ms', async () => {
    const onSearch = jest.fn();
    await render(<SearchBar onSearch={onSearch} />);

    await fireEvent.changeText(screen.getByLabelText('Search products'), 'sh');
    await fireEvent.changeText(screen.getByLabelText('Search products'), '  shirt ');
    await act(async () => {
      await jest.advanceTimersByTimeAsync(299);
    });
    expect(onSearch).not.toHaveBeenCalled();
    await act(async () => {
      await jest.advanceTimersByTimeAsync(1);
    });

    expect(onSearch).toHaveBeenCalledTimes(1);
    expect(onSearch).toHaveBeenCalledWith('shirt');
  });

  it('does not emit on mount or when the text is unchanged', async () => {
    const onSearch = jest.fn();
    await render(<SearchBar value="shirt" onSearch={onSearch} />);

    await act(async () => {
      await jest.advanceTimersByTimeAsync(1000);
    });

    expect(onSearch).not.toHaveBeenCalled();
  });

  it('clears immediately without waiting for the debounce', async () => {
    const onSearch = jest.fn();
    await render(<SearchBar value="shirt" onSearch={onSearch} />);

    await fireEvent.press(screen.getByRole('button', { name: 'Clear search' }));

    expect(onSearch).toHaveBeenCalledWith('');
    expect(screen.getByLabelText('Search products').props.value).toBe('');
  });

  it('shows no clear button when empty', async () => {
    await render(<SearchBar />);
    expect(screen.queryByRole('button', { name: 'Clear search' })).toBeNull();
  });

  it('submits the trimmed text immediately', async () => {
    const onSubmit = jest.fn();
    await render(<SearchBar onSubmit={onSubmit} />);

    await fireEvent.changeText(screen.getByLabelText('Search products'), ' jeans ');
    await fireEvent(screen.getByLabelText('Search products'), 'submitEditing');

    expect(onSubmit).toHaveBeenCalledWith('jeans');
  });

  it('replaces the typed text when the committed value changes from outside', async () => {
    await render(<SearchBar value="shirt" />);

    await screen.rerender(<SearchBar value="" />);

    expect(screen.getByLabelText('Search products').props.value).toBe('');
  });
});
