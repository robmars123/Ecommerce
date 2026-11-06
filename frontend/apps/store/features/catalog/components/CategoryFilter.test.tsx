import { StyleSheet } from 'react-native';
import { fireEvent, render, screen } from '@testing-library/react-native';

import { CategoryFilter } from './CategoryFilter';
import { categories } from '@/tests/fixtures';

describe('CategoryFilter', () => {
  it('shows All plus each category and marks All selected when none is chosen', async () => {
    await render(<CategoryFilter categories={categories} onSelect={jest.fn()} />);

    expect(screen.getByRole('button', { name: 'All' })).toBeSelected();
    expect(screen.getByRole('button', { name: 'Clothing' })).not.toBeSelected();
    expect(screen.getByRole('button', { name: 'Tops' })).toBeOnTheScreen();
  });

  it('marks the selected category and reports selection, with undefined for All', async () => {
    const onSelect = jest.fn();
    await render(<CategoryFilter categories={categories} selected="tops" onSelect={onSelect} />);

    expect(screen.getByRole('button', { name: 'Tops' })).toBeSelected();
    expect(screen.getByRole('button', { name: 'All' })).not.toBeSelected();
    await fireEvent.press(screen.getByRole('button', { name: 'Clothing' }));
    expect(onSelect).toHaveBeenLastCalledWith('clothing');
    await fireEvent.press(screen.getByRole('button', { name: 'All' }));
    expect(onSelect).toHaveBeenLastCalledWith(undefined);
  });

  it('meets the 44pt minimum touch target', async () => {
    await render(<CategoryFilter categories={categories} onSelect={jest.fn()} />);
    expect(screen.getByRole('button', { name: 'Tops' })).toHaveStyle({ minHeight: 44 });
  });

  it('does not stretch or shrink the horizontal scroller in a column parent', async () => {
    await render(<CategoryFilter categories={categories} onSelect={jest.fn()} />);

    const style = StyleSheet.flatten(screen.getByTestId('category-scroller').props.style);
    expect(style).toMatchObject({ flexGrow: 0, flexShrink: 0 });
  });
});
