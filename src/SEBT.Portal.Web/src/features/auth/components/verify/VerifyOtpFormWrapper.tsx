'use client'

import { useRouter } from 'next/navigation'
import { useEffect, useSyncExternalStore } from 'react'

import { VerifyOtpForm } from './VerifyOtpForm'

interface VerifyOtpFormWrapperProps {
  contactLink: string
}

function subscribeToSessionStorage(callback: () => void) {
  window.addEventListener('storage', callback)
  return () => window.removeEventListener('storage', callback)
}

function getEmailSnapshot() {
  return sessionStorage.getItem('otp_email')
}

function getServerSnapshot() {
  return null
}

/**
 * Wrapper component that handles email retrieval from sessionStorage
 * and redirects to login if no email is found.
 *
 * Uses useSyncExternalStore to read from sessionStorage without triggering
 * cascading renders from setState inside useEffect.
 */
export function VerifyOtpFormWrapper({ contactLink }: VerifyOtpFormWrapperProps) {
  const router = useRouter()
  const email = useSyncExternalStore(subscribeToSessionStorage, getEmailSnapshot, getServerSnapshot)

  useEffect(() => {
    if (email === null) {
      router.replace('/login')
    }
  }, [email, router])

  if (!email) {
    return null
  }

  return (
    <VerifyOtpForm
      email={email}
      contactLink={contactLink}
    />
  )
}
