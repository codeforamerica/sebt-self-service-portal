'use client'

import { MaintenanceIcon } from '@/features/outage/components/MaintenanceIcon'
import { getFooterLinkLabel } from '@/features/outage/getFooterLinkLabel'
import { getOutageFooterCopy, getOutageMessages } from '@/features/outage/getOutageMessages'
import {
  getSiteDisplayName,
  getState,
  getStateAssetPath,
  getStateLinks,
  type StateCode
} from '@sebt/design-system'
import Image from 'next/image'

const DEFAULT_LOGO_DIMENSIONS = { width: 122, height: 52 } as const

const logoDimensions: Record<StateCode, { width: number; height: number }> = {
  dc: { width: 122, height: 52 },
  co: { width: 192, height: 28 }
}

export function OutagePageContent() {
  const state = getState()
  const messages = getOutageMessages()
  const footerCopy = getOutageFooterCopy()
  const links = getStateLinks(state)
  const contactHref =
    links.help.contactUs !== '#' ? links.help.contactUs : (links.help.helpDeskEmail ?? '#')
  const isExternalLink = contactHref.startsWith('http')
  const footerLinkLabel = getFooterLinkLabel(contactHref)
  const { width, height } = logoDimensions[state] ?? DEFAULT_LOGO_DIMENSIONS

  const primaryMessage =
    messages.find((message) => message.language === 'en')?.body1 ?? messages[0]?.body1

  return (
    <div className="display-flex flex-align-center flex-justify-center minh-viewport padding-y-4 padding-x-2">
      <div className="width-full maxw-mobile-lg text-center text-base-dark">
        <MaintenanceIcon />

        <h1 className="usa-sr-only">{primaryMessage ?? 'Maintenance'}</h1>

        <div className="display-flex flex-column gap-4 margin-bottom-5">
          {messages.map((message) => (
            <section
              key={message.language}
              lang={message.language}
              aria-label={message.language}
            >
              {message.body1 && (
                <p className="margin-top-0 margin-bottom-1 font-body-md line-height-body-3">
                  {message.body1}
                </p>
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

        <p className="margin-0 font-body-sm line-height-body-3">
          {footerCopy.prefix}{' '}
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
      </div>
    </div>
  )
}
