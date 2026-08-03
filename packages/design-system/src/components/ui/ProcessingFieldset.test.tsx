import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { ProcessingFieldset } from './ProcessingFieldset';

describe('ProcessingFieldset', () => {
  it('renders children inside a usa-fieldset', () => {
    const { container } = render(
      <ProcessingFieldset isProcessing={false} legend="Contact details">
        <input aria-label="Email" />
      </ProcessingFieldset>
    );

    const fieldset = container.querySelector('fieldset');
    expect(fieldset).toHaveClass('usa-fieldset');
    expect(screen.getByLabelText('Email')).toBeInTheDocument();
  });

  it('renders the legend as the first child of the fieldset', () => {
    const { container } = render(
      <ProcessingFieldset isProcessing={false} legend="Contact details">
        <input aria-label="Email" />
      </ProcessingFieldset>
    );

    const legend = container.querySelector('fieldset')?.firstElementChild;
    expect(legend?.tagName).toBe('LEGEND');
    expect(legend).toHaveClass('usa-legend');
    expect(legend).toHaveTextContent('Contact details');
  });

  it('names the fieldset group via the legend', () => {
    render(
      <ProcessingFieldset isProcessing={false} legend="Contact details">
        <input aria-label="Email" />
      </ProcessingFieldset>
    );

    expect(screen.getByRole('group')).toHaveAccessibleName('Contact details');
  });

  it('visually hides the legend but keeps the accessible name when legendHidden', () => {
    render(
      <ProcessingFieldset isProcessing={false} legend="Contact details" legendHidden>
        <input aria-label="Email" />
      </ProcessingFieldset>
    );

    const legend = screen.getByText('Contact details');
    expect(legend).toHaveClass('usa-sr-only');
    expect(legend).not.toHaveClass('usa-legend');
    expect(screen.getByRole('group')).toHaveAccessibleName('Contact details');
  });

  it('accepts rich legend content', () => {
    render(
      <ProcessingFieldset
        isProcessing={false}
        legend={
          <>
            Choose one<span> *</span>
          </>
        }
      >
        <input aria-label="Email" />
      </ProcessingFieldset>
    );

    expect(screen.getByRole('group')).toHaveAccessibleName(/choose one/i);
  });

  it('leaves descendant controls enabled and unfaded while idle', () => {
    const { container } = render(
      <ProcessingFieldset isProcessing={false} legend="Contact details">
        <input aria-label="Email" />
      </ProcessingFieldset>
    );

    expect(screen.getByLabelText('Email')).not.toBeDisabled();
    expect(container.querySelector('fieldset')).not.toHaveAttribute('disabled');
    expect(container.querySelector('fieldset')).not.toHaveClass('opacity-50');
  });

  it('disables descendant controls through the fieldset while processing', () => {
    render(
      <ProcessingFieldset isProcessing legend="Contact details">
        <input aria-label="Email" />
        <button type="submit">Continue</button>
      </ProcessingFieldset>
    );

    expect(screen.getByLabelText('Email')).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Continue' })).toBeDisabled();
  });

  it('fades to 50% opacity while processing', () => {
    const { container } = render(
      <ProcessingFieldset isProcessing legend="Contact details">
        <input aria-label="Email" />
      </ProcessingFieldset>
    );

    expect(container.querySelector('fieldset')).toHaveClass('usa-fieldset', 'opacity-50');
  });

  it('merges a custom className', () => {
    const { container } = render(
      <ProcessingFieldset isProcessing={false} legend="Contact details" className="margin-top-3">
        <input aria-label="Email" />
      </ProcessingFieldset>
    );

    expect(container.querySelector('fieldset')).toHaveClass('usa-fieldset', 'margin-top-3');
  });
});
