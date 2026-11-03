import type { ButtonHTMLAttributes, ReactNode } from 'react'
import { Icon } from './Icon'
import { Spinner } from './Spinner'

type Variant = 'primary' | 'secondary' | 'tonal' | 'ghost' | 'danger'

const variants: Record<Variant, string> = {
  primary: 'bg-primary text-on-primary hover:bg-primary-container hover:shadow-hover',
  secondary: 'bg-white text-ink border border-line-strong hover:bg-slate-50',
  tonal: 'bg-surface-container-low text-ink hover:bg-surface-container',
  ghost: 'bg-transparent text-primary-container hover:bg-accent-surface',
  danger: 'bg-transparent text-danger hover:bg-danger-surface',
}

interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: Variant
  /** sm: 36px (tables, toolbars, cards); md: 44px; lg: 48px (the main action of a page). */
  size?: 'sm' | 'md' | 'lg'
  icon?: string
  trailingIcon?: string
  loading?: boolean
  loadingText?: string
  block?: boolean
  /** Shorter label shown on phones ("Continue" instead of "Continue to review"). */
  shortLabel?: string
  children: ReactNode
}

/**
 * The one button. Lifecycle states from the Stitch "System States" board: default, hover,
 * loading (spinner, clicks blocked so a booking can't be submitted twice) and disabled.
 */
export function Button({
  variant = 'primary',
  size = 'md',
  icon,
  trailingIcon,
  loading = false,
  loadingText,
  block = false,
  shortLabel,
  disabled,
  className = '',
  children,
  ...rest
}: ButtonProps) {
  return (
    <button
      type="button"
      {...rest}
      disabled={disabled || loading}
      aria-busy={loading}
      className={[
        'inline-flex items-center justify-center gap-2 rounded-card font-medium transition-colors duration-150',
        'disabled:cursor-not-allowed disabled:opacity-100',
        size === 'lg' ? 'h-12 px-6 text-body-lg' : size === 'sm' ? 'h-9 px-3 text-body-md' : 'h-11 px-4 text-body-md',
        block ? 'w-full' : '',
        disabled && !loading ? '!bg-line !text-muted !shadow-none border-transparent' : variants[variant],
        loading ? 'opacity-90' : '',
        className,
      ].join(' ')}
    >
      {loading ? <Spinner /> : icon && <Icon name={icon} size={18} />}
      {loading && loadingText ? <span>{loadingText}</span> : shortLabel ? <><span className="sm:hidden">{shortLabel}</span><span className="hidden sm:inline">{children}</span></> : <span>{children}</span>}
      {!loading && trailingIcon && <Icon name={trailingIcon} size={18} />}
    </button>
  )
}
