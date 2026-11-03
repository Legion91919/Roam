import type { Meta, StoryObj } from '@storybook/react'
import { fn } from '@storybook/test'
import { useState } from 'react'
import { Button, Card, CardFooter, CardHeader, CardSection, DataList, Dialog, IconBadge, Inline, LinkCard, Money, Pill, Stack, Text } from '.'

const meta = {
  title: 'Library/Surfaces/Card',
  component: Card,
  argTypes: {
    padding: { control: 'inline-radio', options: ['none', 'sm', 'md', 'lg'] },
    tone: { control: 'inline-radio', options: ['default', 'accent', 'danger', 'muted'] },
  },
  args: { padding: 'md', tone: 'default', children: <Text>A card: white surface, hairline border, 8px radius.</Text> },
} satisfies Meta<typeof Card>
export default meta
type Story = StoryObj<typeof meta>

export const Basic: Story = {}
export const WithSections: Story = {
  args: { padding: 'none' },
  render: (args) => (
    <Card {...args} className="max-w-xl">
      <CardSection>
        <CardHeader icon="directions_car" title="Your trip" subtitle="King Shaka International Airport" actions={<Pill tone="success" dot>Confirmed</Pill>} />
      </CardSection>
      <CardSection divider>
        <DataList columns={2} items={[{ label: 'Pick up', value: 'Tue, 10 Nov 2026', hint: '09:00 SAST', hintMono: true }, { label: 'Return', value: 'Fri, 13 Nov 2026', hint: '09:00 SAST', hintMono: true }]} />
      </CardSection>
      <CardFooter tone="muted">
        <Text tone="muted">Total (incl. VAT)</Text>
        <Money amount={1350} size="lg" tone="brand" />
      </CardFooter>
    </Card>
  ),
}
export const Tones: Story = {
  render: () => (
    <Stack gap={3} className="max-w-xl">
      {(['default', 'accent', 'muted', 'danger'] as const).map((tone) => <Card key={tone} tone={tone}><Text tone="strong">tone="{tone}"</Text></Card>)}
    </Stack>
  ),
}
export const Badges: Story = {
  render: () => (
    <Inline gap={3}>
      {(['brand', 'neutral', 'accent', 'danger', 'outline'] as const).map((tone) => <IconBadge key={tone} icon="key" tone={tone} />)}
      <IconBadge icon="verified" size="lg" tone="accent" />
    </Inline>
  ),
}
export const Link: Story = {
  render: () => (
    <div className="max-w-md">
      <LinkCard href="#">
        <Stack gap={1}><Text tone="strong" weight="medium">ROAM-7C3E-5A10-1111-4ABC</Text><Text size="sm" tone="muted">Compact · Tue, 10 Nov 2026</Text></Stack>
      </LinkCard>
    </div>
  ),
}
export const DialogStory: Story = {
  name: 'Dialog',
  render: function Render() {
    const [open, setOpen] = useState(true)
    const close = fn()
    return (
      <>
        <Button onClick={() => setOpen(true)}>Open dialog</Button>
        <Dialog open={open} onClose={() => { close(); setOpen(false) }} eyebrow="ROAM-7C3E" title="Cancel this booking?"
          footer={<Inline gap={3} justify="end"><Button variant="secondary" onClick={() => setOpen(false)}>Keep booking</Button><Button variant="danger" icon="cancel">Cancel booking</Button></Inline>}>
          <Text>The card hold is released straight away. This can't be undone.</Text>
        </Dialog>
      </>
    )
  },
}
