import { useEffect } from 'react'
import { useMyReservations } from '../api/hooks'
import { BookingListItem } from '../components/booking/BookingListItem'
import { Page, PageHeader } from '../components/booking/Layout'
import { Button, Card, EmptyState, QueryState, SkeletonStack, Stack } from '../components/ui'
import { useAuth } from '../lib/auth'
import { navigate, withNext } from '../lib/router'

/** The signed-in customer's own bookings (GET /api/reservations/mine: filtered by owner on the server). */
export function MyBookingsPage() {
  const { session } = useAuth()
  const bookings = useMyReservations(Boolean(session))

  useEffect(() => {
    if (!session) navigate(withNext('/signin', '/bookings'))
  }, [session])
  if (!session) return null

  return (
    <Page width="narrow">
      <PageHeader title="My bookings" subtitle={`Signed in as ${session.email}`} />
      <QueryState query={bookings} loading={<SkeletonStack count={3} size="block" />}>
        {bookings.data?.length === 0 ? (
          <Card padding="none">
            <EmptyState icon="event_note" title="No bookings yet" message="Search a branch and dates to book your first car."
              action={<Button icon="search" onClick={() => navigate('/')}>Find a car</Button>} />
          </Card>
        ) : (
          <Stack gap={3}>
            {bookings.data?.map((r) => <BookingListItem key={r.id} reservation={r} />)}
          </Stack>
        )}
      </QueryState>
    </Page>
  )
}
