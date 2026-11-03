import type { Meta, StoryObj } from '@storybook/react'
import { BackLink, Eyebrow, Heading, Icon, Inline, Money, Stack, Text, TextLink } from '.'

const meta = {
  title: 'Library/Typography',
  component: Text,
  args: { children: 'Pick up at the counter in arrivals.' },
  argTypes: {
    size: { control: 'inline-radio', options: ['lg', 'md', 'sm', 'label'] },
    tone: { control: 'select', options: ['default', 'strong', 'muted', 'subtle', 'brand', 'danger', 'warning'] },
  },
} satisfies Meta<typeof Text>
export default meta
type Story = StoryObj<typeof meta>

export const TextPlayground: Story = { name: 'Text' }
export const Scale: Story = {
  render: () => (
    <Stack gap={3}>
      <Eyebrow>Step 4 of 4</Eyebrow>
      <Heading level={1} size="xl">Heading xl</Heading>
      <Heading level={2} size="lg">Heading lg</Heading>
      <Heading level={3} size="md" icon="key">Heading md with icon</Heading>
      <Heading level={4} size="sm">Heading sm</Heading>
      <Text size="lg">Body large: the keys are handed over at the counter.</Text>
      <Text>Body: age, licence and loyalty come from your verified profile.</Text>
      <Text size="sm" tone="muted">Small, muted: shown next to a field.</Text>
      <Text mono tone="strong">ROAM-7C3E-5A10-1111-4ABC</Text>
    </Stack>
  ),
}
export const Tones: Story = {
  render: () => (
    <Stack gap={1}>
      {(['default', 'strong', 'muted', 'subtle', 'brand', 'danger', 'warning'] as const).map((tone) => <Text key={tone} tone={tone}>{tone}</Text>)}
    </Stack>
  ),
}
export const Amounts: Story = {
  render: () => (
    <Inline gap={6} align="baseline">
      <Money amount={1350} size="display" tone="brand" />
      <Money amount={1350} size="xl" />
      <Money amount={1350} />
      <Money amount={1500} strike tone="subtle" />
    </Inline>
  ),
}
export const Links: Story = {
  render: () => (
    <Stack gap={3}>
      <TextLink href="#" icon="edit_calendar">Change dates</TextLink>
      <BackLink href="#">Back to vehicles</BackLink>
      <Inline gap={2}><Icon name="flight_land" /><Icon name="key" /><Icon name="verified" filled /></Inline>
    </Stack>
  ),
}
