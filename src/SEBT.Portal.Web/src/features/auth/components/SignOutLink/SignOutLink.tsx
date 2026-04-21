'use client'

import { useRouter } from 'next/navigation'
import { useTranslation } from 'react-i18next'

import { useAuth } from '../../context'

export function SignOutLink() {
  const { t } = useTranslation('dashboard')
  const { logout } = useAuth()
  const router = useRouter()

  const handleLogout = async () => {
    await logout()
    router.push('/login')
  }

  return (
    <button
      type="button"
      onClick={handleLogout}
      className="button-unstyled usa-link font-sans-md text-bold line-height-sans-1"
    >
      {t('logout')}
    </button>
  )
}
