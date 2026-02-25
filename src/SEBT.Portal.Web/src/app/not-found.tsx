import { Alert } from '@/components/ui'
import { getTranslations } from '@/lib/translations'
import Link from 'next/link'

export default function NotFound() {
  const t = getTranslations('common')

  return (
    <section
      className="usa-section"
      aria-labelledby="not-found-heading"
    >
      <div className="grid-container">
        <Alert
          variant="error"
          heading={t('pageNotFound', 'Page not found')}
        >
          <p>
            {t(
              'pageNotFoundBody',
              'The page you are looking for does not exist or has been moved.'
            )}
          </p>
          <Link
            href="/"
            className="usa-button margin-top-2"
          >
            {t('returnToHome', 'Return to home')}
          </Link>
        </Alert>
      </div>
    </section>
  )
}
