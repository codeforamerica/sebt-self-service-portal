import { forwardRef } from 'react'

import type { ButtonProps, ButtonVariant } from './types'

const variantClasses: Record<ButtonVariant, string> = {
  primary: 'usa-button',
  secondary: 'usa-button usa-button--secondary',
  outline: 'usa-button usa-button--outline',
  unstyled: 'usa-button usa-button--unstyled'
}

export const Button = forwardRef<HTMLButtonElement, ButtonProps>(function Button(
  {
    children,
    className = '',
    variant = 'primary',
    fullWidth = false,
    isLoading = false,
    loadingText,
    disabled,
    type = 'button',
    ...props
  },
  ref
) {
  // eslint-disable-next-line security/detect-object-injection -- variant is typed ButtonVariant
  const baseClass = variantClasses[variant]
  const combinedClassName = [baseClass, fullWidth && 'usa-button--full-width', className]
    .filter(Boolean)
    .join(' ')

  return (
    <button
      ref={ref}
      type={type}
      className={combinedClassName}
      disabled={disabled || isLoading}
      aria-busy={isLoading}
      {...props}
    >
      {isLoading && loadingText ? loadingText : children}
    </button>
  )
})
