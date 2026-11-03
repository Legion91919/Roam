import type { ElementType, HTMLAttributes, MouseEvent, ReactNode } from 'react'
import { formatMoney } from '../../lib/format'
import { cx } from './cx'
import { Icon } from './Icon'

// ---------------------------------------------------------------------------------------------
// Typography: the DESIGN.md type scale (headline-xl … body-sm, label, mono) and its colour roles,
// as components. Pages never pick a font size or a text colour themselves.
// ---------------------------------------------------------------------------------------------

export type Tone = 'default' | 'strong' | 'muted' | 'subtle' | 'brand' | 'danger' | 'warning' | 'inverse'

const tones: Record<Tone, string> = {
  default: 'text-on-surface-variant',
  strong: 'text-ink',
  muted: 'text-secondary',
  subtle: 'text-muted',
  brand: 'text-primary-container',
  danger: 'text-danger',
  warning: 'text-warning',
  inverse: 'text-inverse-on-surface',
}

const headingSizes = {
  xl: 'text-headline-xl-mobile sm:text-headline-xl',
  lg: 'text-headline-lg',
  md: 'text-headline-md',
  sm: 'text-headline-sm',
} as const

/** Section and page titles. `level` is the HTML outline (h1-h4); `size` is the visual size. */
export function Heading({ level = 2, size = 'md', icon, tone = 'strong', tabular, className, children, ...rest }: HTMLAttributes<HTMLHeadingElement> & {
  level?: 1 | 2 | 3 | 4
  /** Tabular figures (a total, a distance). */
  tabular?: boolean
  size?: keyof typeof headingSizes
  icon?: string
  tone?: Tone
  children: ReactNode
}) {
  const As = `h${level}` as ElementType
  return (
    <As {...rest} className={cx(headingSizes[size], tones[tone], icon && 'flex items-center gap-2', tabular && 'tabular', className)}>
      {icon && <Icon name={icon} size={size === 'sm' ? 20 : 22} />}
      {children}
    </As>
  )
}

const textSizes = { lg: 'text-body-lg', md: 'text-body-md', sm: 'text-body-sm', label: 'text-label-md' } as const

export interface TextProps extends HTMLAttributes<HTMLElement> {
  as?: 'p' | 'span' | 'div' | 'dt' | 'dd' | 'li' | 'label' | 'strong'
  size?: keyof typeof textSizes
  tone?: Tone
  weight?: 'regular' | 'medium'
  /** Monospace (references, plates, VINs, times): JetBrains Mono at the code size. */
  mono?: boolean
  /** Tabular figures, so amounts and odometers line up. */
  tabular?: boolean
  align?: 'left' | 'center' | 'right'
  truncate?: boolean
  strike?: boolean
  /** Leading icon, aligned with the text. */
  icon?: string
  children?: ReactNode
}

/** Body text in the type scale and a colour role. */
export function Text({ as: As = 'p', size = 'md', tone = 'default', weight = 'regular', mono, tabular, align, truncate, strike, icon, className, children, ...rest }: TextProps) {
  return (
    <As
      {...rest}
      className={cx(
        mono ? (size === 'lg' ? 'font-mono text-headline-sm tracking-wider' : 'font-mono text-mono-code') : textSizes[size],
        tones[tone],
        weight === 'medium' && 'font-medium',
        tabular && 'tabular',
        align === 'center' && (icon ? 'justify-center text-center' : 'text-center'),
        align === 'right' && 'text-right',
        truncate && 'truncate',
        strike && 'line-through',
        icon && 'flex items-center gap-2',
        className,
      )}
    >
      {icon && <Icon name={icon} size={size === 'sm' ? 16 : 18} className="shrink-0" />}
      {icon ? <span className="min-w-0">{children}</span> : children}
    </As>
  )
}

/** Small uppercase label above a title or a value ("STEP 4 OF 4", "BOOKING REFERENCE"). */
export function Eyebrow({ tone = 'muted', children, className }: { tone?: Tone; children: ReactNode; className?: string }) {
  return <p className={cx('text-label-sm uppercase tracking-wider', tones[tone], className)}>{children}</p>
}

const moneySizes = { sm: 'text-body-sm', md: 'text-body-md', lg: 'text-body-lg', xl: 'text-headline-md', display: 'text-headline-lg' } as const

/** An amount in South African notation (R 1 350,00) with tabular figures. */
export function Money({ amount, size = 'md', tone = 'strong', strike, weight = 'regular', as: As = 'span' }: {
  amount: number
  size?: keyof typeof moneySizes
  tone?: Tone
  strike?: boolean
  weight?: 'regular' | 'medium'
  as?: 'span' | 'p' | 'dd'
}) {
  return <As className={cx('tabular', moneySizes[size], tones[tone], strike && 'line-through', weight === 'medium' && 'font-medium')}>{formatMoney(amount)}</As>
}

/** A link styled as text (in-app hash links or callbacks). */
export function TextLink({ href, onClick, icon, children, tone = 'brand', size = 'md' }: {
  href?: string
  onClick?: (event: MouseEvent) => void
  icon?: string
  children: ReactNode
  tone?: Tone
  size?: 'md' | 'sm'
}) {
  const className = cx('inline-flex items-center gap-1.5 hover:underline', size === 'sm' ? 'text-body-sm' : 'text-body-md', tones[tone])
  const content = <>{icon && <Icon name={icon} size={size === 'sm' ? 16 : 18} />}{children}</>
  return href
    ? <a href={href} onClick={onClick} className={className}>{content}</a>
    : <button type="button" onClick={onClick} className={className}>{content}</button>
}

/** "← Back to …" at the top of a page. */
export function BackLink({ href, onClick, children }: { href?: string; onClick?: () => void; children: ReactNode }) {
  return <div><TextLink href={href} onClick={onClick} icon="arrow_back">{children}</TextLink></div>
}
