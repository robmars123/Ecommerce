import { StyleSheet } from 'react-native';
import { fireEvent, render, screen } from '@testing-library/react-native';

import { SortPicker } from './SortPicker';

describe('SortPicker', () => {
  it('hides relevance when there is no search and marks newest as the default', async () => {
    await render(<SortPicker hasSearch={false} onSelect={jest.fn()} />);

    expect(screen.queryByRole('button', { name: 'Relevance' })).toBeNull();
    expect(screen.getByRole('button', { name: 'Newest' })).toBeSelected();
  });

  it('offers relevance, defaulting to it, when there is a search', async () => {
    await render(<SortPicker hasSearch onSelect={jest.fn()} />);

    expect(screen.getByRole('button', { name: 'Relevance' })).toBeSelected();
  });

  it('reports the chosen sort and marks the active one', async () => {
    const onSelect = jest.fn();
    await render(<SortPicker sort="price-asc" hasSearch={false} onSelect={onSelect} />);

    expect(screen.getByRole('button', { name: 'Price: low to high' })).toBeSelected();
    await fireEvent.press(screen.getByRole('button', { name: 'Price: high to low' }));
    expect(onSelect).toHaveBeenCalledWith('price-desc');
  });

  it('does not stretch or shrink the horizontal scroller in a column parent', async () => {
    await render(<SortPicker hasSearch={false} onSelect={jest.fn()} />);

    const style = StyleSheet.flatten(screen.getByTestId('sort-scroller').props.style);
    expect(style).toMatchObject({ flexGrow: 0, flexShrink: 0 });
  });
});

describe('SortPicker best selling', () => {
  it('offers best selling without a search and reports it', async () => {
    const onSelect = jest.fn();
    await render(<SortPicker hasSearch={false} onSelect={onSelect} />);

    await fireEvent.press(screen.getByRole('button', { name: 'Best selling' }));

    expect(onSelect).toHaveBeenCalledWith('bestselling');
  });

  it('marks it as the active sort', async () => {
    await render(<SortPicker sort="bestselling" hasSearch onSelect={jest.fn()} />);

    expect(screen.getByRole('button', { name: 'Best selling' })).toBeSelected();
  });
});
