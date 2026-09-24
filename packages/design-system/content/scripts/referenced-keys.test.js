import { describe, expect, it } from 'vitest'
import { collectReferences, createIsReferenced } from './referenced-keys.js'

function isReferencedIn(...texts) {
  const files = texts.map((text, i) => ({ path: `File${i}.tsx`, text }))
  return createIsReferenced(collectReferences(files))
}

describe('createIsReferenced', () => {
  it('finds a literal key in the namespace the file translates with', () => {
    const isReferenced = isReferencedIn(`
      const { t } = useTranslation('landing')
      return <RichText>{t('body4')}</RichText>
    `)

    expect(isReferenced('landing', 'body4')).toBe(true)
  })

  it('does not count the same key name in a namespace no file uses', () => {
    const isReferenced = isReferencedIn(`
      const { t } = useTranslation('landing')
      return <p>{t('body4')}</p>
    `)

    expect(isReferenced('disclaimer', 'body4')).toBe(false)
  })

  it('does not count a key that no call names', () => {
    const isReferenced = isReferencedIn(`
      const { t: tDev } = useTranslation('dev')
      return <p>{tDev('alertSuggestion', { count })}</p>
    `)

    expect(isReferenced('dev', 'alertMoreEntries')).toBe(false)
  })

  it('follows an aliased translate function', () => {
    const isReferenced = isReferencedIn(`
      const { t: tDev } = useTranslation('dev')
      return <p>{tDev('alertSuggestion', { count })}</p>
    `)

    expect(isReferenced('dev', 'alertSuggestion')).toBe(true)
  })

  it('reads an explicit namespace prefix on the key', () => {
    const isReferenced = isReferencedIn(`
      const { t } = useTranslation('dashboard')
      return <p>{t('common:buttonContinue')}</p>
    `)

    expect(isReferenced('common', 'buttonContinue')).toBe(true)
    expect(isReferenced('dashboard', 'buttonContinue')).toBe(false)
  })

  it('counts a key for every namespace a file translates with', () => {
    const isReferenced = isReferencedIn(`
      const { t } = useTranslation('result')
      const { t: tCommon } = useTranslation('common')
      return <h1>{t('title')}</h1>
    `)

    expect(isReferenced('result', 'title')).toBe(true)
    expect(isReferenced('common', 'title')).toBe(true)
  })

  it('treats every key of a namespace as referenced when a call builds its key at runtime', () => {
    const isReferenced = isReferencedIn(`
      const { t } = useTranslation('personalInfo')
      return <label>{t(field.labelKey)}</label>
    `)

    expect(isReferenced('personalInfo', 'anythingAtAll')).toBe(true)
    expect(isReferenced('landing', 'anythingAtAll')).toBe(false)
  })

  it('treats a template-literal key as built at runtime', () => {
    const isReferenced = isReferencedIn(`
      const { t } = useTranslation('result')
      return <p>{t(\`status\${kind}\`)}</p>
    `)

    expect(isReferenced('result', 'statusApproved')).toBe(true)
  })

  it('counts a literal key for any namespace when the file names no namespace', () => {
    const isReferenced = isReferencedIn(`
      export function Selector({ t }) {
        return <button aria-label={t('languageSelector')} />
      }
    `)

    expect(isReferenced('common', 'languageSelector')).toBe(true)
    expect(isReferenced('landing', 'languageSelector')).toBe(true)
  })

  it('opens the default namespace when a file with no namespace builds its key at runtime', () => {
    const isReferenced = isReferencedIn(`
      export function Selector({ t, languages }) {
        return languages.map((lang) => <li>{t(lang.key)}</li>)
      }
    `)

    expect(isReferenced('common', 'languageSpanish')).toBe(true)
    expect(isReferenced('landing', 'title')).toBe(false)
  })

  it('does not mistake other functions ending in t for a translate call', () => {
    const isReferenced = isReferencedIn(`
      const { t } = useTranslation('landing')
      const value = format('body4')
      const other = parseInt('title')
      return <p>{t('title')}</p>
    `)

    expect(isReferenced('landing', 'body4')).toBe(false)
    expect(isReferenced('landing', 'title')).toBe(true)
  })

  it('combines references across files', () => {
    const isReferenced = isReferencedIn(
      `const { t } = useTranslation('landing'); t('title')`,
      `const { t } = useTranslation('login'); t('body')`
    )

    expect(isReferenced('landing', 'title')).toBe(true)
    expect(isReferenced('login', 'body')).toBe(true)
    expect(isReferenced('login', 'title')).toBe(false)
  })
})
