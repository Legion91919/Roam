import type { ReactNode } from 'react'
import type { Branch } from '../../api/types'
import type { BookingDraft } from '../../lib/draft'
import { Button, Card, Grid, Heading, IconBadge, Inline, Stack, Text, TileInput, ToggleField } from '../ui'
import { BranchPicker } from './BranchPicker'

/** The four trip date/time tiles (search card and the review page's "Change dates"). */
export function TripDateTiles({ value, onChange }: {
  value: Pick<BookingDraft, 'pickupDate' | 'pickupTime' | 'returnDate' | 'returnTime'>
  onChange: (changes: Partial<BookingDraft>) => void
}) {
  return (
    <Grid columns={{ base: 2, lg: 4 }} gap={3}>
      <TileInput label="Pick-up date" icon="calendar_today" type="date" value={value.pickupDate} onChange={(e) => onChange({ pickupDate: e.target.value })} />
      <TileInput label="Pick-up time" icon="schedule" type="time" value={value.pickupTime} onChange={(e) => onChange({ pickupTime: e.target.value })} />
      <TileInput label="Return date" icon="event_available" type="date" value={value.returnDate} min={value.pickupDate} onChange={(e) => onChange({ returnDate: e.target.value })} />
      <TileInput label="Return time" icon="history" type="time" value={value.returnTime} onChange={(e) => onChange({ returnTime: e.target.value })} />
    </Grid>
  )
}

/** Screen 1 search card. Purely presentational: the page owns the data and the navigation. */
export function SearchCard({ branches, draft, onChange, onSearch, invalidDates }: {
  branches: Branch[] | undefined
  draft: BookingDraft
  onChange: (changes: Partial<BookingDraft>) => void
  onSearch: () => void
  invalidDates: boolean
}) {
  return (
    <Card padding="lg" gap={5} className="shadow-hover">
      <BranchPicker label="Pick up at" branches={branches} value={draft.pickupBranchId} onChange={(v) => onChange({ pickupBranchId: v, category: null })} />
      <Inline gap={3} justify="between">
        <ToggleField checked={draft.oneWay} onChange={(oneWay) => onChange({ oneWay })} label="Return to a different branch" />
        <Text as="span" size="sm" tone="muted" icon="verified">One-way fee R 750,00</Text>
      </Inline>
      {draft.oneWay && (
        <BranchPicker label="Return to" branches={branches} value={draft.returnBranchId || draft.pickupBranchId} onChange={(v) => onChange({ returnBranchId: v })} />
      )}
      <TripDateTiles value={draft} onChange={onChange} />
      <Stack direction="responsive" gap={4} justify="between" align="center">
        <Text size="sm" tone={invalidDates ? 'danger' : 'muted'} icon={invalidDates ? 'warning' : 'info'}>
          {invalidDates ? 'Return must be after pickup.' : 'Every started 24 hours is a day, with a 59-minute grace.'}
        </Text>
        <Button size="lg" trailingIcon="arrow_forward" onClick={onSearch} disabled={invalidDates || !draft.pickupBranchId}>Search cars</Button>
      </Stack>
    </Card>
  )
}

/** The landing hero: tagline, title and the glow behind the search card. */
export function SearchHero({ children }: { children: ReactNode }) {
  return (
    <section className="relative">
      <div className="pointer-events-none absolute -top-40 left-1/2 h-[340px] w-[980px] max-w-full -translate-x-1/2 rounded-full bg-gradient-to-b from-primary-fixed/40 to-transparent blur-3xl" />
      <div className="relative mx-auto max-w-3xl pb-8 pt-6 text-center sm:pt-10">
        <span className="inline-flex items-center gap-2 rounded-full bg-secondary-container px-3 py-1">
          <span className="h-1.5 w-1.5 animate-pulse rounded-full bg-primary" />
          <span className="eyebrow">South Africa gateway mobility</span>
        </span>
        <Heading level={1} size="xl" className="mt-5">Pick up at the airport,<br />drive away in minutes</Heading>
        <Text size="lg" tone="muted" className="mt-3">Airport terminals and city branches across South Africa. Every rand explained up front.</Text>
      </div>
      <div className="relative mx-auto max-w-[880px]">
        <Stack gap={4}>{children}</Stack>
      </div>
    </section>
  )
}

const POINTS = [
  { icon: 'payments', text: 'No hidden fees: every rand explained' },
  { icon: 'event_available', text: 'Free cancellation before pickup' },
  { icon: 'hourglass_bottom', text: '59-minute grace on returns' },
]

export function TrustPoints() {
  return (
    <Grid columns={{ base: 1, sm: 3 }} gap={6} className="mx-auto mt-4 max-w-3xl">
      {POINTS.map((p) => (
        <Stack key={p.text} gap={3} align="center">
          <IconBadge icon={p.icon} size="sm" tone="accent" />
          <Text tone="muted" align="center">{p.text}</Text>
        </Stack>
      ))}
    </Grid>
  )
}

