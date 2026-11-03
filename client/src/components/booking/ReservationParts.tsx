import type { ReservationDetails } from '../../api/types'
import { CATEGORY_INFO, CATEGORY_LABEL } from '../../lib/catalog'
import { bookingReference, chargeableDays, formatDay, formatTime } from '../../lib/format'
import { CopyButton, Eyebrow, Grid, Heading, Icon, IconBadge, Inline, Money, Pill, Stack, Text } from '../ui'
import { CarThumb } from './CarIllustration'
import { PayAtBranchLine, PaymentLine } from './PaymentCard'

/** Booking reference callout (DESIGN.md): mono text, dashed divider, one-click copy. */
export function BookingReference({ id }: { id: string }) {
  const reference = bookingReference(id)
  return (
    <div className="rounded-card bg-surface-container-low p-4 sm:p-5">
      <Stack direction="responsive" gap={3} justify="between" align="center">
        <Stack gap={1} grow>
          <Eyebrow>Booking reference</Eyebrow>
          <p className="font-mono text-[15px] font-medium tracking-wide text-ink" data-testid="booking-reference">{reference}</p>
          <p className="truncate border-t border-dashed border-line-strong pt-1 font-mono text-body-sm text-muted" title={id}>{id}</p>
        </Stack>
        <CopyButton text={id} />
      </Stack>
    </div>
  )
}

/** Pickup and return legs side by side. */
export function TripLegs({ reservation, muted = false }: { reservation: ReservationDetails; muted?: boolean }) {
  const legs = [
    { label: 'Pickup', icon: 'flight_land', branch: reservation.pickupBranchName, at: reservation.pickupAt },
    { label: 'Return', icon: 'flight_takeoff', branch: reservation.returnBranchName, at: reservation.returnAt },
  ]
  return (
    <Grid columns={{ base: 1, sm: 2 }}>
      {legs.map((leg) => (
        <div key={leg.label} className={`flex gap-3 rounded-card p-4 ${muted ? 'bg-surface-container-low' : ''}`}>
          <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg bg-surface-container text-on-surface-variant"><Icon name={leg.icon} size={20} /></span>
          <Stack gap={0} grow>
            <Eyebrow>{leg.label} · {leg.branch}</Eyebrow>
            <Heading level={3} size="sm" tone={muted ? 'default' : 'strong'} className="mt-1">{formatDay(leg.at)}</Heading>
            <Text mono tone="muted">{formatTime(leg.at)} SAST</Text>
          </Stack>
        </div>
      ))}
    </Grid>
  )
}

const HERO = {
  NoShow: { icon: 'person_off', pill: 'No-show', title: "The car wasn't collected" },
  Cancelled: { icon: 'event_busy', pill: 'Cancelled', title: 'Reservation cancelled' },
  CheckedOut: { icon: 'key', pill: 'Checked out', title: 'Enjoy the drive' },
  Confirmed: { icon: 'check_circle', pill: 'Confirmed', title: "You're booked" },
} as const

/** Status hero at the top of a booking: icon, status pill, title and one line about what it means. */
export function ReservationHero({ reservation }: { reservation: ReservationDetails }) {
  const ended = reservation.status === 'Cancelled' || reservation.status === 'NoShow'
  const hero = HERO[reservation.status]
  const pill = reservation.status === 'Cancelled' && reservation.cancellation?.cancelledBy === 'Agent' ? 'Cancelled by Roam' : hero.pill
  const message =
    reservation.status === 'NoShow' ? `${reservation.pickupBranchName} held the car past the pickup time, then released it.`
    : reservation.status === 'Cancelled' ? `The booking was cancelled and its inventory released back to ${reservation.pickupBranchName}.`
    : reservation.status === 'CheckedOut' ? 'The keys have been handed over.'
    : 'Your car is reserved. Keep your booking reference handy at the counter.'

  return (
    <div className={`px-6 pb-6 pt-10 text-center sm:px-10 ${ended ? '' : 'bg-gradient-to-b from-primary-fixed/30 to-transparent'}`}>
      <Stack gap={3} align="center">
        <IconBadge icon={hero.icon} size="lg" tone={ended ? 'danger' : 'brand'} />
        <Pill tone={ended ? 'danger' : 'success'} dot className="mt-2 uppercase tracking-wider">{pill}</Pill>
        <Heading level={1} size="lg">{hero.title}</Heading>
        <Text size="lg" tone="muted" align="center" className="max-w-md">{message}</Text>
      </Stack>
    </div>
  )
}

