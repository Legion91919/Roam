import type { ReservationDetails, ReservationStatus } from '../../api/types'
import { CATEGORY_LABEL } from '../../lib/catalog'
import { bookingReference, formatDay, formatTime } from '../../lib/format'
import { Icon, Inline, LinkCard, Money, Pill, Stack, Text } from '../ui'
import { CarThumb } from './CarIllustration'
import { PayAtBranchLine, PaymentLine } from './PaymentCard'

const STATUS_TEXT: Record<ReservationStatus, string> = { Confirmed: 'Confirmed', CheckedOut: 'Checked out', Cancelled: 'Cancelled', NoShow: 'No-show' }

/** One booking in "My bookings": car, status, where and when, reference, payment and total. */
export function BookingListItem({ reservation: r }: { reservation: ReservationDetails }) {
  const ended = r.status === 'Cancelled' || r.status === 'NoShow'
  return (
    <LinkCard href={`#/reservations/${r.id}`}>
      <Stack direction="row" gap={4} align="center">
        <CarThumb category={r.category} size="md" muted={ended} hideOnPhone />
        <Stack gap={0} grow>
          <Inline gap={2}>
            <Text as="span" size="lg" tone="strong" weight="medium">{CATEGORY_LABEL[r.category]}</Text>
            <Pill tone={ended ? 'danger' : 'success'} dot>{STATUS_TEXT[r.status]}</Pill>
            {r.hasInspectionReport && <Pill tone="neutral"><Icon name="fact_check" size={14} /> Report available</Pill>}
          </Inline>
          <Text tone="muted" truncate>{r.pickupBranchName} · {formatDay(r.pickupAt)} {formatTime(r.pickupAt)}</Text>
          <Text mono tone="subtle">{bookingReference(r.id)}</Text>
          {r.payment
            ? <PaymentLine payment={r.payment} size="sm" />
            : r.paymentOption === 'PayAtBranch' && r.status === 'Confirmed' && <PayAtBranchLine size="sm" />}
        </Stack>
        <Money amount={r.total} size="lg" tone={r.status === 'Cancelled' ? 'subtle' : 'strong'} strike={r.status === 'Cancelled'} />
        <Icon name="chevron_right" className="text-secondary" />
      </Stack>
    </LinkCard>
  )
}
