import type { ReactNode } from 'react'
import type { CounterBooking } from '../../api/types'
import { CATEGORY_INFO, CATEGORY_LABEL } from '../../lib/catalog'
import { formatDay, formatTime } from '../../lib/format'
import { CarThumb } from '../booking/CarIllustration'
import { Card, CardFooter, CardSection, CopyButton, DataList, Eyebrow, Inline, Money, Stack, Text } from '../ui'
import { BookingPayment, BookingStatusPill } from './BookingTable'

/** Header card for one booking at the counter: reference, customer, trip, car, payment. */
export function BookingSummary({ booking, children }: { booking: CounterBooking; children?: ReactNode }) {
  const info = CATEGORY_INFO[booking.category]
  const rental = booking.rental

  return (
    <Card padding="none">
      <CardSection>
        <Inline gap={4} justify="between" align="start">
          <Stack gap={1}>
            <Eyebrow>Booking reference</Eyebrow>
            <p className="font-mono text-headline-sm text-ink" data-testid="staff-reference">{booking.reference}</p>
            <Inline><BookingStatusPill booking={booking} /></Inline>
          </Stack>
          <CopyButton text={booking.reference} />
        </Inline>
      </CardSection>

      <CardSection divider>
        <DataList columns={4} items={[
          { label: 'Customer', value: <Text as="span" size="lg" tone="strong" weight="medium">{booking.customerName}</Text>, hint: booking.customerEmail },
          { label: `Pickup · ${booking.pickupBranchName}`, value: formatDay(booking.pickupAt), hint: `${formatTime(booking.pickupAt)} SAST`, hintMono: true },
          { label: `Return · ${booking.returnBranchName}`, value: formatDay(booking.returnAt), hint: `${formatTime(booking.returnAt)} SAST`, hintMono: true },
          {
            label: CATEGORY_LABEL[booking.category],
            value: <Stack direction="row" gap={3} align="center"><CarThumb category={booking.category} /><Text as="span" tone="strong">{info.example}</Text></Stack>,
          },
        ]} />
      </CardSection>

      <CardFooter tone="muted">
        <BookingPayment booking={booking} />
        <Text as="span" tone="muted">Booking total <Money amount={booking.total} weight="medium" /></Text>
      </CardFooter>

      {rental && (
        <CardSection divider>
          <DataList columns={4} size="md" items={[
            { label: 'Vehicle', value: rental.vehicleRegistration, mono: true },
            { label: 'Out at', value: <span className="tabular">{rental.startOdometerKm.toLocaleString('en-ZA')} km</span> },
            { label: 'Picked up', value: `${formatDay(rental.pickedUpAt)} ${formatTime(rental.pickedUpAt)}` },
            { label: 'Due back', value: <Text as="span" tone={rental.isLate ? 'danger' : 'strong'} weight={rental.isLate ? 'medium' : 'regular'}>{formatDay(rental.dueBackAt)} {formatTime(rental.dueBackAt)}</Text> },
          ]} />
        </CardSection>
      )}

      {children && <CardSection divider>{children}</CardSection>}
    </Card>
  )
}
