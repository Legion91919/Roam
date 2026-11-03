import { useCallback, useState } from 'react'
import { ApiError, describeError } from '../../api/client'
import { useAvailableVehicles, useBranchPickups, useCheckOut, useCounterBooking, useTakeCounterPayment, useTokenizeCard } from '../../api/hooks'
import type { AvailableVehicle, CardDetails, CounterBooking, Inspection, VehicleCategory } from '../../api/types'
import { CarThumb } from '../../components/booking/CarIllustration'
import { CardFields, PaymentLine } from '../../components/booking/PaymentCard'
import { InspectionStepper } from '../../components/inspection/InspectionStepper'
import { BookingSummary } from '../../components/staff/BookingSummary'
import { BookingTable } from '../../components/staff/BookingTable'
import { StaffLayout } from '../../components/staff/StaffLayout'
import {
  BackLink, Banner, Button, Callout, Card, CardHeader, FormActions, Heading, Money, Pill, QueryState, RadioCards, Stack, Text, ToggleField,
} from '../../components/ui'
import { validateCard } from '../../lib/card'
import { CATEGORY_LABEL } from '../../lib/catalog'
import { formatMoney } from '../../lib/format'
import { navigate } from '../../lib/router'
import { useStaffBranch, useStaffDate } from '../../lib/staffPrefs'
import { DatePicker } from './TodayPage'

/**
 * Check out: pick a confirmed pickup → choose a fleet car → inspect it (checklist, fuel, odometer, 4 photos)
 * → take the card payment (pay-at-branch bookings only) → hand over the keys.
 */
export function CheckoutPage({ id }: { id?: string }) {
  return id ? <CheckoutForm reservationId={id} /> : <PickupsToCheckOut />
}

function PickupsToCheckOut() {
  const [branchId] = useStaffBranch()
  const [date, setDate] = useStaffDate()
  const pickups = useBranchPickups(branchId, date)

  return (
    <StaffLayout page="checkout" title="Check out" subtitle="Confirmed bookings waiting for their keys." actions={<DatePicker value={date} onChange={setDate} />}>
      <BookingTable
        title="Waiting for keys"
        icon="key"
        when="pickup"
        waitingFor={branchId ? undefined : 'Choose your branch at the top to see its bookings.'}
        rows={pickups.data?.filter((b) => b.status === 'Confirmed')}
        loading={pickups.isLoading}
        error={pickups.isError ? describeError(pickups.error) : null}
        onRetry={() => pickups.refetch()}
        empty="Nobody is waiting for keys at this branch on this day."
        action={(b) => <Button size="sm" icon="key" onClick={() => navigate(`/agent/checkout/${b.reservationId}`)}>Check out</Button>}
      />
    </StaffLayout>
  )
}

function CheckoutForm({ reservationId }: { reservationId: string }) {
  const booking = useCounterBooking(reservationId)
  const [vehicle, setVehicle] = useState<AvailableVehicle | null>(null)
  const [inspected, setInspected] = useState<Inspection | null>(null)
  const [rentalId, setRentalId] = useState<string | null>(null)
  const onCompleted = useCallback((report: Inspection) => setInspected(report), [])
  // Pay at the branch: the card is taken here (step 3) before the keys. Stays true once paid, so the
  // paid step keeps showing above the hand-over.
  const needsPayment = booking.data?.paymentOption === 'PayAtBranch'
  const b = booking.data

  return (
    <StaffLayout page="checkout" title="Hand over the keys" subtitle="Choose the car, inspect it with the customer, then hand over the keys.">
      <BackLink href="#/agent/checkout">Waiting for keys</BackLink>
      <QueryState query={booking} loading="panel">
        {b === null && <Banner error={{ code: 'resource.not_found', title: 'Not found', message: 'No booking with that id.', retryable: false }} />}
        {b && (
          <Stack gap={6}>
            <BookingSummary booking={b} />
            {rentalId ? (
              <KeysHandedOver vehicle={vehicle} />
            ) : b.status !== 'Confirmed' ? (
              <Banner error={{ code: 'reservation.not_checkoutable', title: 'Cannot check out', message: `This booking is ${b.status === 'CheckedOut' ? 'already checked out' : b.status === 'NoShow' ? 'a no-show' : b.status.toLowerCase()}.`, retryable: false }} />
            ) : (
              <>
                <ChooseVehicle reservationId={reservationId} category={b.category} selected={vehicle} locked={inspected !== null}
                  payFirst={needsPayment && !b.payment} onSelect={(v) => { setVehicle(v); setInspected(null) }} />
                {vehicle && <InspectionStepper reservationId={reservationId} kind="Checkout" vehicleId={vehicle.id} minimumOdometer={vehicle.odometerKm} onCompleted={onCompleted} />}
                {vehicle && inspected && needsPayment && <TakePayment booking={b} />}
                {vehicle && inspected && (!needsPayment || b.payment) && (
                  <HandOver reservationId={reservationId} vehicle={vehicle} freeUpgrade={vehicle.isUpgrade} step={needsPayment ? 4 : 3} onDone={setRentalId} />
                )}
              </>
            )}
          </Stack>
        )}
      </QueryState>
    </StaffLayout>
  )
}

