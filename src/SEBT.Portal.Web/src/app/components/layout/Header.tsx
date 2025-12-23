import type { HeaderProps } from '@/src/types/components'
import Image from 'next/image'
import Link from 'next/link'

export function Header({ state = 'dc' }: HeaderProps) {
  return (
    <header
      className="usa-header usa-header--basic bg-white shadow-2"
      role="banner"
    >
      <div className="display-flex flex-justify flex-align-center width-full padding-y-3 padding-left-1 padding-right-3">
        <div className="usa-navbar border-0">
          <div className="usa-logo">
            <Link
              href="/"
              title="Home"
              aria-label="Home"
              className="display-flex flex-align-center"
            >
              <Image
                src={`/images/states/${state}/logo.svg`}
                alt={`${state.toUpperCase()} SUN Bucks`}
                width={121}
                height={51}
                priority
              />
            </Link>
          </div>
        </div>
        <div className="usa-language-container">
          <div className="usa-language__link">
            <div className="display-flex flex-align-center gap-3">
              <Image
                src={`/images/states/${state}/icons/translate_Rounded.svg`}
                alt=""
                width={16}
                height={16}
                aria-hidden="true"
              />
              <span>Translate</span>
            </div>
            <div
              className="display-flex gap-05"
              role="group"
              aria-label="Language selection"
            >
              <button
                type="button"
                lang="en"
                title="English"
                className="usa-button usa-button--unstyled"
              >
                English
              </button>
              <span aria-hidden="true">,&nbsp;</span>
              <button
                type="button"
                lang="es"
                title="Español | Spanish"
                className="usa-button usa-button--unstyled"
              >
                Español
              </button>
              <span aria-hidden="true">,&nbsp;</span>
              <button
                type="button"
                lang="am"
                title="አማርኛ | Amharic"
                className="usa-button usa-button--unstyled"
              >
                አማርኛ
              </button>
            </div>
          </div>
        </div>
      </div>
    </header>
  )
}
