import { useState } from 'react'
import { describeError } from '../../api/client'
import { useAgentCancel, useMarkNoShow } from '../../api/hooks'
import type { CounterBooking } from '../../api/types'
import { formatTime } from '../../lib/format'
import { Banner, Button, Dialog, Field, Inline, Stack, Text, Textarea } from '../ui'

const NO_SHOW_AFTER_MS = 2 * 60 * 60 * 1000

/**
 * Staff end a booking: cancel (any confirmed booking, with a reason the customer will see) or record
 * a no-show (from pickup + 2h; one day's rate captured). The server enforces both rules; the buttons
 * only reflect them.
 */
export function BookingActions({ booking, compact = false }: { booking: CounterBooking; compact?: boolean }) {
  const [dialog, setDialog] = useState<'cancel' | 'noShow' | null>(null)
  const [reason, setReason] = useState('')
  const cancel = useAgentCancel()
  const noShow = useMarkNoShow()

  if (booking.status !== 'Confirmed') return null
  const noShowFrom = new Date(new Date(booking.pickupAt).getTime() + NO_SHOW_AFTER_MS)
  const canNoShow = Date.now() >= noShowFrom.getTime()
  const close = () => { setDialog(null); setReason(''); cancel.reset(); noShow.reset() }

  return (
    <>
      <Inline gap={2}>
        <Button variant="danger" size={compact ? 'sm' : 'md'} icon="event_busy" onClick={() => setDialog('cancel')}>Cancel booking</Button>
        <Button variant="secondary" size={compact ? 'sm' : 'md'} icon="person_off" disabled={!canNoShow}
          title={canNoShow ? undefined : `Available from ${formatTime(noShowFrom.toISOString())}`} onClick={() => setDialog('noShow')}>
          Mark no-show
        </Button>
      </Inline>

      <Dialog open={dialog === 'cancel'} onClose={close} title={`Cancel ${booking.reference}?`}
        footer={<>
          <Button variant="secondary" onClick={close}>Keep booking</Button>
          <Button variant="danger" icon="event_busy" loading={cancel.isPending} loadingText="Cancelling…" disabled={!reason.trim()}
            onClick={() => cancel.mutate({ reservationId: booking.reservationId, reason: reason.trim() }, { onSuccess: close })}>
            Cancel booking
          </Button>
        </>}>
        <Stack gap={4}>
          <Text tone="muted">The car goes back to inventory{booking.payment ? ` and the card hold of ${booking.customerName} is released` : ''}.</Text>
          <Field label="Reason (the customer will see this)">
            <Textarea maxLength={200} value={reason} onChange={(e) => setReason(e.target.value)} placeholder="e.g. Customer phoned to cancel" />
          </Field>
          {cancel.isError && <Banner error={describeError(cancel.error)} />}
        </Stack>
      </Dialog>

      <Dialog open={dialog === 'noShow'} onClose={close} title="Record a no-show?"
        footer={<>
          <Button variant="secondary" onClick={close}>Back</Button>
          <Button icon="person_off" loading={noShow.isPending} loadingText="Recording…" onClick={() => noShow.mutate(booking.reservationId, { onSuccess: close })}>Record no-show</Button>
        </>}>
        <Stack gap={4}>
          <Text tone="muted">
            {booking.customerName} did not collect the car.{' '}
            {booking.payment ? "One day's rate is charged as the no-show fee and the rest of the card hold is released." : 'Paying at the branch: there is no card to charge a no-show fee to.'}{' '}
            The car goes back to inventory.
          </Text>
          {noShow.isError && <Banner error={describeError(noShow.error)} />}
        </Stack>
      </Dialog>
    </>
  )
}
