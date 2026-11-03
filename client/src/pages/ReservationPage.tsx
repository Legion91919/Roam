import { useEffect, useState } from 'react'
import { describeError } from '../api/client'
import { useCancelReservation, useReservation } from '../api/hooks'
import { InspectionReports } from '../components/booking/InspectionReports'
import { Page } from '../components/booking/Layout'
import {
  BookingReference, cancellationText, ReservationHero, ReservationMeta, ReservationPayment, ReservationVehicle, TripLegs,
} from '../components/booking/ReservationParts'
import { Banner, Button, Callout, Card, CardFooter, CardSection, Dialog, QueryState, Skeleton, Stack, Text } from '../components/ui'
import { useAuth } from '../lib/auth'
import { useDraft } from '../lib/draft'
import { formatMoney } from '../lib/format'
import { navigate, withNext } from '../lib/router'

/**
 * Screens 4 and 5: one page, driven by the reservation's status from the QUERY side
 * (GET /api/reservations/{id}). Cancelling invalidates the query, so the page re-renders as
 * "Cancelled" straight from the server's answer: no local status juggling.
 */
export function ReservationPage({ id }: { id: string }) {
  const { session } = useAuth()
  const reservation = useReservation(session ? id : undefined)
  const cancel = useCancelReservation()
  const [confirming, setConfirming] = useState(false)
  const { reset } = useDraft()

  useEffect(() => {
    if (!session) navigate(withNext('/signin', `/reservations/${id}`))
  }, [session, id])

  if (!session) return null

  const loading = (
    <Card padding="lg" gap={4}>
      <Stack gap={3} align="center"><Skeleton size="avatar" /><Skeleton size="title" /></Stack>
      <Skeleton size="block" /><Skeleton size="card" />
    </Card>
  )
  const r = reservation.data
  const ended = r?.status === 'Cancelled' || r?.status === 'NoShow'

  return (
    <Page width="narrow">
      <QueryState query={reservation} loading={loading}>
        {r && (
          <>
            <Card padding="none" screenOnly>
              <ReservationHero reservation={r} />
              <CardSection padding="lg" attached><BookingReference id={r.id} /></CardSection>
              <CardSection padding="lg" divider><ReservationVehicle reservation={r} /></CardSection>
              <CardSection padding="lg" divider tone={ended ? 'default' : 'muted'} gap={4}>
                <TripLegs reservation={r} muted={ended} />
                <Stack gap={3}>
                  <ReservationMeta reservation={r} />
                  <ReservationPayment reservation={r} />
                </Stack>
              </CardSection>
              {ended && (
                <CardSection padding="lg" attached>
                  <Callout data-testid="cancellation-note">{cancellationText(r)}</Callout>
                </CardSection>
              )}
              {cancel.isError && (
                <CardSection padding="lg" attached><Banner error={describeError(cancel.error)} onDismiss={() => cancel.reset()} /></CardSection>
              )}
              <CardFooter padding="lg">
                <Button variant={ended ? 'primary' : 'tonal'} icon={ended ? 'search' : 'add_circle'} onClick={() => { reset(); navigate('/') }}>Book another vehicle</Button>
                {r.status === 'Confirmed' && <Button variant="danger" icon="close" onClick={() => setConfirming(true)}>Cancel reservation</Button>}
              </CardFooter>
            </Card>

            {r.hasInspectionReport && <InspectionReports reservationId={r.id} />}

            <Dialog open={confirming} onClose={() => setConfirming(false)} title="Cancel this reservation?"
              footer={<>
                <Button variant="secondary" onClick={() => setConfirming(false)}>Keep my booking</Button>
                <Button variant="danger" icon="close" loading={cancel.isPending} loadingText="Cancelling…"
                  onClick={() => cancel.mutate(r.id, { onSettled: () => setConfirming(false) })}>Cancel reservation</Button>
              </>}>
              <Text>
                Free of charge before pickup: {r.payment ? `the ${formatMoney(r.total)} hold on your card is released and ` : ''}the car goes back to {r.pickupBranchName}. This can't be undone.
              </Text>
            </Dialog>
          </>
        )}
      </QueryState>
      {reservation.isError && <Button variant="ghost" icon="arrow_back" onClick={() => navigate('/bookings')}>My bookings</Button>}
    </Page>
  )
}
