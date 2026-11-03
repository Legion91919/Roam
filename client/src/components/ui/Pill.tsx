import type { ReactNode } from 'react'

type Tone = 'success' | 'warning' | 'danger' | 'neutral' | 'solid'

const tones: Record<Tone, string> = {
  success: 'bg-accent-surface text-primary-container',
  warning: 'bg-warning-surface text-warning',
  danger: 'bg-danger-surface text-danger',
  neutral: 'bg-surface-container-low text-on-surface-variant',
  solid: 'bg-primary text-on-primary',
}

/** Pill-shaped status badge (DESIGN.md "Chips & Badges"): rounded-full, 24px tall. */
export function Pill({ tone = 'neutral', dot = false, children, className = '' }: { tone?: Tone; dot?: boolean; children: ReactNode; className?: string }) {
  return (
    <span className={`inline-flex h-6 items-center gap-1.5 rounded-full px-2.5 text-label-md font-medium ${tones[tone]} ${className}`}>
      {dot && <span className="h-1.5 w-1.5 rounded-full bg-current" />}
      {children}
    </span>
  )
}