function KeysHandedOver({ vehicle }: { vehicle: AvailableVehicle | null }) {
  return (
    <Card gap={3} data-testid="keys-handed-over">
      <Heading level={2} size="sm" tone="brand" icon="check_circle">Keys handed over</Heading>
      <Text tone="muted"><Text as="span" mono tone="strong">{vehicle?.registration}</Text> is on rent. The customer can now see the checkout report on their booking; the card hold stays until the return.</Text>
      <FormActions align="between">
        <Button icon="today" onClick={() => navigate('/agent')}>Back to today</Button>
        {vehicle && <Button variant="secondary" icon="directions_car" onClick={() => navigate(`/agent/fleet/${vehicle.id}`)}>Car history</Button>}
      </FormActions>
    </Card>
  )
}

/** Step 1: a real fleet car that is at the branch, Available, and in the booked category (or a free upgrade). */
function ChooseVehicle({ reservationId, category, selected, locked, payFirst, onSelect }: {
  reservationId: string
  category: VehicleCategory
  selected: AvailableVehicle | null
  locked: boolean
  payFirst: boolean
  onSelect: (vehicle: AvailableVehicle) => void
}) {
  const [upgrades, setUpgrades] = useState(false)
  const vehicles = useAvailableVehicles(reservationId, upgrades)

  return (
    <Card gap={4}>
      <CardHeader size="sm" title="1. Choose the car" actions={<ToggleField checked={upgrades} onChange={setUpgrades} label="Show free upgrades" />} />
      <QueryState query={vehicles} loading="block">
        {vehicles.data?.length === 0 && (
          <Callout>No {CATEGORY_LABEL[category]} car is available at this branch right now.{!upgrades && ' Try showing free upgrades.'}</Callout>
        )}
        <RadioCards label="Car to hand over" columns={3} value={selected?.id ?? null}
          onChange={(id) => { const v = vehicles.data?.find((x) => x.id === id); if (v) onSelect(v) }}
          options={(vehicles.data ?? []).map((v) => ({
            value: v.id,
            disabled: locked && selected?.id !== v.id,
            content: (
              <Stack direction="row" gap={3} align="center" grow>
                <CarThumb category={v.category} />
                <Stack gap={0} grow>
                  <Text mono tone="strong">{v.registration}</Text>
                  <Text tone="strong" truncate>{v.make} {v.model} · {v.year}</Text>
                  <Text size="sm" tone="muted">{v.transmission} · {v.odometerKm.toLocaleString('en-ZA')} km</Text>
                </Stack>
                {v.isUpgrade && <Pill tone="success">Upgrade to {CATEGORY_LABEL[v.category]}</Pill>}
              </Stack>
            ),
          }))} />
      </QueryState>
      {locked && <Text size="sm" tone="muted">The inspection is completed for this car. {payFirst ? 'Take the payment, then hand over the keys below.' : 'Hand over the keys below.'}</Text>}
    </Card>
  )
}

