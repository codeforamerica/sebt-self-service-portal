import { render } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { Spinner } from './Spinner';

describe('Spinner', () => {
  it('renders a span with the usa-spinner class', () => {
    const { container } = render(<Spinner />);

    const spinner = container.querySelector('span.usa-spinner');
    expect(spinner).not.toBeNull();
  });

  it('is hidden from assistive tech (announcing is the caller`s job)', () => {
    const { container } = render(<Spinner />);

    const spinner = container.querySelector('.usa-spinner');
    expect(spinner).toHaveAttribute('aria-hidden', 'true');
  });

  it('merges a custom className with the base class', () => {
    const { container } = render(<Spinner className="margin-left-2" />);

    const spinner = container.querySelector('.usa-spinner');
    expect(spinner).toHaveClass('usa-spinner', 'margin-left-2');
  });
});
