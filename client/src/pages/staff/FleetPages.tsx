import { useState, type FormEvent } from 'react'
import { describeError } from '../../api/client'
import { useBranches, useRegisterVehicle, useVehicleHistory, useVehicles, type VehicleFilter } from '../../api/hooks'
import type { VehicleListItem } from '../../api/types'
import { FleetFilters, vehicleFormErrors, VehicleFormFields, type VehicleFormValue } from '../../components/fleet/FleetForms'
import { InspectionReportDialog, VehicleTimeline } from '../../components/fleet/VehicleHistory'
import { km, VehicleFacts, VehicleStatusActions, VehicleStatusPill } from '../../components/fleet/VehicleParts'
import { StaffLayout } from '../../components/staff/StaffLayout'
import {
  BackLink, Banner, Button, Card, CardSection, DataTable, EmptyState, FormActions, Grid, GridItem, Pagination, QueryState, SplitLayout, Stack, Text, TextLink, type Column,
} from '../../components/ui'
import { CATEGORY_LABEL } from '../../lib/catalog'
import { navigate } from '../../lib/router'
import { useStaffBranch } from '../../lib/staffPrefs'

const COLUMNS: Column<VehicleListItem>[] = [
  { key: 'registration', header: 'Registration', cell: (v) => <Text as="span" mono><TextLink href={`#/agent/fleet/${v.id}`}>{v.registration}</TextLink></Text> },
  { key: 'car', header: 'Car', cell: (v) => <Text as="span" tone="strong">{v.make} {v.model} <Text as="span" tone="muted">{v.year} · {v.transmission}</Text></Text> },
  { key: 'category', header: 'Category', cell: (v) => CATEGORY_LABEL[v.category] },
  { key: 'branch', header: 'Branch', cell: (v) => <><Text as="span" mono tone="muted">{v.currentBranchCode}</Text> {v.currentBranchName}</> },
  { key: 'odometer', header: 'Odometer', align: 'right', cell: (v) => <Text as="span" tabular>{km(v.odometerKm)}</Text> },
  { key: 'status', header: 'Status', cell: (v) => <VehicleStatusPill status={v.status} /> },
]

/** Every car in the fleet, filterable by branch (defaults to the counter's branch), category, status and plate/VIN/model. */
export function FleetPage() {
  const [staffBranch] = useStaffBranch()
  const branches = useBranches()
  const [filter, setFilter] = useState<VehicleFilter>({ branchId: staffBranch, category: '', status: '', search: '', page: 1 })
  const vehicles = useVehicles(filter)
  const data = vehicles.data

  return (
    <StaffLayout page="fleet" title="Fleet" subtitle="Cars at each branch, their status and history."
      actions={<Button icon="add" onClick={() => navigate('/agent/fleet/new')}>Add vehicle</Button>}>
      <Card padding="none">
        <CardSection padding="sm"><FleetFilters value={filter} branches={branches.data} onChange={(changes) => setFilter((f) => ({ ...f, page: 1, ...changes }))} /></CardSection>
        <CardSection padding="none" divider>
          <DataTable columns={COLUMNS} rows={data?.items} rowKey={(v) => v.id} onRowClick={(v) => navigate(`/agent/fleet/${v.id}`)} minWidth={820}
            loading={vehicles.isLoading} error={vehicles.isError ? describeError(vehicles.error) : null} onRetry={() => vehicles.refetch()}
            empty={<EmptyState icon="directions_car" message="No cars match these filters." />} />
        </CardSection>
        {data && <Pagination page={data.page} pageSize={data.pageSize} total={data.total} noun="cars" onPage={(page) => setFilter((f) => ({ ...f, page }))} />}
      </Card>
    </StaffLayout>
  )
}

/**
 * Register a new car. A new car adds one to the category's capacity at its home branch in the same
 * transaction (server side).
 */
export function AddVehiclePage() {
  const [staffBranch] = useStaffBranch()
  const branches = useBranches()
  const register = useRegisterVehicle()
  const [form, setForm] = useState<VehicleFormValue>({ registration: '', vin: '', make: '', model: '', year: String(new Date().getFullYear()), category: 'Economy', transmission: 'Manual', homeBranchId: staffBranch, odometerKm: '0' })
  const errors = vehicleFormErrors(form)

  const submit = (e: FormEvent) => {
    e.preventDefault()
    register.mutate({
      registration: form.registration.trim().toUpperCase(), vin: form.vin.trim().toUpperCase(), make: form.make.trim(), model: form.model.trim(), year: Number(form.year),
      category: form.category, transmission: form.transmission, homeBranchId: form.homeBranchId, odometerKm: Number(form.odometerKm),
    }, { onSuccess: (vehicle) => navigate(`/agent/fleet/${vehicle.id}`) })
  }

  return (
    <StaffLayout page="vehicleNew" title="Add vehicle" subtitle="Register a car to a branch. It is available for checkout straight away.">
      <BackLink href="#/agent/fleet">Fleet</BackLink>
      <Card padding="lg" width="form">
        <Grid as="form" columns={{ base: 1, sm: 2 }} gap={5} onSubmit={submit}>
          <VehicleFormFields value={form} branches={branches.data} onChange={(changes) => setForm((f) => ({ ...f, ...changes }))} />
          {register.isError && <GridItem><Banner error={describeError(register.error)} /></GridItem>}
          <GridItem>
            <FormActions>
              <Button variant="secondary" onClick={() => navigate('/agent/fleet')}>Cancel</Button>
              <Button type="submit" icon="add" loading={register.isPending} loadingText="Registering…" disabled={Boolean(errors.registration || errors.vin)}>Register vehicle</Button>
            </FormActions>
          </GridItem>
        </Grid>
      </Card>
    </StaffLayout>
  )
}

/** One car: its details, the status actions that apply to its status, and its full history (rentals with their inspection reports, service, moves). */
export function VehiclePage({ id }: { id: string }) {
  const history = useVehicleHistory(id)
  const [report, setReport] = useState<string | null>(null)
  const vehicle = history.data?.vehicle

  return (
    <StaffLayout page="vehicle" title={vehicle ? vehicle.registration : 'Vehicle'} subtitle={vehicle ? `${vehicle.make} ${vehicle.model} ${vehicle.year}` : undefined}
      actions={vehicle && <VehicleStatusPill status={vehicle.status} />}>
      <BackLink href="#/agent/fleet">Fleet</BackLink>
      <QueryState query={history} loading="panel">
        {history.data && vehicle && (
          <SplitLayout breakpoint="xl" asideWidth="sm" asidePosition="start" aside={<Stack gap={6}><VehicleFacts vehicle={vehicle} /><VehicleStatusActions vehicle={vehicle} /></Stack>}>
            <VehicleTimeline history={history.data} onOpenReport={setReport} />
          </SplitLayout>
        )}
      </QueryState>
      <InspectionReportDialog id={report} onClose={() => setReport(null)} />
    </StaffLayout>
  )
}
