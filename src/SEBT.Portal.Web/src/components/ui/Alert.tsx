import type { AlertProps, AlertVariant } from './types'

const variantClasses: Record<AlertVariant, string> = {
  info: 'usa-alert--info',
  success: 'usa-alert--success',
  warning: 'usa-alert--warning',
  error: 'usa-alert--error',
  emergency: 'usa-alert--emergency'
}

export function Alert({
  variant = 'info',
  heading,
  children,
  slim = false,
  noIcon = false,
  className = ''
}: AlertProps) {
  const baseClass = 'usa-alert'
  // eslint-disable-next-line security/detect-object-injection -- variant is typed AlertVariant
  const variantClass = variantClasses[variant]
  const slimClass = slim ? 'usa-alert--slim' : ''
  const noIconClass = noIcon ? 'usa-alert--no-icon' : ''
  const combinedClassName =
    `${baseClass} ${variantClass} ${slimClass} ${noIconClass} ${className}`.trim()

  return (
    <div
      className={combinedClassName}
      role="alert"
    >
      <div className="usa-alert__body">
        {heading && <h4 className="usa-alert__heading">{heading}</h4>}
        <p className="usa-alert__text">{children}</p>
      </div>
    </div>
  )
}
