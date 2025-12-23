import { getState } from '@/src/lib/state'
import Link from 'next/link'
import { HelpSection } from '../../components/layout'
import styles from './login.module.css'

export default function LoginPage() {
  const state = getState()
  const stateName = state === 'dc' ? 'DC' : state.toUpperCase()

  return (
    <>
      <div className="usa-section">
        <div className="grid-container maxw-tablet">
          <section
            className={styles.content}
            aria-labelledby="login-title"
          >
            <h1
              id="login-title"
              className={styles.title}
            >
              To see your child&apos;s {stateName} SUN Bucks information, use your email your
              child&apos;s school has on file. If you don&apos;t have access to that email, contact
              the school to update it.
            </h1>

            <p className={styles.instruction}>
              If you applied to {stateName} SUN Bucks using the application form, use the email you
              provided on your application.
            </p>

            <form className={`usa-form ${styles.form}`}>
              <label
                className={`usa-label ${styles.label}`}
                htmlFor="email"
              >
                Enter your email address <span className="text-secondary-dark">*</span>
              </label>
              <input
                className={`usa-input ${styles.input}`}
                id="email"
                name="email"
                type="email"
                autoComplete="email"
                required
                aria-required="true"
              />

              <button
                type="submit"
                className={`usa-button usa-button--full-width margin-top-3 ${styles.button}`}
              >
                Continue
              </button>
            </form>

            <p className={`margin-top-4 ${styles.contact}`}>
              <Link
                href="/contact"
                className={`usa-link ${styles.contactLink}`}
              >
                Contact us
              </Link>{' '}
              if you need assistance logging into your account.
            </p>
          </section>
        </div>
      </div>

      <HelpSection state={state} />
    </>
  )
}
