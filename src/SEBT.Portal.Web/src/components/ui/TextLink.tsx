import Link from 'next/link'
import type { ComponentProps, ReactNode } from 'react'

export interface TextLinkProps extends Omit<ComponentProps<typeof Link>, 'className'> {
  children: ReactNode
  className?: string
}

export function TextLink({ children, className = '', ...props }: TextLinkProps) {
  const combinedClassName = ['text-bold text-ink text-underline', className]
    .filter(Boolean)
    .join(' ')

  return (
    <Link
      className={combinedClassName}
      {...props}
    >
      {children}
    </Link>
  )
}
