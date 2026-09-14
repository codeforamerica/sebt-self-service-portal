import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { existsSync, readFileSync } from 'node:fs'
import path from 'node:path'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { CHECKER_STATES } from '@/lib/checkerAssetPath'
import { allowsSequentialChecks } from '@/lib/flowConfig'
import { EnrollmentProvider } from '../context/EnrollmentContext'
import { CheckAnotherChildCard } from './CheckAnotherChildCard'

const mockPush = vi.fn()
vi.mock('next/navigation', () => ({ useRouter: () => ({ push: mockPush }) }))

function renderCard() {
  return render(
    <EnrollmentProvider>
      <CheckAnotherChildCard copy="streamlinedEnrolledCard2" />
    </EnrollmentProvider>
  )
}

describe('CheckAnotherChildCard', () => {
  beforeEach(() => {
    mockPush.mockClear()
    sessionStorage.clear()
  })
  afterEach(() => sessionStorage.clear())

  it('returns the visitor to the child form', async () => {
    renderCard()
    await userEvent.click(screen.getByRole('button'))
    expect(mockPush).toHaveBeenCalledWith('/check')
  })

  // The next check stands alone, so the finished child must not travel with it
  // — nor linger in session storage where the visitor cannot remove it.
  it('drops the finished child on the way out', async () => {
    sessionStorage.setItem(
      'enrollmentState',
      JSON.stringify({
        children: [
          { id: 'done', firstName: 'Jane', lastName: 'Doe', dateOfBirth: '2015-04-12' }
        ],
        editingChildId: null
      })
    )

    renderCard()
    await userEvent.click(screen.getByRole('button'))

    expect(sessionStorage.getItem('enrollmentState')).toBeNull()
  })

  // Once the season closes both outcomes ask the same question in the past tense,
  // and the sheet stores that wording under one key. Only the body moves.
  it('takes a body override while keeping the outcome heading and button', () => {
    render(
      <EnrollmentProvider>
        <CheckAnotherChildCard
          copy="streamlinedEnrolledCard2"
          bodyKey="applyForSebtClosedCard2Body"
        />
      </EnrollmentProvider>
    )

    expect(screen.getByRole('heading', { level: 2 })).toHaveTextContent(
      'streamlinedEnrolledCard2Title'
    )
    expect(screen.getByText('applyForSebtClosedCard2Body')).toBeInTheDocument()
    expect(screen.queryByText('streamlinedEnrolledCard2Body')).toBeNull()
  })
})

// The card reads its copy from the `result` namespace by prefix. A state that
// offers sequential checks without those rows renders raw key names, and since
// the card only appears for some states a single-state run would not catch it.
describe('check-another copy', () => {
  const prefixes = ['streamlinedEnrolledCard2', 'applyForSebtCard2'] as const
  const suffixes = ['Title', 'Body', 'Action'] as const

  afterEach(() => {
    vi.unstubAllEnvs()
  })

  for (const state of CHECKER_STATES) {
    it(`covers every language ${state} ships, when ${state} offers another check`, () => {
      vi.stubEnv('NEXT_PUBLIC_STATE', state)
      if (!allowsSequentialChecks()) {
        return
      }

      for (const lang of ['en', 'es', 'am']) {
        const file = path.join(__dirname, '../../../../content/locales', lang, state, 'result.json')
        // eslint-disable-next-line security/detect-non-literal-fs-filename -- test-only read of generated locale files
        if (!existsSync(file)) {
          continue
        }
        // eslint-disable-next-line security/detect-non-literal-fs-filename -- test-only read of generated locale files
        const copy = JSON.parse(readFileSync(file, 'utf8')) as Record<string, string>

        for (const prefix of prefixes) {
          for (const suffix of suffixes) {
            expect(copy[`${prefix}${suffix}`], `${lang}/${state} missing ${prefix}${suffix}`)
              .toBeTruthy()
          }
        }

        // Both outcomes fall back to this one body once the season has closed.
        expect(
          copy.applyForSebtClosedCard2Body,
          `${lang}/${state} missing applyForSebtClosedCard2Body`
        ).toBeTruthy()
      }
    })
  }
})
