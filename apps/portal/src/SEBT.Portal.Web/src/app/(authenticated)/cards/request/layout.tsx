'use client'

import type { ReactNode } from 'react'

import { IalGuard } from '@/features/auth'

export default function CardsRequestLayout({ children }: { children: ReactNode }) {
  return <IalGuard>{children}</IalGuard>
}