/**
 * Pay at the branch: the customer's card goes to the gateway's hosted fields (tokenize), then the
 * booking total is held on it, exactly like a card booking's online hold. Card details live in this
 * component's state only until the token comes back.
 */
function TakePayment({ booking }: { booking: CounterBooking }) {
  const tokenize = useTokenizeCard()
  const pay = useTakeCounterPayment()
  const [card, setCard] = useState<CardDetails>({ number: '', expiry: '', cvc: '' })
  const errors = validateCard(card)
  const valid = Object.keys(errors).length === 0

  const submit = async () => {
    pay.reset()
    let paymentToken: string
    try {
      paymentToken = (await tokenize.mutateAsync(card)).token
    } catch {
      return   // shown by the tokenize banner
    }
    setCard({ number: '', expiry: '', cvc: '' })
    pay.mutate({ reservationId: booking.reservationId, paymentToken })
  }

  const declined = pay.error instanceof ApiError && pay.error.code === 'payment.declined'
  const error = tokenize.isError ? describeError(tokenize.error)
    : declined ? { code: 'payment.declined', title: 'Card declined', message: 'The bank declined the card. Nothing was taken: try another card.', retryable: false }
    : pay.isError ? describeError(pay.error) : null

  return (
    <Card gap={4} data-testid="take-payment">
      <CardHeader size="sm" title="3. Take the payment"
        subtitle={<>Pay at the branch: hold <Money amount={booking.total} weight="medium" /> on the customer's card before the keys. It is taken when the car comes back (plus any late fee).</>}
        actions={booking.payment ? <Pill tone="success" dot>Paid</Pill> : <Pill tone="warning" dot>Not paid yet</Pill>} />

      {booking.payment ? (
        <Callout tone="success"><PaymentLine payment={booking.payment} /></Callout>
      ) : (
        <>
          <Callout icon="badge" size="sm">The card must be in the main driver's name ({booking.customerName}). The customer types the card details; they go to the payment gateway only.</Callout>
          <CardFields value={card} onChange={setCard} errors={errors} />
          {error && <Banner error={error} onDismiss={() => { tokenize.reset(); pay.reset() }} />}
          <FormActions>
            <Button icon="credit_card" size="lg" disabled={!valid} loading={tokenize.isPending || pay.isPending}
              loadingText={tokenize.isPending ? 'Securing card…' : 'Authorising…'} onClick={submit}>
              Hold {formatMoney(booking.total)} on the card
            </Button>
          </FormActions>
        </>
      )}
    </Card>
  )
}

function HandOver({ reservationId, vehicle, freeUpgrade, step, onDone }: { reservationId: string; vehicle: AvailableVehicle; freeUpgrade: boolean; step: number; onDone: (rentalId: string) => void }) {
  const checkOut = useCheckOut()
  return (
    <Card>
      <Stack direction="responsive" gap={4} justify="between" align="center">
        <Stack gap={1} grow>
          <CardHeader size="sm" title={`${step}. Hand over the keys`}
            subtitle={<>Inspection completed for <Text as="span" mono tone="strong">{vehicle.registration}</Text>{freeUpgrade ? ' (free upgrade, booked price)' : ''}. The start odometer comes from the inspection.</>} />
          {checkOut.isError && <Banner error={describeError(checkOut.error)} onDismiss={() => checkOut.reset()} />}
        </Stack>
        <Button icon="key" size="lg" loading={checkOut.isPending} loadingText="Checking out…"
          // mutateAsync, not mutate(…, { onSuccess }): the refetch the hook triggers re-renders this booking as
          // CheckedOut and unmounts this button, and TanStack drops per-call callbacks of an unmounted observer.
          onClick={() => checkOut.mutateAsync({ reservationId, vehicleId: vehicle.id, freeUpgrade }).then((r) => onDone(r.rentalId), () => {})}>
          Hand over keys
        </Button>
      </Stack>
    </Card>
  )
}
