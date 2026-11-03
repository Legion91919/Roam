import type { ReactNode } from 'react'
import { describeError } from '../../api/client'
import { Banner } from './Banner'
import { cx } from './cx'
import { Icon } from './Icon'
import { Skeleton, type SkeletonSize } from './Skeleton'

// ---------------------------------------------------------------------------------------------
// Feedback: inline notices (Callout) and the loading/error wrapper for a query (QueryState).
// Errors from the API use Banner (with the machine-readable code); everything else is a Callout.
// ---------------------------------------------------------------------------------------------

const calloutTones = {
  info: { box: 'bg-surface-container-low text-on-surface-variant', icon: 'info', iconClass: 'text-ink' },
  success: { box: 'bg-accent-surface/60 text-on-surface-variant', icon: 'check_circle', iconClass: 'text-primary-container' },
  warning: { box: 'border border-warning/20 bg-warning-surface text-ink', icon: 'warning', iconClass: 'text-warning' },
  danger: { box: 'border border-danger/30 bg-danger-surface text-ink', icon: 'report', iconClass: 'text-danger' },
} as const

/** A note in the flow of the page: what happens next, a rule that applies, a warning. */
export function Callout({ tone = 'info', icon, title, children, action, size = 'md', ...rest }: {
  tone?: keyof typeof calloutTones
  icon?: string
  title?: ReactNode
  children?: ReactNode
  action?: ReactNode
  size?: 'md' | 'sm'
  role?: string
  'data-testid'?: string
}) {
  const t = calloutTones[tone]
  return (
    <div {...rest} className={cx('flex items-start gap-3 rounded-card', size === 'sm' ? 'p-3 text-body-sm' : 'p-4 text-body-md', t.box)}>
      <Icon name={icon ?? t.icon} size={size === 'sm' ? 18 : 20} filled={tone === 'success'} className={cx('mt-0.5 shrink-0', t.iconClass)} />
      <div className="min-w-0 flex-1">
        {title && <p className={cx('font-medium', tone === 'danger' ? 'text-danger' : tone === 'success' ? 'text-primary-container' : 'text-ink')}>{title}</p>}
        {children && <div className={title ? 'mt-1' : undefined}>{children}</div>}
      </div>
      {action && <div className="shrink-0">{action}</div>}
    </div>
  )
}

/** The parts of a TanStack Query result that QueryState needs. */
export interface QueryLike {
  isLoading: boolean
  isError: boolean
  error: unknown
  refetch: () => unknown
}

/**
 * Loading and error states for one query, so a page only writes the success state:
 *   <QueryState query={history} loading="panel">{history.data && <VehicleView … />}</QueryState>
 */
export function QueryState({ query, loading = 'block', children }: { query: QueryLike; loading?: SkeletonSize | ReactNode; children: ReactNode }) {
  if (query.isLoading) return typeof loading === 'string' ? <Skeleton size={loading as SkeletonSize} /> : <>{loading}</>
  if (query.isError) return <Banner error={describeError(query.error)} onRetry={() => query.refetch()} />
  return <>{children}</>
}

/** Only the error state of a query (the page shows its own loading placeholders). */
export function QueryError({ query }: { query: QueryLike }) {
  return query.isError ? <Banner error={describeError(query.error)} onRetry={() => query.refetch()} /> : null
}
