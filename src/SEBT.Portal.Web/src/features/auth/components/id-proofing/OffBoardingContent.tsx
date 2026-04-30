import Link from 'next/link'

interface OffBoardingContentProps {
  title: string
  body: string
  backHref: string
  backLabel: string
  contactHref: string
  contactLabel: string
  canApply: boolean
  applyBody?: string | undefined
  applySkipBody?: string | undefined
  applyLabel?: string | undefined
  applyHref?: string | undefined
}

export function OffBoardingContent({
  title,
  body,
  backHref,
  backLabel,
  contactHref,
  contactLabel,
  canApply,
  applyBody,
  applySkipBody,
  applyLabel,
  applyHref
}: OffBoardingContentProps) {
  const isExternalLink = contactHref.startsWith('http')

  return (
    <>
      <h1
        id="off-boarding-title"
        className="font-sans-xl text-bold line-height-sans-1 margin-bottom-4"
      >
        {title}
      </h1>

      <p className="font-sans-sm">{body}</p>

      <div className="display-flex flex-row gap-2 margin-y-4">
        <Link
          href={backHref}
          className="usa-button usa-button--outline margin-right-2"
        >
          {backLabel}
        </Link>
        <a
          href={contactHref}
          {...(isExternalLink ? { target: '_blank', rel: 'noopener noreferrer' } : {})}
          className="usa-button"
        >
          {contactLabel}
          {isExternalLink && <span className="usa-sr-only"> (opens in a new tab)</span>}
        </a>
      </div>

      {canApply && (applyBody || applySkipBody || (applyLabel && applyHref)) && (
        <div
          className="margin-top-4"
          data-testid="apply-section"
        >
          {applyBody && <p className="font-sans-sm">{applyBody}</p>}
          {applySkipBody && <p className="font-sans-sm">{applySkipBody}</p>}
          {applyLabel && applyHref && (
            <a
              href={applyHref}
              className="usa-button margin-top-4"
            >
              {applyLabel}
            </a>
          )}
        </div>
      )}
    </>
  )
}
