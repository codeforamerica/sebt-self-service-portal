import type { RoutePageContextMap } from '@sebt/analytics'

export const enrollmentCheckerRoutes: RoutePageContextMap = {
  '/': { name: 'Landing', flow: 'enrollment_checker', step: 'landing' },
  '/disclaimer': { name: 'Disclaimer', flow: 'enrollment_checker', step: 'disclaimer' },
  '/check': { name: 'Check', flow: 'enrollment_checker', step: 'check' },
  '/review': { name: 'Review', flow: 'enrollment_checker', step: 'review' },
  '/results': { name: 'Results', flow: 'enrollment_checker', step: 'results' },
  '/closed': { name: 'Closed', flow: 'enrollment_checker', step: 'closed' }
}
