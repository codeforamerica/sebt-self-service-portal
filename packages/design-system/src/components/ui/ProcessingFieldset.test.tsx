import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { ProcessingFieldset } from './ProcessingFieldset';

describe('ProcessingFieldset', () => {
  it('renders children inside a usa-fieldset', () => {
    const { container } = render(
      <ProcessingFieldset isProcessing={false}>
        <input aria-label="Email" />
      </ProcessingFieldset>
    );

    const fieldset = container.querySelector('fieldset');
    expect(fieldset).toHaveClass('usa-fieldset');
    expect(screen.getByLabelText('Email')).toBeInTheDocument();
  });

  it('leaves descendant controls enabled and unfaded while idle', () => {
    const { container } = render(
      <ProcessingFieldset isProcessing={false}>
        <input aria-label="Email" />
      </ProcessingFieldset>
    );

    expect(screen.getByLabelText('Email')).not.toBeDisabled();
    expect(container.querySelector('fieldset')).not.toHaveAttribute('disabled');
    expect(container.querySelector('fieldset')).not.toHaveClass('opacity-50');
  });

  it('disables descendant controls through the fieldset while processing', () => {
    render(
      <ProcessingFieldset isProcessing>
        <input aria-label="Email" />
        <button type="submit">Continue</button>
      </ProcessingFieldset>
    );

    expect(screen.getByLabelText('Email')).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Continue' })).toBeDisabled();
  });

  it('fades to 50% opacity while processing', () => {
    const { container } = render(
      <ProcessingFieldset isProcessing>
        <input aria-label="Email" />
      </ProcessingFieldset>
    );

    expect(container.querySelector('fieldset')).toHaveClass('usa-fieldset', 'opacity-50');
  });

  it('merges a custom className', () => {
    const { container } = render(
      <ProcessingFieldset isProcessing={false} className="margin-top-3">
        <input aria-label="Email" />
      </ProcessingFieldset>
    );

    expect(container.querySelector('fieldset')).toHaveClass('usa-fieldset', 'margin-top-3');
  });
});
