import { cx } from './cx'

const sizes = {
  line: 'h-5 w-full',
  title: 'h-8 w-56',
  field: 'h-11 w-full',
  row: 'h-12 w-full',
  block: 'h-24 w-full',
  media: 'h-32 w-full',
  card: 'h-48 w-full',
  panel: 'h-64 w-full',
  page: 'h-96 w-full',
  avatar: 'h-16 w-16 rounded-full',
} as const

export type SkeletonSize = keyof typeof sizes

/** Grey pulsing placeholder in the shape of the content that is loading. */
export function Skeleton({ size = 'line', className }: { size?: SkeletonSize; className?: string }) {
  return <div aria-hidden="true" className={cx('animate-pulse rounded-lg bg-surface-container', sizes[size], className)} />
}

/** Several placeholders stacked (a list or a form that is loading). */
export function SkeletonStack({ count = 3, size = 'row' }: { count?: number; size?: SkeletonSize }) {
  return <div className="space-y-3" aria-busy="true">{Array.from({ length: count }, (_, i) => <Skeleton key={i} size={size} />)}</div>
}
