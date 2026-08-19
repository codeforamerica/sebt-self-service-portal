import type { ButtonHTMLAttributes, InputHTMLAttributes, ReactNode } from 'react'

export type ButtonVariant = 'primary' | 'secondary' | 'outline' | 'unstyled'

export interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: ButtonVariant
  fullWidth?: boolean
  isLoading?: boolean
  /**
   * @deprecated The processing state keeps the button label unchanged; render a
   * ProcessingIndicator beside the button instead of swapping the label.
   */
  loadingText?: string
}

export type AlertVariant = 'info' | 'success' | 'warning' | 'error' | 'emergency'

export interface AlertProps {
  variant?: AlertVariant
  heading?: string
  headingClassName?: string
  textClassName?: string
  children: ReactNode
  slim?: boolean
  noIcon?: boolean
  className?: string
}

export interface InputFieldProps extends Omit<InputHTMLAttributes<HTMLInputElement>, 'id'> {
  label: string
  error?: string
  hint?: string
  isRequired?: boolean
}
