import type { HTMLAttributes } from 'react'

export interface RequiredFieldsNoteProps extends HTMLAttributes<HTMLParagraphElement> {
  /** Translated note text; its first `*` is rendered in the error color. */
  text: string
}

/**
 * Explains the required-field asterisk, styled to match the asterisks on field labels.
 */
export function RequiredFieldsNote({ text, ...rest }: RequiredFieldsNoteProps) {
  const asteriskIndex = text.indexOf('*')

  if (asteriskIndex === -1) {
    return <p {...rest}>{text}</p>
  }

  return (
    <p {...rest}>
      {text.slice(0, asteriskIndex)}
      <span className="text-error">*</span>
      {text.slice(asteriskIndex + 1)}
    </p>
  )
}
