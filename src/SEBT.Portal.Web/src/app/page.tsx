import Link from 'next/link'

export default function Home() {
  const state = process.env.NEXT_PUBLIC_STATE || 'dc'

  return (
    <>
      <div className="usa-section">
        <div className="grid-container">
          <header
            className="usa-header"
            style={{ marginBottom: '2rem' }}
          >
            <div className="usa-nav-container">
              <div className="usa-navbar">
                <div
                  className="usa-logo"
                  id="basic-logo"
                >
                  <em className="usa-logo__text">
                    <Link
                      href="/"
                      title="Home"
                      aria-label="Home"
                    >
                      SEBT Self-Service Portal
                    </Link>
                  </em>
                </div>
              </div>
            </div>
          </header>

          <main>
            <section>
              <h1 className="usa-prose">USWDS Implementation with Figma Design Tokens - Next.js</h1>

              <div className="usa-alert usa-alert--success">
                <div className="usa-alert__body">
                  <h4 className="usa-alert__heading">✓ Next.js Implementation Working</h4>
                  <p className="usa-alert__text">
                    USWDS is successfully integrated with Next.js using {state.toUpperCase()} state
                    theme tokens from Figma. Build-time token generation creates CSS Variables that
                    are applied to USWDS components.
                  </p>
                </div>
              </div>

              <div style={{ marginTop: '2rem' }}>
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

              <div style={{ marginTop: '2rem' }}>
                <h2 className="usa-prose">USWDS Components with {state.toUpperCase()} Theme</h2>

                <div className="usa-button-group">
                  <button className="usa-button">
                    Primary Button ({state.toUpperCase()} Theme)
                  </button>
                  <button className="usa-button usa-button--secondary">Secondary</button>
                  <button className="usa-button usa-button--outline">Outline</button>
                </div>

                <div
                  className="usa-alert usa-alert--info"
                  style={{ marginTop: '2rem' }}
                >
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

              <div style={{ marginTop: '2rem' }}>
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
          </main>
        </div>
      </div>

      <footer className="usa-footer">
        <div className="usa-footer__primary-section">
          <div className="grid-container">
            <div className="grid-row grid-gap">
              <div className="tablet:grid-col-12">
                <p className="usa-footer__primary-content">
                  SEBT Portal - USWDS with Figma Design Tokens (Next.js)
                </p>
              </div>
            </div>
          </div>
        </div>
      </footer>
    </>
  )
}
