import type { CategoryAvailability } from '../../api/types'
import { CATEGORY_INFO, CATEGORY_LABEL } from '../../lib/catalog'
import { formatMoney } from '../../lib/format'
import { Button, Grid, Heading, Icon, Inline, Money, Pill, Skeleton, Stack, Text } from '../ui'
import { CarIllustration } from './CarIllustration'

function StockPill({ available }: { available: number }) {
  if (available === 0) return <Pill tone="neutral"><Icon name="block" size={14} /> Sold out</Pill>
  if (available === 1) return <Pill tone="danger"><Icon name="schedule" size={14} /> Only 1 left</Pill>
  if (available <= 2) return <Pill tone="warning">{available} left</Pill>
  return <Pill tone="success">{available} left</Pill>
}

function Spec({ icon, children, wide, highlight }: { icon: string; children: string; wide?: boolean; highlight?: boolean }) {
  return (
    <li className={wide ? 'col-span-2' : undefined}>
      <Text as="span" tone={highlight ? 'brand' : 'default'} icon={icon}>{children}</Text>
    </li>
  )
}

/** Screen 2 vehicle card: default, selected and sold-out states. */
export function CategoryCard({ availability, days, selected, onSelect }: {
  availability: CategoryAvailability
  days: number
  selected: boolean
  onSelect: () => void
}) {
  const info = CATEGORY_INFO[availability.category]
  const label = CATEGORY_LABEL[availability.category]
  const soldOut = availability.availableCars === 0

  return (
    <article
      aria-label={`${label}, ${formatMoney(availability.dailyRate)} per day`}
      className={[
        'relative flex flex-col gap-4 rounded-card border p-5 transition-all sm:p-6',
        selected ? 'border-[1.5px] border-primary-container bg-accent-surface/40 shadow-hover' : 'border-line bg-white hover:border-line-strong hover:shadow-hover',
        soldOut ? 'opacity-60' : '',
      ].join(' ')}
    >
      {selected && <span className="absolute inset-x-0 top-0 h-1.5 rounded-t-card bg-primary" />}
      <Inline gap={3} justify="between" align="start">
        <StockPill available={availability.availableCars} />
        <span className="rounded-md bg-surface-container-low px-2 py-1 font-mono text-mono-code text-on-surface-variant">{info.code}</span>
      </Inline>

      <Stack gap={0}>
        <Inline gap={2}>
          <Heading level={3} size="md" tone={soldOut ? 'subtle' : 'strong'}>{label}</Heading>
          {selected && <Pill tone="solid"><Icon name="check" size={14} /> Selected</Pill>}
        </Inline>
        <Text tone="muted">{info.example}</Text>
      </Stack>

      <div className="rounded-card bg-surface-container-low p-4">
        <CarIllustration category={availability.category} muted={soldOut} className="mx-auto h-24 w-full" />
      </div>

      <Grid as="ul" columns={2} gap={2} className="rounded-card bg-surface-container-low/60 p-3">
        <Spec icon="airline_seat_recline_normal">{`${info.seats} seats`}</Spec>
        <Spec icon="luggage">{`${info.bags} bags`}</Spec>
        <Spec icon="settings" highlight={info.transmission === 'Automatic' && !soldOut}>{info.transmission}</Spec>
        {info.features.map((f) => <Spec key={f} icon="check_circle" wide>{f}</Spec>)}
        {info.note && <Spec icon="lock" wide>{info.note}</Spec>}
      </Grid>

      <Stack gap={1} className="mt-auto border-t border-line pt-4">
        <Inline gap={3} justify="between" align="end">
          <Text tone="muted">Per 24h</Text>
          <Money amount={availability.dailyRate} size="display" tone={soldOut ? 'subtle' : selected ? 'brand' : 'strong'} strike={soldOut} />
        </Inline>
        <Text size="sm" tone="muted" align="right">
          From <Money amount={availability.dailyRate * days} size="sm" weight="medium" /> for {days} day{days === 1 ? '' : 's'}
        </Text>
      </Stack>

      <Button block variant={selected ? 'primary' : 'tonal'} icon={selected ? 'check_circle' : soldOut ? 'event_busy' : undefined}
        trailingIcon={selected || soldOut ? undefined : 'arrow_forward'} disabled={soldOut} onClick={onSelect}>
        {soldOut ? 'Sold out for selected dates' : selected ? 'Selected · Continue below' : `Select ${label}`}
      </Button>
    </article>
  )
}

/** Loading state: shimmer cards with the same footprint as the real ones (no layout jump). */
export function CategoryCardSkeleton() {
  return (
    <div className="rounded-card border border-line bg-white p-6" aria-hidden="true">
      <Stack gap={4}>
        <Skeleton size="line" className="w-16" />
        <Skeleton size="title" className="w-32" />
        <Skeleton size="media" />
        <Skeleton size="line" />
        <Skeleton size="field" />
      </Stack>
    </div>
  )
}
