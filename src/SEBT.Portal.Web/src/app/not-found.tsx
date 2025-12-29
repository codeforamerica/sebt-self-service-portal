import { Alert } from '@/components/ui'
import Link from 'next/link'

// TODO: Localize hardcoded strings (heading, body, link text)
export default function NotFound() {
  return (
    <section
      className="usa-section"
      aria-labelledby="not-found-heading"
    >
      <div className="grid-container">
        <Alert
          variant="error"
          heading="Page not found"
        >
          <p>The page you are looking for does not exist or has been moved.</p>
          <Link
            href="/"
            className="usa-button margin-top-2"
          >
            Return to home
          </Link>
        </Alert>
      </div>
    </section>
  )
}
