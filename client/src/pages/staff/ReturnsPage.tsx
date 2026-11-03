import { useCallback, useState, type ReactNode } from 'react'
import { describeError } from '../../api/client'
import { useBranchReturns, useCounterBooking, useReturnVehicle } from '../../api/hooks'
import type { Inspection, ReturnResult } from '../../api/types'
import { InspectionStepper } from '../../components/inspection/InspectionStepper'
import { PaymentLine } from '../../components/booking/PaymentCard'
import { BookingSummary } from '../../components/staff/BookingSummary'
import { BookingTable } from '../../components/staff/BookingTable'
import { StaffLayout } from '../../components/staff/StaffLayout'
import { BackLink, Banner, Button, Callout, Card, CardHeader, DataList, Heading, Inline, Pill, QueryState, Stack, Text } from '../../components/ui'
import { formatMoney } from '../../lib/format'
import { navigate } from '../../lib/router'
import { useStaffBranch, useStaffDate } from '../../lib/staffPrefs'
import { DatePicker } from './TodayPage'

/** Returns: cars due back at the branch → return inspection (new damage compared with checkout) → close the rental and capture. */
export function ReturnsPage({ id }: { id?: string }) {
  return id ? <ReturnForm reservationId={id} /> : <ReturnsDue />
}

function ReturnsDue() {
  const [branchId] = useStaffBranch()
  const [date, setDate] = useStaffDate()
  const returns = useBranchReturns(branchId, date)

  return (
    <StaffLayout page="returns" title="Returns" subtitle="Cars still out and due back here by the end of the day. Late ones are flagged." actions={<DatePicker value={date} onChange={setDate} />}>
      <BookingTable
        title="Due back"
        icon="assignment_return"
        when="return"
        waitingFor={branchId ? undefined : "Choose your branch at the top to see its bookings."}
        rows={returns.data}
        loading={returns.isLoading}
        error={returns.isError ? describeError(returns.error) : null}
        onRetry={() => returns.refetch()}
        empty="No cars due back at this branch by the end of this day."
        action={(b) => <Button variant={b.rental?.isLate ? 'primary' : 'secondary'} size="sm" icon="assignment_return" onClick={() => navigate(`/agent/returns/${b.reservationId}`)}>Take return</Button>}
      />
    </StaffLayout>
  )
}

function ReturnForm({ reservationId }: { reservationId: string }) {
  const booking = useCounterBooking(reservationId)
  const returnVehicle = useReturnVehicle()
  const [inspected, setInspected] = useState<Inspection | null>(null)
  const [result, setResult] = useState<ReturnResult | null>(null)
  const onCompleted = useCallback((report: Inspection) => setInspected(report), [])
  const rental = booking.data?.rental

  return (
    <StaffLayout page="returns" title="Take a return" subtitle="Inspect the car with the customer. The end odometer, late fee and card capture follow from the inspection and the server.">
      <BackLink href="#/agent/returns">Returns due</BackLink>
      <QueryState query={booking} loading="panel">
        {booking.data === null && <Banner error={{ code: 'resource.not_found', title: 'Not found', message: 'No booking with that id.', retryable: false }} />}
        {booking.data && (
          <Stack gap={6}>
            <BookingSummary booking={booking.data} />
            {result ? (
              <ReturnSettlement result={result} />
            ) : !rental || rental.status !== 'Open' ? (
              <Banner error={{ code: 'rental.already_returned', title: 'Nothing to return', message: rental ? 'This car has already been returned.' : 'This booking has not been checked out.', retryable: false }} />
            ) : (
              <>
                {rental.isLate && <Callout tone="danger" icon="schedule">Overdue: a late fee will be added (1.5× the daily rate per started 24 hours).</Callout>}
                <InspectionStepper reservationId={reservationId} kind="Return" vehicleId={null} minimumOdometer={rental.startOdometerKm} onCompleted={onCompleted} />
                {inspected && (
                  <Card>
                    <Stack direction="responsive" gap={4} justify="between" align="center">
                      <Stack gap={1} grow>
                        <CardHeader size="sm" title="Close the rental" subtitle={<>
                          Returned at {inspected.odometerKm?.toLocaleString('en-ZA')} km ({((inspected.odometerKm ?? 0) - rental.startOdometerKm).toLocaleString('en-ZA')} km driven).
                          {inspected.newDamage.length > 0 && <Text as="span" tone="danger"> {inspected.newDamage.length} new damage item(s) recorded.</Text>}
                        </>} />
                        {returnVehicle.isError && <Banner error={describeError(returnVehicle.error)} onDismiss={() => returnVehicle.reset()} />}
                      </Stack>
                      <Button icon="assignment_return" size="lg" loading={returnVehicle.isPending} loadingText="Returning…" onClick={() => returnVehicle.mutateAsync(rental.rentalId).then(setResult, () => {})}>
                        Return car and capture payment
                      </Button>
                    </Stack>
                  </Card>
                )}
              </>
            )}
          </Stack>
        )}
      </QueryState>
    </StaffLayout>
  )
}

function ReturnSettlement({ result }: { result: ReturnResult }) {
  const figure = (value: ReactNode, tone: 'strong' | 'danger' = 'strong') => <Heading level={3} size="sm" tone={tone} tabular>{value}</Heading>
  return (
    <Card padding="lg" gap={5} data-testid="return-settlement">
      <Heading level={2} size="sm" tone="brand" icon="check_circle">Car returned</Heading>
      <DataList columns={4} items={[
        { label: 'Distance', value: figure(`${result.distanceKm.toLocaleString('en-ZA')} km`) },
        { label: 'Late fee', value: figure(formatMoney(result.lateFee), result.lateFee > 0 ? 'danger' : 'strong') },
        { label: 'Amount due', value: figure(formatMoney(result.amountDue)) },
        { label: 'Card capture', value: result.payment ? <PaymentLine payment={result.payment} /> : <Pill tone="neutral">No card on file</Pill> },
      ]} />
      {result.payment?.status === 'Failed' && (
        <Callout tone="danger">The capture was declined ({result.payment.failureReason}). The return is recorded; take payment at the counter.</Callout>
      )}
      <Inline><Button icon="today" onClick={() => navigate('/agent')}>Back to today</Button></Inline>
    </Card>
  )
}
