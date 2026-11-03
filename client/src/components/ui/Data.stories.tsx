import type { Meta, StoryObj } from '@storybook/react'
import { fn } from '@storybook/test'
import { useState } from 'react'
import { BulletList, Button, Card, CheckList, DataList, DataTable, EmptyState, Inline, Pagination, Pill, Stack, Steps, Text, Timeline, TimelineItem, type Column } from '.'

const meta = {
  title: 'Library/Data',
  component: DataList,
  args: {
    columns: 2,
    items: [
      { label: 'Main driver', value: 'Thabo Nkosi' },
      { label: 'SA ID number', value: '•••••••••1082', mono: true },
      { label: 'Age at pickup', value: '40' },
      { label: 'Licence issued', value: 'Mon, 10 Jan 2005' },
    ],
  },
  argTypes: { columns: { control: 'inline-radio', options: [1, 2, 3, 4] }, layout: { control: 'inline-radio', options: ['stacked', 'rows'] } },
} satisfies Meta<typeof DataList>
export default meta
type Story = StoryObj<typeof meta>

export const Stacked: Story = {}
export const Rows: Story = { args: { layout: 'rows', columns: 1 } }
export const Panel: Story = { args: { panel: true } }

interface Row { ref: string; customer: string; status: 'Confirmed' | 'On rent' | 'Cancelled' }
const rows: Row[] = [
  { ref: 'ROAM-7C3E-5A10-1111-4ABC', customer: 'Thabo Nkosi', status: 'Confirmed' },
  { ref: 'ROAM-5B1D-0C2E-4444-4ABC', customer: 'Ayesha Patel', status: 'On rent' },
  { ref: 'ROAM-2F0D-6C1E-9B8A-4C7D', customer: 'Johan van Wyk', status: 'Cancelled' },
]
const columns: Column<Row>[] = [
  { key: 'ref', header: 'Booking', cell: (r) => <Text as="span" mono tone="brand">{r.ref}</Text>, nowrap: true },
  { key: 'customer', header: 'Customer', cell: (r) => r.customer },
  { key: 'status', header: 'Status', cell: (r) => <Pill dot tone={r.status === 'Confirmed' ? 'success' : r.status === 'On rent' ? 'warning' : 'danger'}>{r.status}</Pill> },
  { key: 'action', header: 'Action', srOnlyHeader: true, align: 'right', cell: () => <Button variant="ghost" size="sm">View</Button> },
]
const states = { loading: false, error: null, empty: 'No pickups at this branch on this day.' }

export const Table: Story = { render: () => <Card padding="none"><DataTable columns={columns} rows={rows} rowKey={(r) => r.ref} {...states} /></Card> }
export const TableLoading: Story = { render: () => <Card padding="none"><DataTable columns={columns} rows={undefined} rowKey={(r) => r.ref} {...states} loading /></Card> }
export const TableEmpty: Story = { render: () => <Card padding="none"><DataTable columns={columns} rows={[]} rowKey={(r) => r.ref} {...states} /></Card> }
export const TableError: Story = {
  render: () => <Card padding="none"><DataTable columns={columns} rows={undefined} rowKey={(r) => r.ref} {...states} onRetry={fn()}
    error={{ code: 'network', title: 'Can’t reach Roam', message: 'Check your connection and try again.', retryable: true }} /></Card>,
}
export const Empty: Story = { render: () => <Card><EmptyState icon="event_busy" title="No bookings yet" message="Your bookings show here once you book a car." action={<Button>Find a car</Button>} /></Card> }
export const Lists: Story = {
  render: () => (
    <Stack gap={6} className="max-w-md">
      <CheckList items={[{ ok: true, text: 'Every checklist item answered' }, { ok: true, text: 'Odometer and fuel recorded' }, { ok: false, text: 'Missing photos: back, left' }]} />
      <BulletList items={['Rear bumper: 5 cm scratch (minor)', 'Windscreen: chip on the passenger side']} />
    </Stack>
  ),
}
export const History: Story = {
  render: () => (
    <Timeline>
      <TimelineItem icon="key" title="Rental · King Shaka International Airport" meta="25 Oct 10:31"><Text tone="muted">17 200 km → 18 420 km</Text></TimelineItem>
      <TimelineItem icon="build" title="Sent to service" meta="20 Oct 08:00" />
      <TimelineItem icon="add_circle" title="Added to the fleet" meta="10 Jan 08:00"><Text size="sm" tone="muted">12 km</Text></TimelineItem>
    </Timeline>
  ),
}
export const Pills: Story = {
  render: () => (
    <Inline gap={2}>
      <Pill tone="success" dot>Confirmed</Pill><Pill tone="warning" dot>On rent</Pill><Pill tone="danger" dot>Cancelled</Pill>
      <Pill tone="neutral">Draft</Pill><Pill tone="solid">Best value</Pill>
    </Inline>
  ),
}
export const Stepper: Story = {
  render: function Render() {
    const [step, setStep] = useState(1)
    return <Card padding="none"><Steps label="Inspection steps" steps={['Checklist', 'Fuel & odometer', 'Photos', 'Review']} current={step} isDone={(i) => i < step} onSelect={setStep} /></Card>
  },
}
export const Pages: Story = {
  render: function Render() {
    const [page, setPage] = useState(1)
    return <Pagination page={page} pageSize={10} total={42} noun="cars" onPage={setPage} />
  },
}
