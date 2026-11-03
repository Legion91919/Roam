import type { ReactNode } from 'react'
import type { DescribedError } from '../../api/client'
import { Banner } from './Banner'
import { Button } from './Button'
import { cx } from './cx'
import { Icon } from './Icon'
import { Skeleton } from './Skeleton'

// ---------------------------------------------------------------------------------------------
// Data display: label/value lists, tables with the four data states, empty states, timelines.
// ---------------------------------------------------------------------------------------------

export interface DataItem {
  label: ReactNode
  value: ReactNode
  /** Monospace value (plates, VINs, ID numbers, references). */
  mono?: boolean
  /** Span the whole row in a multi-column list. */
  wide?: boolean
  /** Secondary line under the value (an email under a name, a time under a date). */
  hint?: ReactNode
  /** Monospace hint (times, codes). */
  hintMono?: boolean
}

const listCols = { 1: '', 2: 'sm:grid-cols-2', 3: 'sm:grid-cols-3', 4: 'sm:grid-cols-2 lg:grid-cols-4' } as const

/**
 * A description list. `stacked`: label above value in a grid (summaries). `rows`: label left,
 * value right, divided rows (fact sheets). `panel` adds the tinted background.
 */
export function DataList({ items, columns = 2, layout = 'stacked', panel = false, size = 'lg' }: {
  items: (DataItem | false | null | undefined)[]
  columns?: keyof typeof listCols
  layout?: 'stacked' | 'rows'
  panel?: boolean
  size?: 'lg' | 'md'
}) {
  const shown = items.filter(Boolean) as DataItem[]
  if (layout === 'rows') {
    return (
      <dl className={cx('divide-y divide-line', panel && 'rounded-card bg-surface-container-low px-5')}>
        {shown.map((item, i) => (
          <div key={i} className="flex justify-between gap-4 py-2.5 text-body-md">
            <dt className="text-secondary">{item.label}</dt>
            <dd className={cx('text-right text-ink', item.mono && 'font-mono text-mono-code')}>{item.value}</dd>
          </div>
        ))}
      </dl>
    )
  }
  return (
    <dl className={cx('grid gap-4', listCols[columns], panel && 'rounded-card bg-surface-container-low p-5')}>
      {shown.map((item, i) => (
        <div key={i} className={cx('min-w-0', item.wide && 'col-span-full')}>
          <dt className="text-body-sm text-secondary">{item.label}</dt>
          <dd className={cx('text-ink', item.mono ? 'font-mono text-mono-code' : size === 'lg' ? 'text-body-lg' : 'text-body-md')}>{item.value}</dd>
          {item.hint && <dd className={cx('text-secondary', item.hintMono ? 'font-mono text-mono-code' : 'text-body-sm')}>{item.hint}</dd>}
        </div>
      ))}
    </dl>
  )
}

/** Nothing to show: an icon, a sentence and optionally a way forward. */
export function EmptyState({ icon = 'inbox', title, message, action, compact = false }: { icon?: string; title?: ReactNode; message: ReactNode; action?: ReactNode; compact?: boolean }) {
  return (
    <div className={cx('flex flex-col items-center gap-2 text-center', compact ? 'px-4 py-6' : 'px-5 py-10')}>
      <Icon name={icon} size={28} className="text-secondary" />
      {title && <p className="text-headline-sm text-ink">{title}</p>}
      <p className="max-w-md text-body-md text-secondary">{message}</p>
      {action && <div className="mt-2">{action}</div>}
    </div>
  )
}

export interface Column<T> {
  key: string
  header: ReactNode
  cell: (row: T) => ReactNode
  align?: 'left' | 'right'
  /** Keep the cell on one line. */
  nowrap?: boolean
  /** Screen-reader-only header (an actions column). */
  srOnlyHeader?: boolean
}

/**
 * A table with the four data states from the Stitch states board: loading (skeleton rows), error
 * (Banner with the API code and Retry), empty (EmptyState) and rows. Scrolls sideways on narrow
 * screens instead of squashing columns.
 */
