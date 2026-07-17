'use client'

import Image from 'next/image'
// StateResources is imported type-only: lib/i18n initializes react-i18next at module
// scope, and pulling that runtime here would break the server-safe main barrel.
import type { StateResources } from '../../lib/i18n'
import { getStateLinks } from '../../lib/links'
import { getSiteDisplayName, getState, getStateAssetPath, type StateCode } from '../../lib/state'
import { getFooterLinkLabel } from './getFooterLinkLabel'
import { getOutageFooterCopy, getOutageMessages } from './getOutageMessages'

const DEFAULT_LOGO_DIMENSIONS = { width: 122, height: 52 } as const

const logoDimensions: Record<StateCode, { width: number; height: number }> = {
  dc: { width: 122, height: 52 },
  co: { width: 192, height: 28 }
}

export interface OutagePageContentProps {
  /**
   * The app's generated locale resources (imported from its
   * generated-locale-resources.ts). Passed in rather than imported so this
   * component can render each app's own bundle — the portal and the enrollment
   * checker generate different namespace sets.
   */
  resources: StateResources
}

export function OutagePageContent({ resources }: OutagePageContentProps) {
  const state = getState()
  const messages = getOutageMessages(resources)
  const footerCopy = getOutageFooterCopy(resources)
  const links = getStateLinks(state)
  const contactHref = links.help.sebtMainSite ?? links.help.helpDeskEmail ?? '#'
  const isExternalLink = contactHref.startsWith('http')
  const footerLinkLabel = getFooterLinkLabel(contactHref)
  const { width, height } = logoDimensions[state] ?? DEFAULT_LOGO_DIMENSIONS

  const primaryMessage =
    messages.find((message) => message.language === 'en')?.body1 ?? messages[0]?.body1

  return (
    <div className="display-flex flex-align-center flex-justify-center minh-viewport padding-y-4 padding-x-2">
      <div className="width-full maxw-mobile-lg text-center text-base-dark">
        <h1 className="usa-sr-only">{primaryMessage ?? 'Maintenance'}</h1>

        <div className="display-flex flex-column gap-4 margin-bottom-5">
          {messages.map((message) => (
            <section
              key={message.language}
              lang={message.language}
              aria-label={message.language}
              className="margin-bottom-3"
            >
              {message.body1 && (
                <p className="margin-top-0 font-body-md line-height-body-3">{message.body1}</p>
              )}
              {message.body2 && (
                <p className="margin-0 font-body-sm line-height-body-3">{message.body2}</p>
              )}
            </section>
          ))}
        </div>

        <div className="margin-bottom-5">
          <Image
            src={getStateAssetPath(state, 'logo.svg')}
            alt={getSiteDisplayName(state)}
            width={width}
            height={height}
            priority
            className="maxw-full height-auto maxh-8"
          />
        </div>

        {footerCopy.map((copy) => (
          <p
            key={copy.language}
            lang={copy.language}
            aria-label={copy.language}
            className="margin-0 font-body-sm line-height-body-3"
          >
            {copy.prefix}{' '}
            <a
              href={contactHref}
              {...(isExternalLink ? { target: '_blank', rel: 'noopener noreferrer' } : {})}
              className="usa-link text-base-dark"
            >
              {footerLinkLabel}
              {isExternalLink && <span className="usa-sr-only"> (opens in a new tab)</span>}
            </a>
            .
          </p>
        ))}
      </div>
    </div>
  )
}
