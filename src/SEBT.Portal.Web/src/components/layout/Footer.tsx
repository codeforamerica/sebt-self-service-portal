'use client'

import Image from 'next/image'
import Link from 'next/link'
import { useTranslation } from 'react-i18next'

import { getFooterLinks, getStateLinks } from '@/lib/links'

import type { FooterProps } from './types'

export function Footer({ state = 'dc' }: FooterProps) {
  const { t } = useTranslation('common')

  if (state === 'co') {
    return <COFooter state={state} />
  }

  const links = getStateLinks(state)
  const footerLinks = getFooterLinks(state)

  return (
    <footer
      className="usa-footer usa-footer--slim"
      role="contentinfo"
    >
      <div className="usa-footer__primary-section padding-top-4">
        <div className="grid-container display-flex flex-justify-center">
          <Image
            src={`/images/states/${state}/seal.svg`}
            alt="Government of the District of Columbia - Muriel Bowser, Mayor"
            width={121}
            height={51}
            className="footer-logo"
          />
        </div>
      </div>

      <div className="usa-footer__secondary-section padding-y-2 text-center border-bottom border-ink">
        <div className="grid-container">
          <Link
            href={links.footer.publicNotifications}
            target="_blank"
            rel="noopener noreferrer"
            className="usa-link text-ink font-ui-md text-semibold"
          >
            {t('publicNotifications')}
          </Link>
        </div>
      </div>

      <div className="usa-footer__secondary-section padding-y-2">
        <div className="grid-container">
          <nav aria-label="Footer navigation">
            <ul className="usa-list usa-list--unstyled display-flex flex-column flex-align-center add-list-reset">
              {footerLinks.map((link) => (
                <li
                  key={link.key}
                  className="margin-y-1"
                >
                  <Link
                    href={link.href}
                    target="_blank"
                    rel="noopener noreferrer"
                    className="usa-link text-ink font-sans-xs"
                  >
                    {t(link.translationKey)}
                  </Link>
                </li>
              ))}
            </ul>
          </nav>
        </div>
      </div>

      <div className="usa-footer__secondary-section text-center">
        <div className="grid-container">
          <p className="margin-0 text-ink footer-copyright">{t('copyright')}</p>
        </div>
      </div>
    </footer>
  )
}

function COFooter({ state }: { state: string }) {
  const links = getStateLinks(state)

  return (
    <footer
      className="usa-footer usa-footer--slim"
      role="contentinfo"
    >
      <div className="usa-footer__primary-section padding-y-2">
        <div className="grid-container text-center">
          <p className="margin-0 text-white font-sans-xs">
            {/* TODO: Use t('copyright') once the key is added to co.csv */}© 2026 State of Colorado
            {' | '}
            <Link
              href={links.footer.transparencyOnline ?? '#'}
              target="_blank"
              rel="noopener noreferrer"
              className="usa-link text-white text-underline"
            >
              {/* TODO: Use t('transparencyOnline') once the key is added to co.csv */}
              Transparency Online
            </Link>
            {' | '}
            <Link
              href={links.footer.generalNotices ?? '#'}
              target="_blank"
              rel="noopener noreferrer"
              className="usa-link text-white text-underline"
            >
              {/* TODO: Use t('generalNotices') once the key is added to co.csv */}
              General Notices
            </Link>
          </p>
        </div>
      </div>

      <div className="usa-footer__secondary-section padding-y-4">
        <div className="grid-container display-flex flex-justify-center">
          <Image
            src={`/images/states/${state}/seal.svg`}
            alt="Colorado Official State Web Portal"
            width={136}
            height={28}
            className="footer-logo"
          />
        </div>
      </div>
    </footer>
  )
}