export function DataTable<T>({ columns, rows, rowKey, onRowClick, loading = false, error = null, onRetry, empty, minWidth = 720, caption }: {
  columns: Column<T>[]
  rows: T[] | undefined
  rowKey: (row: T) => string
  onRowClick?: (row: T) => void
  loading?: boolean
  error?: DescribedError | null
  onRetry?: () => void
  /** Shown when there are no rows (usually an <EmptyState/>). */
  empty?: ReactNode
  minWidth?: number
  caption?: string
}) {
  if (loading) return <div className="space-y-2 p-5" aria-busy="true">{Array.from({ length: 3 }, (_, i) => <Skeleton key={i} size="row" />)}</div>
  if (error) return <div className="p-5"><Banner error={error} onRetry={onRetry} /></div>
  if (!rows) return null
  if (rows.length === 0) return <>{empty ?? <EmptyState message="Nothing to show." />}</>
  return (
    // `relative` keeps absolutely positioned content (the sr-only headers) inside the scroll box on phones.
    <div className="relative overflow-x-auto">
      <table className="w-full text-left text-body-md" style={{ minWidth }}>
        {caption && <caption className="sr-only">{caption}</caption>}
        <thead className="bg-surface-container-low text-label-sm uppercase tracking-wider text-secondary">
          <tr>
            {columns.map((c) => (
              <th key={c.key} scope="col" className={cx('px-5 py-3 font-semibold', c.align === 'right' && 'text-right')}>
                {c.srOnlyHeader ? <span className="sr-only">{c.header}</span> : c.header}
              </th>
            ))}
          </tr>
        </thead>
        <tbody className="divide-y divide-line">
          {rows.map((row) => (
            <tr key={rowKey(row)} onClick={onRowClick ? () => onRowClick(row) : undefined}
              className={cx('align-middle hover:bg-surface-container-low/60', onRowClick && 'cursor-pointer')}>
              {columns.map((c) => (
                <td key={c.key} className={cx('px-5 py-3', c.align === 'right' && 'text-right', c.nowrap && 'whitespace-nowrap')}>{c.cell(row)}</td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

/** Vertical timeline (a car's history). */
export function Timeline({ children }: { children: ReactNode }) {
  return <ol className="relative space-y-6 border-l border-line pl-6">{children}</ol>
}

export function TimelineItem({ icon, title, meta, children }: { icon: string; title: ReactNode; meta?: ReactNode; children?: ReactNode }) {
  return (
    <li className="relative">
      <span className="absolute -left-[37px] flex h-6 w-6 items-center justify-center rounded-full border border-line bg-white text-primary-container"><Icon name={icon} size={16} /></span>
      <div className="flex flex-wrap items-baseline justify-between gap-x-4">
        <p className="font-medium text-ink">{title}</p>
        {meta && <p className="text-body-sm text-secondary">{meta}</p>}
      </div>
      {children && <div className="mt-1 space-y-2">{children}</div>}
    </li>
  )
}

/** "42 cars · page 2 of 3" with Previous / Next, under a paged list. Hidden when everything fits on one page. */
export function Pagination({ page, pageSize, total, noun, onPage }: { page: number; pageSize: number; total: number; noun: string; onPage: (page: number) => void }) {
  const pages = Math.max(1, Math.ceil(total / pageSize))
  if (total <= pageSize) return null
  return (
    <div className="flex items-center justify-between gap-3 border-t border-line px-5 py-3 text-body-sm text-secondary">
      <span>{total} {noun} · page {page} of {pages}</span>
      <div className="flex gap-2">
        <Button variant="secondary" size="sm" icon="chevron_left" disabled={page <= 1} onClick={() => onPage(page - 1)}>Previous</Button>
        <Button variant="secondary" size="sm" trailingIcon="chevron_right" disabled={page >= pages} onClick={() => onPage(page + 1)}>Next</Button>
      </div>
    </div>
  )
}

/** Pass/fail list of requirements ("Every checklist item answered", "Missing photos: left"). */
export function CheckList({ items }: { items: { ok: boolean; text: ReactNode }[] }) {
  return (
    <ul className="space-y-2">
      {items.map((item, i) => (
        <li key={i} className={cx('flex items-center gap-2 text-body-md', item.ok ? 'text-ink' : 'text-danger')}>
          <Icon name={item.ok ? 'check_circle' : 'error'} size={20} className={item.ok ? 'text-primary-container' : ''} /> {item.text}
        </li>
      ))}
    </ul>
  )
}

/** A bulleted list in body text (recorded problems, new damage). */
export function BulletList({ items }: { items: ReactNode[] }) {
  return <ul className="list-disc space-y-1 pl-6 text-body-md text-ink">{items.map((item, i) => <li key={i}>{item}</li>)}</ul>
}
