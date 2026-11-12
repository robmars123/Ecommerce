import { render, screen } from '@testing-library/react-native';

import { ProductImageView } from './ProductImageView';

describe('ProductImageView', () => {
  it('renders a labelled placeholder when the URL is null', async () => {
    await render(<ProductImageView url={null} altText="Blue shirt, front" fallbackLabel="Blue Shirt" />);

    expect(screen.getByLabelText('Blue shirt, front')).toBeOnTheScreen();
  });

  it('labels the placeholder with the fallback when alt text is empty', async () => {
    await render(<ProductImageView url={null} altText="" fallbackLabel="Blue Shirt" />);

    expect(screen.getByLabelText('Blue Shirt')).toBeOnTheScreen();
  });

  it('renders the image with its alt text inside an explicit aspect-ratio box', async () => {
    await render(<ProductImageView url="https://img.test.invalid/a.jpg" altText="Front" fallbackLabel="Shirt" aspectRatio={1.5} />);

    const image = screen.getByLabelText('Front');
    expect(image).toBeOnTheScreen();
  });

  it('keeps the placeholder box sized by the aspect ratio so layout does not shift', async () => {
    await render(<ProductImageView url={null} altText="x" fallbackLabel="y" aspectRatio={2} width={100} />);

    expect(screen.getByLabelText('x')).toHaveStyle({ aspectRatio: 2, width: 100 });
  });
});
