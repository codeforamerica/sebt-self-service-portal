'use client'

import { useRouter } from 'next/navigation'
import { useTranslation } from 'react-i18next'

import { useAuth } from '@/features/auth'

import { useRequiredHouseholdData } from '../../api'

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

export function UserProfileCard() {
  const { t } = useTranslation('dashboard')
  const { logout } = useAuth()
  const router = useRouter()
  const data = useRequiredHouseholdData()

  const handleLogout = async () => {
    await logout()
    router.push('/login')
  }

  if (!data.userProfile) {
    return null
  }

  const { firstName, middleName, lastName } = data.userProfile
  const initials = getInitials(firstName, lastName)
  const fullName = formatFullName(firstName, middleName, lastName)

  return (
    <div className="user-profile-card margin-bottom-4">
      <div
        className="avatar-circle display-flex flex-justify-center flex-align-center bg-base-darkest text-white radius-pill font-sans-md text-bold"
        aria-hidden="true"
      >
        {initials}
      </div>
      <h2 className="margin-0 font-heading-lg">{fullName}</h2>
      <button
        type="button"
        onClick={handleLogout}
        className="button-unstyled usa-link font-sans-md text-bold line-height-sans-1"
      >
        {t('logout')}
      </button>
    </div>
  )
}
