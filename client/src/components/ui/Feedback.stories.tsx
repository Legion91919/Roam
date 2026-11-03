import type { Meta, StoryObj } from '@storybook/react'
import { fn } from '@storybook/test'
import { Banner, Button, Callout, QueryState, Skeleton, SkeletonStack, Spinner, Stack, Text } from '.'

const meta = {
  title: 'Library/Feedback',
  component: Callout,
  args: { tone: 'info', title: 'Pay at the branch', children: 'Bring a card in the main driver’s name. The total is held on it before you get the keys.' },
  argTypes: { tone: { control: 'inline-radio', options: ['info', 'success', 'warning', 'danger'] }, size: { control: 'inline-radio', options: ['md', 'sm'] } },
} satisfies Meta<typeof Callout>
export default meta
type Story = StoryObj<typeof meta>

export const CalloutPlayground: Story = { name: 'Callout' }
export const CalloutTones: Story = {
  render: () => (
    <Stack gap={3} className="max-w-xl">
      <Callout tone="info">Info: free cancellation until pickup.</Callout>
      <Callout tone="success" icon="verified">Completed: this report is now evidence and can't be changed.</Callout>
      <Callout tone="warning" title="Due back late">The car was due back at 09:00. A late fee applies.</Callout>
      <Callout tone="danger" title="New damage since checkout" action={<Button variant="secondary" size="sm">View report</Button>}>Rear bumper: 5 cm scratch.</Callout>
    </Stack>
  ),
}
export const ErrorBanner: Story = {
  render: () => (
    <Stack gap={3} className="max-w-xl">
      <Banner error={{ code: 'network', title: 'Can’t reach Roam', message: 'Check your connection and try again.', retryable: true }} onRetry={fn()} />
      <Banner error={{ code: 'payment.declined', title: 'Card declined', message: 'The bank declined the card. Try another card.', retryable: false }} onDismiss={fn()} />
    </Stack>
  ),
}
export const Loading: Story = {
  render: () => (
    <Stack gap={4} className="max-w-xl">
      <Skeleton size="title" />
      <SkeletonStack count={3} />
      <Skeleton size="block" />
      <Text tone="muted" icon="hourglass_empty">Spinner: <Spinner /></Text>
    </Stack>
  ),
}
export const QueryStates: Story = {
  render: () => (
    <Stack gap={4} className="max-w-xl">
      <QueryState query={{ isLoading: true, isError: false, error: null, refetch: fn() }} loading="row"><Text>Loaded</Text></QueryState>
      <QueryState query={{ isLoading: false, isError: true, error: new Error('offline'), refetch: fn() }}><Text>Loaded</Text></QueryState>
      <QueryState query={{ isLoading: false, isError: false, error: null, refetch: fn() }}><Text tone="strong">Loaded: the content renders.</Text></QueryState>
    </Stack>
  ),
}
