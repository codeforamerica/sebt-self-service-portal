// Safety: markdown-to-jsx renders HTML from markdown strings. This is safe
// only for trusted content (i18n locale strings from generated CSVs). Do not
// use this component with user-supplied input without adding sanitization.
import Markdown from 'markdown-to-jsx'

export interface RichTextProps {
  children: string
  /** When true, renders inline (no wrapping p tags). Use for bold within a sentence. */
  inline?: boolean
}

export function RichText({ children, inline = false }: RichTextProps) {
  return (
    <Markdown options={{
      ...(inline && { forceInline: true }),
      overrides: {}
    }}>
      {children}
    </Markdown>
  )
}
