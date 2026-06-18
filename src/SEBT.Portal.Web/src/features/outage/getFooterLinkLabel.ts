/**
 * Human-readable label for the outage page footer contact link.
 * HTTP(S) URLs show host + path; mailto links show the email address only.
 */
export function getFooterLinkLabel(href: string): string {
  if (href.startsWith('mailto:')) {
    return href.slice('mailto:'.length)
  }

  if (!href.startsWith('http')) {
    return href
  }

  const url = new URL(href)
  const path = url.pathname === '/' ? '' : url.pathname
  return `${url.hostname}${path}`
}
