import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { ProcessingIndicator } from './ProcessingIndicator';

describe('ProcessingIndicator', () => {
  it('always mounts a polite status region, even while idle', () => {
    render(<ProcessingIndicator isProcessing={false} label="Processing" />);

    const region = screen.getByRole('status');
    expect(region).toHaveAttribute('aria-live', 'polite');
  });

  it('renders an empty region while idle', () => {
    const { container } = render(
      <ProcessingIndicator isProcessing={false} label="Processing" />
    );

    expect(container.querySelector('.usa-spinner')).toBeNull();
    expect(screen.getByRole('status')).toBeEmptyDOMElement();
  });

  it('renders the spinner and the visually hidden label while processing', () => {
    const { container } = render(
      <ProcessingIndicator isProcessing label="Processing" />
    );

    const spinner = container.querySelector('.usa-spinner');
    expect(spinner).not.toBeNull();
    expect(spinner).toHaveAttribute('aria-hidden', 'true');

    const label = screen.getByText('Processing');
    expect(label).toHaveClass('usa-sr-only');
    expect(screen.getByRole('status')).toContainElement(label);
  });

  it('never sets aria-busy on the live region (it defers announcements; the submit button carries aria-busy)', () => {
    const { rerender } = render(
      <ProcessingIndicator isProcessing={false} label="Processing" />
    );
    expect(screen.getByRole('status')).not.toHaveAttribute('aria-busy');

    rerender(<ProcessingIndicator isProcessing label="Processing" />);
    expect(screen.getByRole('status')).not.toHaveAttribute('aria-busy');
  });

  it('merges a custom className', () => {
    render(
      <ProcessingIndicator isProcessing={false} label="Processing" className="margin-top-3" />
    );

    expect(screen.getByRole('status')).toHaveClass('margin-top-3');
  });
});
