import type { Meta, StoryObj } from '@storybook/react'
import { useState } from 'react'
import { inspection, vehicle } from '../../stories/fixtures'
import { VehicleFacts, VehicleStatusPill } from '../fleet/VehicleParts'
import { Card, Inline } from '../ui'
import { FuelGauge } from './FuelGauge'
import { InspectionReportView } from './InspectionReportView'

// Inspection and fleet feature components, built only from components/ui.
const meta = {
  title: 'Inspection/InspectionReportView',
  component: InspectionReportView,
  args: { report: inspection },
  decorators: [(Story) => <Card className="max-w-3xl"><Story /></Card>],
} satisfies Meta<typeof InspectionReportView>
export default meta
type Story = StoryObj<typeof meta>

export const ReturnWithNewDamage: Story = {}
export const CleanCheckout: Story = {
  args: { report: { ...inspection, kind: 'Checkout', newDamage: [], damage: [], checklist: inspection.checklist.map((i) => ({ ...i, status: 'Ok', note: null })) } },
}
export const Fuel: Story = {
  name: 'FuelGauge',
  render: function Render() {
    const [value, setValue] = useState<number | null>(5)
    return <div className="max-w-sm"><FuelGauge value={value} onChange={setValue} /></div>
  },
}
export const VehicleStatus: Story = {
  name: 'VehicleStatusPill',
  render: () => <Inline gap={2}>{(['Available', 'OnRent', 'InService', 'Retired'] as const).map((s) => <VehicleStatusPill key={s} status={s} />)}</Inline>,
}
export const Facts: Story = { name: 'VehicleFacts', render: () => <VehicleFacts vehicle={vehicle} /> }
