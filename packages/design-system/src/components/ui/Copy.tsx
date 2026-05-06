'use client'

import type { ElementType, ReactElement } from 'react'

import { useTranslation } from 'react-i18next'

export type CopyProps = {
  ns: string
  k: string
  fallback: string
  as?: ElementType
}

function isMissing(value: string, key: string): boolean {
  return value === key || value === ''
}

export function Copy({ ns, k, fallback, as: Tag = 'span' }: CopyProps): ReactElement {
  const { t } = useTranslation(ns)
  const value = t(k)

  if (!isMissing(value, k)) {
    return <>{value}</>
  }

  if (process.env.NODE_ENV !== 'development') {
    return <>{fallback}</>
  }

  return (
    <Tag
      data-copy-status="fallback"
      data-copy-key={`${ns}:${k}`}
    >
      {fallback}
    </Tag>
  )
}

export type UseCopyResult = {
  text: string
  isFallback: boolean
}

export function useCopy(ns: string, k: string, fallback: string): UseCopyResult {
  const { t } = useTranslation(ns)
  const value = t(k)
  const fallbackUsed = isMissing(value, k)
  return { text: fallbackUsed ? fallback : value, isFallback: fallbackUsed }
}
