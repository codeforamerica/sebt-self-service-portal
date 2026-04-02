import { getTranslations } from '@/lib/translations'
import Link from 'next/link'

/** Placeholder for the profile mailing-address flow; IAL / OIDC step-up is gated in `layout.tsx` for this route only. */
export default function ProfileAddressPage() {
  const t = getTranslations('common')

  return (
    <div className="grid-container maxw-tablet margin-top-4">
      <div className="usa-prose">
        <h1>{t('profileAddressStubTitle', 'Mailing address')}</h1>
        <p>{t('profileAddressStubBody', "We're still building this page. Check back soon.")}</p>
        <p>
          <Link
            href="/dashboard"
            className="usa-button usa-button--outline"
          >
            {t('profileAddressBackToDashboard', 'Back to dashboard')}
          </Link>
        </p>
      </div>
    </div>
  )
}
