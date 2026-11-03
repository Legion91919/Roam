import type { HTMLAttributes, ReactNode } from 'react'
import { cx } from './cx'
import { Icon } from './Icon'
import type { Gap } from './Layout'
import { Eyebrow, Heading, Text } from './Typography'

// ---------------------------------------------------------------------------------------------
// Card: the flat white surface (DESIGN.md "Flat Foundation": 1px border, 10px radius), with a
// header, sections and a footer so pages compose cards instead of styling divs.
// ---------------------------------------------------------------------------------------------

const paddings = { none: '', sm: 'p-4', md: 'p-5 sm:p-6', lg: 'p-6 sm:p-8' } as const
const cardTones = {
  default: 'border-line bg-white',
  accent: 'border-primary-container/40 bg-accent-surface',
  danger: 'border-danger/30 bg-danger-surface',
  muted: 'border-line bg-surface-container-low',
} as const
const gaps: Record<Gap, string> = { 0: 'gap-0', 1: 'gap-1', 2: 'gap-2', 3: 'gap-3', 4: 'gap-4', 5: 'gap-5', 6: 'gap-6', 8: 'gap-8', 10: 'gap-10' }

export interface CardProps extends HTMLAttributes<HTMLDivElement> {
  /** Inner padding. `none` for cards made of CardHeader/CardSection/tables. */
  padding?: keyof typeof paddings
  tone?: keyof typeof cardTones
  /** Lays the children out in a column with this gap. */
  gap?: Gap
  /** `form`: no wider than a comfortable form (staff forms on a wide screen). */
  width?: 'full' | 'form'
  /** Left out when printing (the page prints something else, e.g. an inspection report). */
  screenOnly?: boolean
  children: ReactNode
}

export function Card({ padding = 'md', tone = 'default', gap, width = 'full', screenOnly, className, children, ...rest }: CardProps) {
  return (
    <div {...rest} className={cx('min-w-0 overflow-hidden rounded-card border', cardTones[tone], paddings[padding], width === 'form' && 'max-w-3xl', gap !== undefined && cx('flex flex-col', gaps[gap]), screenOnly && 'print:hidden', className)}>
      {children}
    </div>
  )
}

/**
 * Title row of a card: optional eyebrow, icon, title, subtitle and actions on the right.
 * `divider` turns it into a padded header bar with a bottom border (for padding="none" cards).
 */
export function CardHeader({ title, subtitle, eyebrow, icon, actions, divider = false, size = 'md', level = 2 }: {
  title: ReactNode
  subtitle?: ReactNode
  eyebrow?: ReactNode
  icon?: string
  actions?: ReactNode
  divider?: boolean
  size?: 'lg' | 'md' | 'sm'
  level?: 1 | 2 | 3
}) {
  return (
    <div className={cx('flex flex-wrap items-start justify-between gap-3', divider && 'border-b border-line px-5 py-4 sm:px-6')}>
      <div className="min-w-0">
        {eyebrow && <Eyebrow>{eyebrow}</Eyebrow>}
        <Heading level={level} size={size} icon={icon} className={eyebrow ? 'mt-1' : undefined}>{title}</Heading>
        {subtitle && <Text tone="muted" className="mt-1">{subtitle}</Text>}
      </div>
      {actions && <div className="flex flex-wrap items-center gap-2 print:hidden">{actions}</div>}
    </div>
  )
}

const sectionPaddings = { none: '', sm: 'px-5 py-3 sm:px-6', md: 'px-5 py-5 sm:px-6', lg: 'px-6 py-6 sm:px-10' } as const

/** A horizontal band of a card, optionally separated by a top border or on a tinted background. */
export function CardSection({ padding = 'md', divider = false, attached = false, tone = 'default', gap, className, children, ...rest }: HTMLAttributes<HTMLDivElement> & {
  padding?: keyof typeof sectionPaddings
  divider?: boolean
  /** Continues the section above it: no top padding, no divider. */
  attached?: boolean
  tone?: 'default' | 'muted'
  gap?: Gap
  children: ReactNode
}) {
  return (
    <div {...rest} className={cx(sectionPaddings[padding], divider && 'border-t border-line', attached && '!pt-0', tone === 'muted' && 'bg-surface-container-low/50', gap !== undefined && cx('flex flex-col', gaps[gap]), className)}>
      {children}
    </div>
  )
}

/** Bottom band of a card with the card's actions, split left/right on wider screens. */
export function CardFooter({ children, padding = 'md', tone = 'default', justify = 'between' }: {
  children: ReactNode
  padding?: keyof typeof sectionPaddings
  tone?: 'default' | 'muted'
  justify?: 'between' | 'end'
}) {
  return (
    <div className={cx('flex flex-col gap-3 border-t border-line sm:flex-row sm:flex-wrap sm:items-center', justify === 'end' ? 'sm:justify-end' : 'sm:justify-between', sectionPaddings[padding], tone === 'muted' && 'bg-surface-container-low/50')}>
      {children}
    </div>
  )
}

const badgeSizes = { xs: 'h-6 w-6', sm: 'h-9 w-9', md: 'h-11 w-11', lg: 'h-16 w-16' } as const
const badgeTones = {
  brand: 'bg-primary-fixed-dim/70 text-primary',
  neutral: 'bg-surface-container-low text-primary-container',
  accent: 'bg-accent-surface text-primary-container',
  danger: 'bg-danger-surface text-danger ring-8 ring-danger-surface/50',
  outline: 'border border-line bg-white text-primary-container',
} as const

/** An icon in a circle (status heroes, branch rows, timeline markers). */
export function IconBadge({ icon, size = 'md', tone = 'neutral', className }: { icon: string; size?: keyof typeof badgeSizes; tone?: keyof typeof badgeTones; className?: string }) {
  return (
    <span className={cx('flex shrink-0 items-center justify-center rounded-full', badgeSizes[size], badgeTones[tone], className)}>
      <Icon name={icon} size={size === 'lg' ? 30 : size === 'md' ? 22 : size === 'sm' ? 18 : 16} />
    </span>
  )
}

/** A whole card that is a link (a booking in a list): lifts on hover, keyboard-focusable. */
export function LinkCard({ href, padding = 'sm', children, ...rest }: { href: string; padding?: keyof typeof paddings; children: ReactNode; 'aria-label'?: string }) {
  return (
    <a href={href} {...rest} className="group block rounded-card focus-visible:outline focus-visible:outline-2 focus-visible:outline-primary-container">
      <div className={cx('rounded-card border border-line bg-white transition-shadow group-hover:shadow-hover', paddings[padding])}>{children}</div>
    </a>
  )
}
