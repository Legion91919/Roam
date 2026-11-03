import type { ElementType, HTMLAttributes, ReactNode } from 'react'
import { cx } from './cx'

// ---------------------------------------------------------------------------------------------
// Layout primitives. Pages arrange content with these instead of hand-written flex/grid classes,
// so spacing always comes from one scale (Tailwind's 4px steps, as DESIGN.md specifies).
// ---------------------------------------------------------------------------------------------

export type Gap = 0 | 1 | 2 | 3 | 4 | 5 | 6 | 8 | 10

const gaps: Record<Gap, string> = { 0: 'gap-0', 1: 'gap-1', 2: 'gap-2', 3: 'gap-3', 4: 'gap-4', 5: 'gap-5', 6: 'gap-6', 8: 'gap-8', 10: 'gap-10' }
const aligns = { start: 'items-start', center: 'items-center', end: 'items-end', baseline: 'items-baseline', stretch: 'items-stretch' } as const
const justifies = { start: 'justify-start', center: 'justify-center', end: 'justify-end', between: 'justify-between' } as const
type Align = keyof typeof aligns
// A responsive stack is a plain column on phones; its alignment describes the row (sm and up).
const smAligns = { start: 'sm:items-start', center: 'sm:items-center', end: 'sm:items-end', baseline: 'sm:items-baseline', stretch: 'sm:items-stretch' } as const
const smJustifies = { start: 'sm:justify-start', center: 'sm:justify-center', end: 'sm:justify-end', between: 'sm:justify-between' } as const
const rowGaps: Record<Gap, string> = { 0: 'gap-y-0', 1: 'gap-y-1', 2: 'gap-y-2', 3: 'gap-y-3', 4: 'gap-y-4', 5: 'gap-y-5', 6: 'gap-y-6', 8: 'gap-y-8', 10: 'gap-y-10' }

interface StackProps extends HTMLAttributes<HTMLElement> {
  as?: ElementType
  gap?: Gap
  /** Vertical gap between wrapped lines, when it should differ from `gap` (a wrapping row of facts). */
  rowGap?: Gap
  /** column (default) | row | "responsive": a column on phones, a row from the sm breakpoint
   *  (`align` and `justify` then apply to the row only). */
  direction?: 'column' | 'row' | 'responsive'
  /** Cross-axis alignment; `{ base, sm }` to change it from the sm breakpoint up. */
  align?: Align | { base?: Align; sm?: Align }
  justify?: keyof typeof justifies
  wrap?: boolean
  /** Take the remaining width in a row (flex-1, min-w-0 so long text truncates instead of overflowing). */
  grow?: boolean
  children?: ReactNode
}

/** Flexbox stack: children in a column or a row with consistent gaps. */
export function Stack({ as: As = 'div', gap = 4, rowGap, direction = 'column', align, justify, wrap, grow, className, children, ...rest }: StackProps) {
  return (
    <As
      {...rest}
      className={cx(
        'flex',
        direction === 'column' && 'flex-col',
        direction === 'row' && 'flex-row',
        direction === 'responsive' && 'flex-col sm:flex-row',
        gaps[gap],
        rowGap !== undefined && rowGaps[rowGap],
        typeof align === 'string' && (direction === 'responsive' ? smAligns : aligns)[align],
        typeof align === 'object' && cx(align.base && aligns[align.base], align.sm && smAligns[align.sm]),
        justify && (direction === 'responsive' ? smJustifies : justifies)[justify],
        wrap && 'flex-wrap',
        grow && 'min-w-0 flex-1',
        className,
      )}
    >
      {children}
    </As>
  )
}

/** Empty vertical space (room for a fixed bar at the bottom of the screen). */
export function Spacer({ size = 'md' }: { size?: 'sm' | 'md' | 'lg' }) {
  return <div aria-hidden="true" className={size === 'sm' ? 'h-6' : size === 'lg' ? 'h-24' : 'h-16'} />
}

/** A wrapping row of items, vertically centred (chips, buttons, label + value). */
export function Inline({ gap = 2, align = 'center', ...rest }: Omit<StackProps, 'direction' | 'wrap'>) {
  return <Stack direction="row" wrap gap={gap} align={align} {...rest} />
}

