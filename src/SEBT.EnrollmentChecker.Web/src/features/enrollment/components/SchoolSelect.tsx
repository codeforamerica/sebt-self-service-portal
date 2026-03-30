'use client'

import { useTranslation } from 'react-i18next'
import { useSchools } from '../hooks/useSchools'

interface SchoolSelectProps {
  enabled: boolean
  apiBaseUrl: string
  value: string
  onChange: (code: string, name: string) => void
}

export function SchoolSelect({ enabled, apiBaseUrl, value, onChange }: SchoolSelectProps) {
  const { t } = useTranslation('personalInfo')
  const { data: schools, isLoading, isError } = useSchools({ enabled, apiBaseUrl })

  if (!enabled) return null

  return (
    <div className="usa-form-group">
      <label className="usa-label" htmlFor="school-select">
        {t('schoolLabel')}
      </label>
      {isLoading ? (
        <p className="usa-prose">{t('schoolLoading', { ns: 'common' })}</p>
      ) : isError ? (
        <p className="usa-error-message" role="alert">{t('schoolError', { ns: 'common' })}</p>
      ) : (
        <select
          id="school-select"
          className="usa-select"
          value={value}
          onChange={e => {
            const school = schools?.find(s => s.code === e.target.value)
            onChange(e.target.value, school?.name ?? '')
          }}
        >
          <option value="">{t('schoolSelectPlaceholder')}</option>
          {schools?.map(school => (
            <option key={school.code} value={school.code}>{school.name}</option>
          ))}
        </select>
      )}
    </div>
  )
}