/** The booked category with its price (struck through once cancelled). */
export function ReservationVehicle({ reservation }: { reservation: ReservationDetails }) {
  const info = CATEGORY_INFO[reservation.category]
  const days = chargeableDays(reservation.pickupAt, reservation.returnAt)
  const ended = reservation.status === 'Cancelled' || reservation.status === 'NoShow'
  return (
    <Stack direction="responsive" gap={4} align="center">
      <CarThumb category={reservation.category} size="lg" muted={ended} />
      <Stack gap={0} grow>
        <Eyebrow tone="brand">{CATEGORY_LABEL[reservation.category]} fleet · {info.transmission}</Eyebrow>
        <Heading level={2} size="sm">{info.example}</Heading>
        <Text tone="muted">{info.seats} seats · {info.bags} bags · {days} day{days === 1 ? '' : 's'}</Text>
      </Stack>
      <Stack gap={0} align={{ sm: 'end' }}>
        <Text size="sm" tone="muted">{ended ? 'Booking total' : 'Total (incl. VAT)'}</Text>
        <Money amount={reservation.total} size="xl" tone={ended ? 'subtle' : 'brand'} strike={ended} />
        {reservation.status === 'Cancelled' && <Text size="sm" tone="brand">{reservation.payment ? 'Not charged: card hold released' : 'Not charged'}</Text>}
        {reservation.status === 'NoShow' && reservation.payment && <Text size="sm" tone="danger">No-show fee <Money amount={reservation.payment.captured} size="sm" tone="danger" /></Text>}
      </Stack>
    </Stack>
  )
}

const PAYMENT_NOTE: Record<string, string> = {
  Authorized: 'Held on your card. Taken when you return the car (plus any late fee).',
  Captured: 'Charged after the car was returned.',
  Voided: 'The hold on your card was released.',
  Failed: 'The payment did not go through. Roam will contact you.',
}

/** How the booking is paid: the card line with what its status means, or "Pay at the branch". */
export function ReservationPayment({ reservation }: { reservation: ReservationDetails }) {
  if (reservation.payment) {
    return (
      <Stack gap={1} data-testid="payment-line">
        <PaymentLine payment={reservation.payment} />
        <Text size="sm" tone="muted">{PAYMENT_NOTE[reservation.payment.status]}</Text>
      </Stack>
    )
  }
  if (reservation.paymentOption === 'PayAtBranch' && reservation.status === 'Confirmed') {
    return (
      <Stack gap={1} data-testid="payment-line">
        <PayAtBranchLine />
        <Text size="sm" tone="muted">
          Bring a card in the main driver's name. At pickup <Money amount={reservation.total} size="sm" tone="muted" /> is held on it before you get the keys, and taken when you return the car.
        </Text>
      </Stack>
    )
  }
  return null
}

/** The driver and the accepted terms version. */
export function ReservationMeta({ reservation }: { reservation: ReservationDetails }) {
  return (
    <Inline gap={2}>
      <Pill tone="neutral"><Icon name="person" size={14} /> {reservation.driverName}</Pill>
      <Pill tone="neutral"><Icon name="gavel" size={14} /> Terms v{reservation.termsVersion} accepted</Pill>
    </Inline>
  )
}

/** Who ended the booking, when and why, in the customer's words. */
export function cancellationText(r: ReservationDetails): string {
  const c = r.cancellation
  const when = c ? ` on ${formatDay(c.at)} at ${formatTime(c.at)}` : ''
  if (r.status === 'NoShow') return `The car was not collected, so the booking was closed as a no-show${when}.${r.payment ? " One day's rate was charged as the no-show fee and the rest of the hold was released." : ''}`
  if (c?.cancelledBy === 'Agent') return `Roam cancelled this reservation${when}${c.reason ? `: “${c.reason}”` : '.'}${r.payment ? ' The hold on your card was released.' : ''}`
  return `You cancelled this reservation${when}. The car has been released back to ${r.pickupBranchName} for other customers.`
}
