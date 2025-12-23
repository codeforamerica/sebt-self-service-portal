import type { FooterProps } from '@/src/types/components'
import Image from 'next/image'
import Link from 'next/link'

export function Footer({ state = 'dc' }: FooterProps) {
  const currentYear = new Date().getFullYear()

  return (
    <footer
      className="usa-footer usa-footer--slim"
      role="contentinfo"
    >
      {/* Government Branding Section */}
      <div className="usa-footer__primary-section padding-top-4">
        <div className="grid-container display-flex flex-justify-center">
          <Image
            src={`/images/states/${state}/seal.svg`}
            alt={
              state === 'dc'
                ? 'Government of the District of Columbia - Muriel Bowser, Mayor'
                : `State of ${state.toUpperCase()} Official Government`
            }
            width={121}
            height={51}
            className="footer-logo"
          />
        </div>
      </div>

      {/* Public Notifications Link */}
      <div className="usa-footer__secondary-section padding-y-2 text-center border-bottom border-ink">
        <div className="grid-container">
          <Link
            href="/notifications"
            className="usa-link text-ink font-ui-md text-semibold"
          >
            Public Notifications
          </Link>
        </div>
      </div>

      {/* Navigation Links */}
      <div className="usa-footer__secondary-section padding-y-2">
        <div className="grid-container">
          <nav aria-label="Footer navigation">
            <ul className="usa-list usa-list--unstyled display-flex flex-column flex-align-center add-list-reset">
              <li className="margin-y-1">
                <Link
                  href="/accessibility"
                  className="usa-link text-ink font-sans-xs"
                >
                  Accessibility
                </Link>
              </li>
              <li className="margin-y-1">
                <Link
                  href="/privacy"
                  className="usa-link text-ink font-sans-xs"
                >
                  Privacy and Security
                </Link>
              </li>
              <li className="margin-y-1">
                <Link
                  href="/translate-disclaimer"
                  className="usa-link text-ink font-sans-xs"
                >
                  Google Translate Disclaimer
                </Link>
              </li>
              <li className="margin-y-1">
                <Link
                  href={state === 'dc' ? 'https://dc.gov' : `https://${state}.gov`}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="usa-link usa-link--external text-ink font-sans-xs"
                >
                  About {state.toUpperCase()}.GOV
                </Link>
              </li>
              <li className="margin-y-1">
                <Link
                  href="/terms"
                  className="usa-link text-ink font-sans-xs"
                >
                  Terms and Conditions
                </Link>
              </li>
            </ul>
          </nav>
        </div>
      </div>

      {/* Copyright */}
      <div className="usa-footer__secondary-section text-center">
        <div className="grid-container">
          <p className="margin-0 text-ink footer-copyright">
            © {currentYear}{' '}
            {state === 'dc' ? 'District of Columbia' : `State of ${state.toUpperCase()}`}
          </p>
        </div>
      </div>
    </footer>
  )
}
