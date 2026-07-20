import { IalGuard } from '@/features/auth'
import type { ReactNode } from 'react'

export default function ProfileAddressLayout({ children }: { children: ReactNode }) {
  return <IalGuard>{children}</IalGuard>
}
