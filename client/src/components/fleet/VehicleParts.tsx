import { useState } from 'react'
import { describeError } from '../../api/client'
import { useVehicleAction, type VehicleAction } from '../../api/hooks'
import type { Vehicle, VehicleStatus } from '../../api/types'
import { CATEGORY_LABEL } from '../../lib/catalog'
import { formatDay } from '../../lib/format'
import { navigate } from '../../lib/router'
import { Banner, Button, Card, DataList, Dialog, Field, Input, Pill, Stack, Text, Textarea } from '../ui'

export const VEHICLE_STATUS_TEXT: Record<VehicleStatus, string> = { Available: 'Available', OnRent: 'On rent', InService: 'In service', Retired: 'Retired' }

/** "2 450 km", or a dash when there is no reading. */
export const km = (value: number | null | undefined) => (value == null ? '–' : `${value.toLocaleString('en-ZA')} km`)

export function VehicleStatusPill({ status }: { status: VehicleStatus }) {
  const tone = status === 'Available' ? 'success' : status === 'OnRent' ? 'solid' : status === 'InService' ? 'warning' : 'neutral'
  return <Pill tone={tone} dot>{VEHICLE_STATUS_TEXT[status]}</Pill>
}

/** The car's fact sheet, with "Take the return" while it is on rent. */
export function VehicleFacts({ vehicle }: { vehicle: Vehicle }) {
  return (
    <Card gap={4}>
      <DataList layout="rows" items={[
        { label: 'VIN', value: vehicle.vin, mono: true },
        { label: 'Category', value: CATEGORY_LABEL[vehicle.category] },
        { label: 'Transmission', value: vehicle.transmission },
        { label: 'Odometer', value: <span className="tabular">{km(vehicle.odometerKm)}</span> },
        { label: 'Home branch', value: vehicle.homeBranchName },
        { label: 'Now at', value: vehicle.currentBranchName },
        { label: 'In the fleet since', value: formatDay(vehicle.createdAt) },
      ]} />
      {vehicle.currentReservationId && (
        <Button variant="tonal" block icon="assignment_return" onClick={() => navigate(`/agent/returns/${vehicle.currentReservationId}`)}>Take the return</Button>
      )}
    </Card>
  )
}

const ACTION_TITLE: Record<VehicleAction['kind'], string> = { service: 'Send to service', serviceReturn: 'Back from service', retire: 'Retire this car' }

/** Only the transitions the Domain allows from the current status are offered: Available → service/retire; In service → back (odometer) /retire. */
export function VehicleStatusActions({ vehicle }: { vehicle: Vehicle }) {
  const action = useVehicleAction(vehicle.id)
  const [open, setOpen] = useState<VehicleAction['kind'] | null>(null)
  const [text, setText] = useState('')
  const close = () => { setOpen(null); setText(''); action.reset() }

  if (vehicle.status === 'OnRent' || vehicle.status === 'Retired') {
    return (
      <Card>
        <Text tone="muted">{vehicle.status === 'OnRent' ? 'On rent: status changes are available after the return.' : 'Retired cars stay on record for their history and cannot be changed.'}</Text>
      </Card>
    )
  }

  const submit = () => {
    const request: VehicleAction =
      open === 'serviceReturn' ? { kind: 'serviceReturn', odometerKm: Number(text) } :
      open === 'service' ? { kind: 'service', reason: text.trim() } : { kind: 'retire', reason: text.trim() }
    action.mutate(request, { onSuccess: close })
  }
  const valid = open === 'serviceReturn' ? text !== '' && Number(text) >= vehicle.odometerKm : text.trim().length > 0

  return (
    <Card gap={3}>
      <Text as="span" size="label" tone="strong">Status</Text>
      {vehicle.status === 'Available' && <Button variant="secondary" block icon="build" onClick={() => setOpen('service')}>Send to service</Button>}
      {vehicle.status === 'InService' && <Button block icon="check_circle" onClick={() => { setText(String(vehicle.odometerKm)); setOpen('serviceReturn') }}>Back from service</Button>}
      <Button variant="danger" block icon="block" onClick={() => setOpen('retire')}>Retire</Button>

      <Dialog open={open !== null} onClose={close} title={open ? ACTION_TITLE[open] : ''}
        footer={<>
          <Button variant="secondary" onClick={close}>Back</Button>
          <Button variant={open === 'retire' ? 'danger' : 'primary'} loading={action.isPending} disabled={!valid} onClick={submit}>{open ? ACTION_TITLE[open] : ''}</Button>
        </>}>
        <Stack gap={4}>
          {open === 'serviceReturn' ? (
            <Field label="Odometer now (km)" hint={`At least ${km(vehicle.odometerKm)}.`}>
              <Input type="number" min={vehicle.odometerKm} value={text} onChange={(e) => setText(e.target.value)} />
            </Field>
          ) : (
            <Field label="Reason" hint={open === 'retire' ? 'Retiring removes the car from its category capacity. It cannot be undone.' : 'Shown in the car history.'}>
              <Textarea maxLength={200} value={text} onChange={(e) => setText(e.target.value)} />
            </Field>
          )}
          {action.isError && <Banner error={describeError(action.error)} />}
        </Stack>
      </Dialog>
    </Card>
  )
}
