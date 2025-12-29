import { HelpSection } from '@/components/layout'
import { getState } from '@/lib/state'

export default function Home() {
  const state = getState()

  return (
    <>
      <div className="usa-section">
        <div className="grid-container">
          <section>
            <h1 className="usa-prose">USWDS Implementation with Figma Design Tokens - Next.js</h1>

            <div className="usa-alert usa-alert--success">
              <div className="usa-alert__body">
                <p className="usa-alert__heading">Next.js Implementation Working</p>
                <p className="usa-alert__text">
                  USWDS is successfully integrated with Next.js using {state.toUpperCase()} state
                  theme tokens from Figma. Build-time token generation creates CSS Variables that
                  are applied to USWDS components.
                </p>
              </div>
            </div>

            <div className="margin-top-4">
              <h2 className="usa-prose">Build Workflow</h2>
              <ol className="usa-list">
                <li>
                  <strong>Figma Tokens Studio</strong> → Export design tokens to{' '}
                  <code>design/states/{state}.json</code>
                </li>
                <li>
                  <strong>Token Transformation</strong> → Transform tokens to CSS Variables in{' '}
                  <code>src/app/tokens.css</code>
                </li>
                <li>
                  <strong>Next.js</strong> → Server-side rendering with build-time state detection
                </li>
                <li>
                  <strong>Hot Reload</strong> → Instant updates during development
                </li>
              </ol>
            </div>

            <div className="margin-top-4">
              <h2 className="usa-prose">USWDS Components with {state.toUpperCase()} Theme</h2>

              <div
                className="usa-button-group"
                role="group"
                aria-label="Theme demonstration buttons"
              >
                <button
                  type="button"
                  className="usa-button"
                >
                  Primary Button ({state.toUpperCase()} Theme)
                </button>
                <button
                  type="button"
                  className="usa-button usa-button--secondary"
                >
                  Secondary
                </button>
                <button
                  type="button"
                  className="usa-button usa-button--outline"
                >
                  Outline
                </button>
              </div>

              <div className="usa-alert usa-alert--info margin-top-4">
                <div className="usa-alert__body">
                  <p className="usa-alert__text">
                    <strong>{state.toUpperCase()} Theme Active:</strong>{' '}
                    {state === 'dc'
                      ? 'Primary color is mint-cool-60v (#0f6460)'
                      : 'Primary color from CO state tokens'}
                    . Typography uses Urbanist font family from Figma tokens.
                  </p>
                </div>
              </div>
            </div>

            <div className="margin-top-4">
              <h2 className="usa-prose">Multi-State Deployment</h2>
              <div className="usa-alert usa-alert--warning">
                <div className="usa-alert__body">
                  <p className="usa-alert__text">
                    This application supports build-time state detection for DC and CO. Each state
                    gets a separate build with state-specific design tokens.
                  </p>
                  <ul className="usa-list">
                    <li>
                      <code>STATE=dc pnpm build</code> → Build for dc.sebt-portal.gov
                    </li>
                    <li>
                      <code>STATE=co pnpm build</code> → Build for co.sebt-portal.gov
                    </li>
                    <li>
                      <code>pnpm build</code> → Build with default state (DC)
                    </li>
                  </ul>
                </div>
              </div>
            </div>
          </section>
        </div>
      </div>

      <HelpSection state={state} />
    </>
  )
}
