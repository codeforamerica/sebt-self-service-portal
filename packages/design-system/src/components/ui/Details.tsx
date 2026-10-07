'use client'

import { useId, useState } from 'react'

import { Button } from './Button'
import type { DetailsProps } from './types'

/**
 * Disclosure widget: an inline trigger that reveals a tinted panel beneath it.
 *
 * Structurally a USWDS accordion (heading > button > labelled region), but the
 * trigger is an unstyled Button rather than `.usa-accordion__button`, which
 * paints a plus/minus at the far right and is the selector USWDS's accordion JS
 * binds to.
 *
 * `hidden` takes the collapsed panel out of the tab order and the accessibility
 * tree, matching what USWDS does on toggle (uswds-core/src/js/utils/toggle.js).
 */
export function Details({
  summary,
  children,
  icon,
  headingLevel = 2,
  defaultExpanded = false,
  className = ''
}: DetailsProps) {
  const [isExpanded, setIsExpanded] = useState(defaultExpanded)

  // Generated, not passed in: two Details on one page must not share an
  // aria-controls target.
  const generatedId = useId()
  const contentId = `details-${generatedId}`

  const Heading = `h${headingLevel}` as const

  return (
    <div className={`usa-accordion usa-details ${className}`.trim()}>
      <Heading className="usa-accordion__heading usa-details__heading">
        <Button
          variant="unstyled"
          className="usa-details__button"
          aria-expanded={isExpanded}
          aria-controls={contentId}
          onClick={() => setIsExpanded((prev) => !prev)}
        >
          {icon}
          {summary}
          {/* USWDS's `expand_more`, inlined because this package ships no assets
              and the checker deploys under a basePath. Rotation is CSS. */}
          <svg
            className="usa-icon usa-details__chevron"
            viewBox="0 0 24 24"
            aria-hidden="true"
            focusable="false"
          >
            <path d="M16.59 8.59 12 13.17 7.41 8.59 6 10l6 6 6-6z" />
          </svg>
        </Button>
      </Heading>
      <div
        id={contentId}
        className="usa-accordion__content usa-prose usa-details__content"
        hidden={!isExpanded}
      >
        {children}
      </div>
    </div>
  )
}
