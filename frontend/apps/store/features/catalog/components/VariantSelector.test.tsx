import { fireEvent, render, screen } from '@testing-library/react-native';

import { VariantSelector } from './VariantSelector';
import { makeVariant } from '@/tests/fixtures';

describe('VariantSelector', () => {
  const variants = [
    makeVariant({ id: 'v-1', name: 'Small' }),
    makeVariant({ id: 'v-2', name: '', size: 'L', colour: 'Red' }),
  ];

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
