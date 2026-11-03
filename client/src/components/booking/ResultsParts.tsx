import type { ReactNode } from 'react'
import type { Branch } from '../../api/types'
import { formatShortDate, formatTime } from '../../lib/format'
import { Button, Card, ChipGroup, EmptyState, Icon, Inline, Stack, Text } from '../ui'

/** Compact "JNB → CPT · 1 Dec 09:00 to 4 Dec 09:00 · 3 days" bar with Edit search. */
export function SearchSummary({ from, to, pickupAt, returnAt, days, onEdit }: { from?: Branch; to?: Branch; pickupAt: string; returnAt: string; days: number; onEdit: () => void }) {
  return (
    <Card padding="sm">
      <Stack direction="responsive" gap={4} justify="between" align="center">
        <Inline gap={4}>
          <span className="flex items-center gap-2 rounded-lg bg-surface-container-low px-3 py-1.5">
            <Icon name="flight_land" size={20} className="text-primary-container" />
            <span className="text-headline-sm text-ink">{from?.code ?? '…'}</span>
            <Icon name="arrow_forward" size={16} className="text-secondary" />
            <span className="text-headline-sm text-ink">{to?.code ?? '…'}</span>
          </span>
          <Inline gap={2}>
            <Icon name="calendar_today" size={18} className="text-secondary" />
            <Text as="span" tone="strong" weight="medium">{formatShortDate(pickupAt)}, {formatTime(pickupAt)}</Text>
            <Text as="span" tone="muted">to</Text>
            <Text as="span" tone="strong" weight="medium">{formatShortDate(returnAt)}, {formatTime(returnAt)}</Text>
            <span className="rounded bg-surface-container px-2 py-0.5 font-mono text-mono-code text-on-surface-variant">{days} day{days === 1 ? '' : 's'}</span>
          </Inline>
        </Inline>
        <Button variant="tonal" size="sm" icon="tune" onClick={onEdit}>Edit search</Button>
      </Stack>
    </Card>
  )
}

export type Filter = 'all' | 'automatic' | 'budget'

export function FilterChips({ value, onChange, total }: { value: Filter; onChange: (f: Filter) => void; total: number }) {
  return (
    <ChipGroup label="Filter categories" value={value} onChange={onChange} options={[
      { value: 'all', label: `All categories (${total})` },
      { value: 'automatic', label: 'Automatic only' },
      { value: 'budget', label: 'Under R 500/day' },
    ]} />
  )
}

/** Screen 6 empty state: nothing free for the dates, with ways forward. */
export function NoResults({ branches, currentBranchId, onTryBranch, onModify }: { branches: Branch[]; currentBranchId: string; onTryBranch: (id: string) => void; onModify: () => void }) {
  return (
    <Card padding="none" className="mx-auto w-full max-w-xl">
      <EmptyState icon="event_busy" title="No vehicles available" message="Nothing available for those dates. Try different dates or another branch." action={
        <Inline gap={2} justify="center">
          <Button size="sm" icon="calendar_month" onClick={onModify}>Modify dates</Button>
          {branches.filter((b) => b.id !== currentBranchId).map((b) => (
            <Button key={b.id} size="sm" variant="secondary" icon="near_me" onClick={() => onTryBranch(b.id)}>Try {b.city} ({b.code})</Button>
          ))}
        </Inline>
      } />
    </Card>
  )
}

/** Sticky bottom bar showing the chosen car and the step's primary action. */
export function SelectionBar({ title, subtitle, step, action }: { title: string; subtitle: string; step: string; action: ReactNode }) {
  return (
    <div className="fixed inset-x-0 bottom-0 z-40 border-t border-line bg-white/95 backdrop-blur">
      <div className="mx-auto max-w-[1360px] px-4 py-3 sm:px-8">
        <Stack direction="row" gap={4} justify="between" align="center">
          <Stack direction="row" gap={3} align="center" grow>
            <span className="hidden h-11 w-11 shrink-0 items-center justify-center rounded-card bg-surface-container-low text-on-surface-variant sm:flex"><Icon name="directions_car" size={22} /></span>
            <Stack gap={0} grow>
              <Text tone="strong" weight="medium" truncate>{title}</Text>
              <Text size="sm" tone="muted" truncate>{subtitle}</Text>
            </Stack>
          </Stack>
          <Inline gap={4}>
            <Text as="span" size="sm" tone="muted" align="right" className="hidden md:block">{step}</Text>
            {action}
          </Inline>
        </Stack>
      </div>
    </div>
  )
}
