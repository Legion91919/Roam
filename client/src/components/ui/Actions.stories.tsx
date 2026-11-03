import type { Meta, StoryObj } from '@storybook/react'
import { fn } from '@storybook/test'
import { Button, CopyButton, Inline, Stack } from '.'

const meta = {
  title: 'Library/Actions/Button',
  component: Button,
  args: { children: 'Continue', onClick: fn() },
  argTypes: {
    variant: { control: 'inline-radio', options: ['primary', 'secondary', 'tonal', 'ghost', 'danger'] },
    size: { control: 'inline-radio', options: ['sm', 'md', 'lg'] },
  },
} satisfies Meta<typeof Button>
export default meta
type Story = StoryObj<typeof meta>

export const Primary: Story = { args: { variant: 'primary', trailingIcon: 'arrow_forward' } }
export const Variants: Story = {
  render: (args) => (
    <Inline gap={3}>
      <Button {...args} variant="primary">Primary</Button>
      <Button {...args} variant="secondary">Secondary</Button>
      <Button {...args} variant="tonal">Tonal</Button>
      <Button {...args} variant="ghost">Ghost</Button>
      <Button {...args} variant="danger" icon="cancel">Danger</Button>
    </Inline>
  ),
}
export const Sizes: Story = {
  render: (args) => (
    <Inline gap={3} align="center">
      <Button {...args} size="sm">Small</Button>
      <Button {...args} size="md">Medium</Button>
      <Button {...args} size="lg">Large</Button>
    </Inline>
  ),
}
export const States: Story = {
  render: (args) => (
    <Stack gap={3} className="max-w-sm">
      <Button {...args} loading loadingText="Saving…">Save</Button>
      <Button {...args} disabled>Disabled</Button>
      <Button {...args} block icon="key">Block (full width)</Button>
      <Button {...args} icon="key" shortLabel="Check out">Check out the car</Button>
    </Stack>
  ),
}
export const Copy: Story = { render: () => <CopyButton text="ROAM-7C3E-5A10-1111-4ABC" /> }
