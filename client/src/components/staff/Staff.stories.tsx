import type { Meta, StoryObj } from '@storybook/react'
import { fn } from '@storybook/test'
import { booking, bookings } from '../../stories/fixtures'
import { Button } from '../ui'
import { BookingSummary } from './BookingSummary'
import { BookingTable } from './BookingTable'

// Counter (staff app) feature components, built only from components/ui.
const meta = {
  title: 'Staff/BookingTable',
  component: BookingTable,
  args: {
    title: 'Pickups', icon: 'key', when: 'pickup', rows: bookings, loading: false, error: null, onRetry: fn(),
    empty: 'No pickups at this branch on this day.',
    action: () => <Button size="sm" icon="key">Check out</Button>,
  },
} satisfies Meta<typeof BookingTable>
export default meta
type Story = StoryObj<typeof meta>

export const Pickups: Story = {}
export const Loading: Story = { args: { rows: undefined, loading: true } }
export const Empty: Story = { args: { rows: [] } }
export const WaitingForBranch: Story = { args: { rows: undefined, waitingFor: 'Choose your branch at the top to see its bookings.' } }
export const Failed: Story = { args: { rows: undefined, error: { code: 'network', title: 'Can’t reach Roam', message: 'Check your connection and try again.', retryable: true } } }
export const Summary: Story = { name: 'BookingSummary', render: () => <BookingSummary booking={booking} /> }
export const SummaryOnRent: Story = { name: 'BookingSummary (on rent)', render: () => <BookingSummary booking={bookings[2]} /> }
export const SummaryPayAtBranch: Story = { name: 'BookingSummary (pay at branch)', render: () => <BookingSummary booking={bookings[1]} /> }
