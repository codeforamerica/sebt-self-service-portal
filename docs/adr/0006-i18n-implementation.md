# ADR 0006: Internationalization (i18n) Implementation

## Status
Accepted

## Context
Users need to view the UI in English and Spanish, with different copy for each state (DC, CO). Content is managed in a Google Sheet that is updated regularly and downloaded to the repository periodically.

## Decision
Use `react-i18next` with `i18next` for internationalization.

### Why `react-i18next` over `next-intl`

| Factor | `next-intl` | `react-i18next` |
|--------|-------------|-----------------|
| **Ecosystem** | Next.js only | Universal (React Native, Electron, etc.) |
| **Maturity** | ~3 years | 10+ years, battle-tested |
| **Plugins** | Limited | Rich ecosystem (ICU, backend loaders, etc.) |
| **Interpolation** | Basic | Full ICU MessageFormat (plurals, gender, etc.) |
| **SSR support** | Built-in | Supported with config |
| **Community** | Growing | Massive, well-documented |
| **Namespace loading** | All-or-nothing | Lazy load per page/component |

Reference: https://react.i18next.com/guides/the-drawbacks-of-other-i18n-solutions

### Architecture

```
Google Sheet (source of truth)
    ↓ (manual download as CSV)
content/
├── copy.csv                    # Downloaded sheet (committed)
└── locales/                    # Generated at build time (gitignored)
    ├── en/
    │   ├── dc/
    │   │   ├── common.json     # Shared strings (header, footer)
    │   │   └── landing.json    # Page-specific
    │   └── co/
    └── es/
        ├── dc/
        └── co/
```

### Key Structure

CSV row:
```
Content,English,Español
S1 - Landing Page - Title,Get a one-time payment...,Obtenga un pago único...
```

Generated JSON (`content/locales/en/dc/landing.json`):
```json
{
  "title": "Get a one-time payment of $120 in summer food benefits for your child",
  "body": "Each eligible child can receive a *one-time payment of $120*...",
  "action": "Do I need to apply?"
}
```

### Commands

```bash
pnpm copy:generate      # CSV → JSON per locale/state/namespace
pnpm copy:validate      # Ensure all keys exist across locales
pnpm copy:extract       # Extract keys from code (i18next-parser)
```

### Runtime Usage

```tsx
import { useTranslation } from 'react-i18next'

function LandingPage() {
  const { t } = useTranslation('landing')

  return (
    <>
      <h1>{t('title')}</h1>
      <p>{t('body')}</p>
      <button>{t('action')}</button>
    </>
  )
}
```

## Implementation Phases

1. **Phase 1**: Generation script (CSV → namespaced JSON)
2. **Phase 2**: Install `react-i18next`, `i18next`, configure
3. **Phase 3**: Add `I18nextProvider` to layout
4. **Phase 4**: Replace hardcoded strings with `t()` calls
5. **Phase 5**: Wire language buttons to `i18n.changeLanguage()`

## Consequences

### Positive
- Battle-tested solution with extensive documentation
- Flexible namespace loading for performance
- ICU MessageFormat support for complex pluralization
- Easy to add new languages/states
- Content team can update copy without code changes

### Negative
- Requires build step to generate JSON from CSV
- Additional client-side JavaScript bundle
- Need to maintain key consistency between sheet and code

### Neutral
- Language persisted via cookie/localStorage
- State determined at build time (existing pattern)
