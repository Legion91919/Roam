import { useState } from 'react'
import { useAvailability } from '../../api/hooks'
import type { Branch, VehicleCategory } from '../../api/types'
import { CATEGORY_LABEL } from '../../lib/catalog'
import type { BookingDraft } from '../../lib/draft'
import { chargeableDays, formatDay, formatTime, toIsoWithOffset } from '../../lib/format'
import { Button, Card, CardHeader, DataList, FormActions, QueryError, Stack, Text } from '../ui'
import { TripDateTiles } from './SearchCard'

type Dates = Pick<BookingDraft, 'pickupDate' | 'pickupTime' | 'returnDate' | 'returnTime'>

/**
 * "Your trip" on the review page: where and when, with "Change dates" so the customer does not have
 * to start the search again after choosing a car. The new dates are edited as a local copy and only
 * applied when they are valid and the chosen category still has a car for the whole new period
 * (GET /api/availability). Applying updates the draft, so the live quote re-prices by itself. The API
 * re-checks everything when booking (pickup in the past, sold out: 409).
 */
export function TripCard({ draft, category, pickup, dropOff, onApply, onChooseAnotherCar }: {
  draft: BookingDraft
  category: VehicleCategory
  pickup?: Branch
  dropOff?: Branch
  onApply: (dates: Dates) => void
  onChooseAnotherCar: (dates: Dates) => void
}) {
  const [editing, setEditing] = useState<Dates | null>(null)
  const current: Dates = { pickupDate: draft.pickupDate, pickupTime: draft.pickupTime, returnDate: draft.returnDate, returnTime: draft.returnTime }
  const shown = editing ?? current
  const pickupAt = toIsoWithOffset(shown.pickupDate, shown.pickupTime)
  const returnAt = toIsoWithOffset(shown.returnDate, shown.returnTime)

  const problem =
    !pickupAt || !returnAt ? 'Choose a pickup and a return date and time.'
    : new Date(pickupAt).getTime() < Date.now() ? 'Pickup cannot be in the past.'
    : returnAt <= pickupAt ? 'Return must be after pickup.'
    : null
  const changed = editing !== null && (Object.keys(current) as (keyof Dates)[]).some((k) => editing[k] !== current[k])

  // Only asked while editing valid new dates: is a car of this category free for the whole period?
  const availability = useAvailability(editing && !problem ? draft.pickupBranchId : '', pickupAt, returnAt)
  const carsLeft = availability.data?.find((a) => a.category === category)?.availableCars
  const checking = changed && !problem && (availability.isFetching || carsLeft === undefined)
  const soldOut = changed && !problem && !availability.isFetching && carsLeft === 0
  const days = !problem ? chargeableDays(pickupAt, returnAt) : 0
  const route = `${pickup?.name ?? '…'}${dropOff && dropOff.id !== pickup?.id ? ` → ${dropOff.name}` : ''}`

  const status = problem ?? (!changed ? 'Change the dates or times above.'
    : checking ? 'Checking availability…'
    : soldOut ? `No ${CATEGORY_LABEL[category]} car is free for the whole of these dates.`
    : `${days} day${days === 1 ? '' : 's'}: ${CATEGORY_LABEL[category]} available (${carsLeft} left). The price updates when you apply.`)

  return (
    <Card padding="lg" gap={5} data-testid="trip-card">
      <CardHeader title="Your trip" subtitle={route}
        actions={!editing && <Button variant="tonal" size="sm" icon="edit_calendar" onClick={() => setEditing(current)}>Change dates</Button>} />

      {!editing ? (
        <DataList columns={3} items={[
          { label: 'Pick up', value: <Text as="span" size="lg" tone="strong" weight="medium">{formatDay(pickupAt)}</Text>, hint: formatTime(pickupAt), hintMono: true },
          { label: 'Return', value: <Text as="span" size="lg" tone="strong" weight="medium">{formatDay(returnAt)}</Text>, hint: formatTime(returnAt), hintMono: true },
          { label: 'Length', value: <Text as="span" size="lg" tone="strong" weight="medium">{days} day{days === 1 ? '' : 's'}</Text> },
        ]} />
      ) : (
        <Stack gap={4}>
          <TripDateTiles value={editing} onChange={(changes) => setEditing({ ...editing, ...changes })} />
          <Text role="status" tone={problem || soldOut ? 'danger' : changed && !checking ? 'brand' : 'muted'}
            icon={problem || soldOut ? 'warning' : checking ? 'hourglass_top' : 'check_circle'}>
            {status}
          </Text>
          <QueryError query={availability} />
          <FormActions>
            <Button variant="secondary" onClick={() => setEditing(null)}>Cancel</Button>
            {soldOut ? (
              <Button icon="directions_car" onClick={() => { onChooseAnotherCar(editing); setEditing(null) }}>Choose another car for these dates</Button>
            ) : (
              <Button icon="check" disabled={!changed || Boolean(problem) || checking} onClick={() => { onApply(editing); setEditing(null) }}>Update dates</Button>
            )}
          </FormActions>
        </Stack>
      )}
    </Card>
  )
}
