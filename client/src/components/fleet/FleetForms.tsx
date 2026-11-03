import type { VehicleFilter } from '../../api/hooks'
import type { Branch, Transmission, VehicleCategory, VehicleStatus } from '../../api/types'
import { CATEGORY_LABEL } from '../../lib/catalog'
import { Field, Grid, GridItem, Input, Segmented, Select, Stack, Text } from '../ui'
import { VEHICLE_STATUS_TEXT } from './VehicleParts'

export const CATEGORIES: VehicleCategory[] = ['Economy', 'Compact', 'Suv', 'Luxury']
const STATUSES: VehicleStatus[] = ['Available', 'OnRent', 'InService', 'Retired']
const TRANSMISSIONS = ['Manual', 'Automatic'] as const satisfies readonly Transmission[]

/** Fleet list filters: branch, category, status and a plate / VIN / model search. */
export function FleetFilters({ value, onChange, branches }: { value: VehicleFilter; onChange: (changes: Partial<VehicleFilter>) => void; branches: Branch[] | undefined }) {
  return (
    <Grid columns={{ base: 1, sm: 2, lg: 4 }} gap={3}>
      <Field label="Branch">
        <Select controlSize="sm" placeholder="All branches" value={value.branchId ?? ''} onChange={(e) => onChange({ branchId: e.target.value })}
          options={branches?.map((b) => ({ value: b.id, label: b.name }))} />
      </Field>
      <Field label="Category">
        <Select controlSize="sm" placeholder="All categories" value={value.category} onChange={(e) => onChange({ category: e.target.value as VehicleCategory | '' })}
          options={CATEGORIES.map((c) => ({ value: c, label: CATEGORY_LABEL[c] }))} />
      </Field>
      <Field label="Status">
        <Select controlSize="sm" placeholder="Any status" value={value.status} onChange={(e) => onChange({ status: e.target.value as VehicleStatus | '' })}
          options={STATUSES.map((s) => ({ value: s, label: VEHICLE_STATUS_TEXT[s] }))} />
      </Field>
      <Field label="Search">
        <Input controlSize="sm" type="search" placeholder="Plate, VIN or model" value={value.search} onChange={(e) => onChange({ search: e.target.value })} />
      </Field>
    </Grid>
  )
}

export interface VehicleFormValue {
  registration: string
  vin: string
  make: string
  model: string
  year: string
  category: VehicleCategory
  transmission: Transmission
  homeBranchId: string
  odometerKm: string
}

const PLATE = /^[A-Z]{1,3} \d{1,6}(-\d{1,4})?$/
const VIN = /^[A-HJ-NPR-Z0-9]{17}$/

/** Plate and VIN checked with the same patterns as the Domain (the server is still the authority, and rejects duplicates). */
export function vehicleFormErrors(value: VehicleFormValue) {
  const plate = value.registration.trim().toUpperCase()
  const vin = value.vin.trim().toUpperCase()
  return {
    registration: plate && !PLATE.test(plate) ? 'Use the plate as shown, e.g. "CA 123-456" or "ND 45678".' : undefined,
    vin: vin && !VIN.test(vin) ? '17 letters and digits; VINs never use I, O or Q.' : undefined,
  }
}

/** The fields of a new car (cells for a two-column form grid). */
export function VehicleFormFields({ value, onChange, branches }: { value: VehicleFormValue; onChange: (changes: Partial<VehicleFormValue>) => void; branches: Branch[] | undefined }) {
  const errors = vehicleFormErrors(value)
  return (
    <>
      <Field label="Registration" error={errors.registration}>
        <Input mono uppercase invalid={Boolean(errors.registration)} required autoComplete="off" placeholder="CA 123-456" value={value.registration} onChange={(e) => onChange({ registration: e.target.value })} />
      </Field>
      <Field label="VIN" error={errors.vin}>
        <Input mono uppercase invalid={Boolean(errors.vin)} required autoComplete="off" maxLength={17} value={value.vin} onChange={(e) => onChange({ vin: e.target.value })} />
      </Field>
      <Field label="Make"><Input required maxLength={40} placeholder="Toyota" value={value.make} onChange={(e) => onChange({ make: e.target.value })} /></Field>
      <Field label="Model"><Input required maxLength={40} placeholder="Corolla Cross" value={value.model} onChange={(e) => onChange({ model: e.target.value })} /></Field>
      <Field label="Year"><Input type="number" required min={1990} max={new Date().getFullYear() + 1} value={value.year} onChange={(e) => onChange({ year: e.target.value })} /></Field>
      <Field label="Odometer (km)"><Input type="number" required min={0} value={value.odometerKm} onChange={(e) => onChange({ odometerKm: e.target.value })} /></Field>
      <Field label="Category">
        <Select value={value.category} onChange={(e) => onChange({ category: e.target.value as VehicleCategory })} options={CATEGORIES.map((c) => ({ value: c, label: CATEGORY_LABEL[c] }))} />
      </Field>
      <Field label="Home branch">
        <Select required value={value.homeBranchId} onChange={(e) => onChange({ homeBranchId: e.target.value })}
          options={[{ value: '', label: 'Choose a branch', disabled: true }, ...(branches ?? []).map((b) => ({ value: b.id, label: b.name }))]} />
      </Field>
      <GridItem>
        <Stack gap={2}>
          <Text as="span" size="label" tone="strong">Transmission</Text>
          <Segmented label="Transmission" options={TRANSMISSIONS} value={value.transmission} onChange={(transmission) => onChange({ transmission })} />
        </Stack>
      </GridItem>
    </>
  )
}
