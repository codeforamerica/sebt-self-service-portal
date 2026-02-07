'use client'

import { useRouter } from 'next/navigation'
import { useTranslation } from 'react-i18next'

import { useAuth } from '@/features/auth'

import type { HouseholdData } from '../../api'

interface UserProfileCardProps {
  data: HouseholdData
}

function getInitials(firstName: string, lastName: string | null | undefined): string {
  const firstInitial = firstName.charAt(0).toUpperCase()
  if (!lastName) {
    return firstInitial
  }
  const lastInitial = lastName.charAt(0).toUpperCase()
  return `${firstInitial}${lastInitial}`
}

function formatFullName(
  firstName: string,
  middleName: string | null | undefined,
  lastName: string | null | undefined
): string {
  // Extract middle initial (first character) to handle both initials and full middle names
  const middleInitial = middleName ? middleName.charAt(0).toUpperCase() : null

  // Handle mononyms (users with only a first name)
  if (!lastName) {
    return middleInitial ? `${firstName} ${middleInitial}.` : firstName
  }
  if (middleInitial) {
    return `${firstName} ${middleInitial}. ${lastName}`
  }
  return `${firstName} ${lastName}`
}

export function UserProfileCard({ data }: UserProfileCardProps) {
  const { t } = useTranslation('dashboard')
  const { logout } = useAuth()
  const router = useRouter()

  const handleLogout = () => {
    logout()
    router.push('/login')
  }

  if (!data.userProfile) {
    return null
  }

  const { firstName, middleName, lastName } = data.userProfile
  const initials = getInitials(firstName, lastName)
  const fullName = formatFullName(firstName, middleName, lastName)

  return (
    <div className="display-flex flex-align-start margin-bottom-4">
      {/* Avatar with initials - sized to match heading text */}
      <div
        className="display-flex flex-justify-center flex-align-center bg-primary text-white radius-pill font-sans-xs text-bold"
        style={{
          width: '2rem',
          height: '2rem',
          flexShrink: 0
        }}
        aria-hidden="true"
      >
        {initials}
      </div>

      {/* Name and Logout */}
      <div className="margin-left-105">
        <h2 className="margin-0 font-heading-lg">{fullName}</h2>
        <button
          type="button"
          onClick={handleLogout}
          className="usa-button usa-button--unstyled text-bold"
        >
          {t('logout')}
        </button>
      </div>
    </div>
  )
}
