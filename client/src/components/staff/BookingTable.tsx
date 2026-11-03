import type { ReactNode } from 'react'
import type { DescribedError } from '../../api/client'
import type { CounterBooking } from '../../api/types'
import { CATEGORY_LABEL } from '../../lib/catalog'
import { formatShortDate, formatTime } from '../../lib/format'
import { PayAtBranchLine, PaymentLine } from '../booking/PaymentCard'
import { Card, CardHeader, DataTable, EmptyState, Pill, Stack, Text, TextLink, type Column } from '../ui'

/** Reservation status (and lateness) as a pill. */
export function BookingStatusPill({ booking }: { booking: CounterBooking }) {
  if (booking.rental?.status === 'Open' && booking.rental.isLate) return <Pill tone="danger" dot>Late</Pill>
  if (booking.rental?.status === 'Open') return <Pill tone="warning" dot>On rent</Pill>
  if (booking.rental?.status === 'Closed') return <Pill tone="neutral" dot>Returned</Pill>
  if (booking.status === 'NoShow') return <Pill tone="danger" dot>No-show</Pill>
  if (booking.status === 'Cancelled') return <Pill tone="danger" dot>{booking.cancellation?.cancelledBy === 'Agent' ? 'Cancelled by staff' : 'Cancelled'}</Pill>
  if (booking.status === 'CheckedOut') return <Pill tone="warning" dot>Checked out</Pill>
  return <Pill tone="success" dot>Confirmed</Pill>
}

/** The card, pay-at-branch or "no card" state of a booking, as the counter sees it. */
export function BookingPayment({ booking, size = 'md' }: { booking: CounterBooking; size?: 'md' | 'sm' }) {
  if (booking.payment) return <PaymentLine payment={booking.payment} size={size} />
  if (booking.paymentOption === 'PayAtBranch') return <PayAtBranchLine size={size} />
  return <Text size={size} tone="subtle" icon="credit_card_off">No card on file</Text>
}

/**
 * A staff table of bookings with the four data states (from DataTable). `when` picks the time
 * column (pickup or due back); `action` renders the row's buttons.
 */
export function BookingTable({ title, icon, rows, loading, error, onRetry, empty, when, action, waitingFor }: {
  title: string
  icon: string
  rows: CounterBooking[] | undefined
  loading: boolean
  error: DescribedError | null
  onRetry?: () => void
  empty: string
  when: 'pickup' | 'return'
  action?: (booking: CounterBooking) => ReactNode
  /** Shown instead of the data states while a required input (e.g. the branch) is missing. */
  waitingFor?: string
}) {
  const columns: Column<CounterBooking>[] = [
    {
      key: 'when', header: when === 'pickup' ? 'Pickup' : 'Due back', nowrap: true,
      cell: (b) => {
        const at = when === 'pickup' ? b.pickupAt : b.rental?.dueBackAt ?? b.returnAt
        return <><Text as="span" mono tone="strong">{formatTime(at)}</Text> <Text as="span" size="sm" tone="muted" className="ml-2">{formatShortDate(at)}</Text></>
      },
    },
    {
      key: 'booking', header: 'Booking',
      cell: (b) => (
        <Stack gap={0}>
          <span className="whitespace-nowrap font-mono text-mono-code"><TextLink href={`#/agent/find?ref=${b.reference}`}>{b.reference}</TextLink></span>
          <Text size="sm" tone="muted">{CATEGORY_LABEL[b.category]}{b.rental ? ` · ${b.rental.vehicleRegistration}` : ''}</Text>
        </Stack>
      ),
    },
    { key: 'customer', header: 'Customer', cell: (b) => <Stack gap={0}><Text tone="strong">{b.customerName}</Text><Text size="sm" tone="muted">{b.customerEmail}</Text></Stack> },
    { key: 'status', header: 'Status', cell: (b) => <BookingStatusPill booking={b} /> },
    { key: 'payment', header: 'Payment', cell: (b) => <BookingPayment booking={b} size="sm" /> },
    ...(action ? [{ key: 'action', header: 'Action', srOnlyHeader: true, align: 'right' as const, nowrap: true, cell: action }] : []),
  ]

  return (
    <Card padding="none">
      <CardHeader divider size="sm" icon={icon} title={title}
        actions={rows && <Text as="span" size="label" tone="muted">{rows.length} {rows.length === 1 ? 'booking' : 'bookings'}</Text>} />
      {waitingFor
        ? <EmptyState icon="storefront" message={waitingFor} />
        : <DataTable columns={columns} rows={rows} rowKey={(b) => b.reservationId} loading={loading} error={error} onRetry={onRetry}
            minWidth={880} empty={<EmptyState icon="event_available" message={empty} />} />}
    </Card>
  )
}
