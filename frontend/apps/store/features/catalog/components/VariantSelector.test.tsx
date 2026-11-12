import { fireEvent, render, screen } from '@testing-library/react-native';

import { VariantSelector, variantGroupLabel } from './VariantSelector';
import { makeVariant } from '@/tests/fixtures';

describe('VariantSelector', () => {
  const variants = [makeVariant({ id: 'v-1', name: 'Small' }), makeVariant({ id: 'v-2', name: '', size: 'L', colour: 'Red' })];

  it('marks the selected variant and labels by name, or size and colour when unnamed', async () => {
    await render(<VariantSelector variants={variants} selectedId="v-2" onSelect={jest.fn()} />);

    expect(screen.getByRole('button', { name: 'Small' })).not.toBeSelected();
    expect(screen.getByRole('button', { name: 'L Red' })).toBeSelected();
  });

  it('calls onSelect with the variant id', async () => {
    const onSelect = jest.fn();
    await render(<VariantSelector variants={variants} selectedId="v-1" onSelect={onSelect} />);

    await fireEvent.press(screen.getByRole('button', { name: 'L Red' }));

    expect(onSelect).toHaveBeenCalledWith('v-2');
  });
});

describe('VariantSelector sold-out variants', () => {
  const variants = [
    makeVariant({ id: 'v-1', name: 'Small', size: 'S', stockStatus: 'Out' }),
    makeVariant({ id: 'v-2', name: 'Large', size: 'L', stockStatus: 'InStock' }),
  ];

  it('disables an Out variant with a Sold out hint, keeps its name, and never selects it', async () => {
    const onSelect = jest.fn();
    await render(<VariantSelector variants={variants} selectedId="v-2" onSelect={onSelect} />);

    const out = screen.getByRole('button', { name: 'Small' });
    expect(out).toBeDisabled();
    expect(out.props.accessibilityHint).toBe('Sold out');
    await fireEvent.press(out);
    expect(onSelect).not.toHaveBeenCalled();
    expect(screen.getByRole('button', { name: 'Large' })).toBeEnabled();
  });

  it('labels the group Size only when every variant has a size, and shows the selection', async () => {
    expect(variantGroupLabel(variants)).toBe('Size');
    expect(variantGroupLabel([...variants, makeVariant({ id: 'v-3', size: null })])).toBe('Option');

    await render(<VariantSelector variants={variants} selectedId="v-2" onSelect={jest.fn()} />);
    expect(screen.getByText('Size: Large')).toBeOnTheScreen();
  });
});
