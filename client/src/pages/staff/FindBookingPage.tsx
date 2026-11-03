import { useState, type FormEvent } from 'react'
import { useCounterBooking } from '../../api/hooks'
import { BookingActions } from '../../components/staff/BookingActions'
import { BookingSummary } from '../../components/staff/BookingSummary'
import { StaffLayout } from '../../components/staff/StaffLayout'
import { Button, Card, EmptyState, Inline, Input, QueryState, Stack, Text } from '../../components/ui'
import { navigate } from '../../lib/router'

const REFERENCE = /^(ROAM)?[-\s]*([0-9A-F]{4}[-\s]*){4}$/i
const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i

/** Find ANY customer's booking by its reference (agents only), with the counter actions. */
export function FindBookingPage({ reference }: { reference: string }) {
  const [input, setInput] = useState(reference)
  const booking = useCounterBooking(reference)
  const valid = REFERENCE.test(input.trim()) || GUID.test(input.trim())
  const b = booking.data

  const submit = (e: FormEvent) => {
    e.preventDefault()
    navigate(`/agent/find?ref=${encodeURIComponent(input.trim().toUpperCase())}`)
  }

  return (
    <StaffLayout page="find" title="Find booking" subtitle="Search by the reference the customer shows you, e.g. ROAM-8F4B-92A1-0C7E-449D.">
      <Card>
        <Stack as="form" direction="responsive" gap={3} onSubmit={submit}>
          <Stack grow>
            <Input mono uppercase leadingIcon="search" aria-label="Booking reference" placeholder="ROAM-XXXX-XXXX-XXXX-XXXX" autoFocus value={input} onChange={(e) => setInput(e.target.value)} />
          </Stack>
          <Button type="submit" icon="search" disabled={!valid}>Find</Button>
        </Stack>
      </Card>

      {!reference && <Text tone="muted" align="center">Enter a reference to look up a booking.</Text>}
      <QueryState query={booking} loading="panel">
        {b === null && (
          <Card padding="none">
            <EmptyState icon="search_off" title={`No booking with reference ${reference}`} message="Check the characters with the customer (0 and O, 8 and B)." />
          </Card>
        )}
        {b && (
          <BookingSummary booking={b}>
            <Inline gap={3}>
              {b.status === 'Confirmed' && <Button icon="key" onClick={() => navigate(`/agent/checkout/${b.reservationId}`)}>Check out</Button>}
              <BookingActions booking={b} />
              {b.vehicleId && <Button variant="ghost" icon="directions_car" onClick={() => navigate(`/agent/fleet/${b.vehicleId}`)}>Car history</Button>}
              {b.rental?.status === 'Open' && <Button icon="assignment_return" onClick={() => navigate(`/agent/returns/${b.reservationId}`)}>Take return</Button>}
              {(b.status === 'Cancelled' || b.status === 'NoShow') && (
                <Text tone="muted">
                  {b.status === 'NoShow' ? 'No-show recorded' : b.cancellation?.cancelledBy === 'Agent' ? 'Cancelled by staff' : 'Cancelled by the customer'}
                  {b.cancellation?.reason && b.status !== 'NoShow' ? `: “${b.cancellation.reason}”` : ''}. No counter actions.
                </Text>
              )}
              {b.rental?.status === 'Closed' && <Text tone="muted">Returned: nothing left to do.</Text>}
            </Inline>
          </BookingSummary>
        )}
      </QueryState>
    </StaffLayout>
  )
}