type Columns = 1 | 2 | 3 | 4
const cols: Record<'base' | 'sm' | 'md' | 'lg' | 'xl', Record<Columns, string>> = {
  base: { 1: 'grid-cols-1', 2: 'grid-cols-2', 3: 'grid-cols-3', 4: 'grid-cols-4' },
  sm: { 1: 'sm:grid-cols-1', 2: 'sm:grid-cols-2', 3: 'sm:grid-cols-3', 4: 'sm:grid-cols-4' },
  md: { 1: 'md:grid-cols-1', 2: 'md:grid-cols-2', 3: 'md:grid-cols-3', 4: 'md:grid-cols-4' },
  lg: { 1: 'lg:grid-cols-1', 2: 'lg:grid-cols-2', 3: 'lg:grid-cols-3', 4: 'lg:grid-cols-4' },
  xl: { 1: 'xl:grid-cols-1', 2: 'xl:grid-cols-2', 3: 'xl:grid-cols-3', 4: 'xl:grid-cols-4' },
}

export type ResponsiveColumns = Columns | Partial<Record<'base' | 'sm' | 'md' | 'lg' | 'xl', Columns>>

interface GridProps extends HTMLAttributes<HTMLElement> {
  as?: ElementType
  /** A number (same at every width) or per breakpoint, e.g. { base: 1, sm: 2, xl: 3 }. */
  columns?: ResponsiveColumns
  gap?: Gap
  align?: keyof typeof aligns
  children?: ReactNode
}

/** CSS grid with responsive column counts. Children can span all columns with <GridItem span="full">. */
export function Grid({ as: As = 'div', columns = 1, gap = 4, align, className, children, ...rest }: GridProps) {
  const c = typeof columns === 'number' ? { base: columns } : { base: 1 as Columns, ...columns }
  return (
    <As
      {...rest}
      className={cx(
        'grid min-w-0',
        (Object.keys(c) as (keyof typeof cols)[]).map((bp) => cols[bp][c[bp]!]).join(' '),
        gaps[gap],
        align && aligns[align],
        className,
      )}
    >
      {children}
    </As>
  )
}

/** A grid cell that spans every column (a wide field in a two-column form, a full-width row). */
export function GridItem({ span = 'full', className, children, ...rest }: HTMLAttributes<HTMLDivElement> & { span?: 'full' }) {
  return <div {...rest} className={cx(span === 'full' && 'col-span-full', 'min-w-0', className)}>{children}</div>
}

/**
 * Main content + a side column (a summary, a car's facts). Stacks on small screens; side by side
 * from `breakpoint`. The aside can stick to the top while the main column scrolls.
 */
export function SplitLayout({ aside, children, asidePosition = 'end', asideWidth = 'md', breakpoint = 'lg', sticky = false, gap = 6 }: {
  aside: ReactNode
  children: ReactNode
  asidePosition?: 'start' | 'end'
  /** sm: 360px; md: a third of the width */
  asideWidth?: 'sm' | 'md'
  breakpoint?: 'lg' | 'xl'
  sticky?: boolean
  gap?: Gap
}) {
  const template = {
    lg: { sm: { end: 'lg:grid-cols-[minmax(0,1fr)_360px]', start: 'lg:grid-cols-[360px_minmax(0,1fr)]' }, md: { end: 'lg:grid-cols-[minmax(0,2fr)_minmax(0,1fr)]', start: 'lg:grid-cols-[minmax(0,1fr)_minmax(0,2fr)]' } },
    xl: { sm: { end: 'xl:grid-cols-[minmax(0,1fr)_360px]', start: 'xl:grid-cols-[360px_minmax(0,1fr)]' }, md: { end: 'xl:grid-cols-[minmax(0,2fr)_minmax(0,1fr)]', start: 'xl:grid-cols-[minmax(0,1fr)_minmax(0,2fr)]' } },
  }[breakpoint][asideWidth][asidePosition]
  const stickyClass = sticky ? (breakpoint === 'lg' ? 'lg:sticky lg:top-24' : 'xl:sticky xl:top-24') : ''
  const side = <aside className={cx('flex min-w-0 flex-col gap-4 self-start', stickyClass, breakpoint === 'lg' ? 'w-full' : 'w-full')}>{aside}</aside>
  return (
    <div className={cx('grid min-w-0 grid-cols-1 items-start', gaps[gap], template)}>
      {asidePosition === 'start' && side}
      <div className="flex min-w-0 flex-col gap-6">{children}</div>
      {asidePosition === 'end' && side}
    </div>
  )
}
