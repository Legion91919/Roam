import type { Meta, StoryObj } from '@storybook/react'
import { fn } from '@storybook/test'
import { useState } from 'react'
import { Button, CheckboxCard, ChipGroup, Field, FileButton, FormActions, Grid, Input, RadioCards, Segmented, Select, Stack, Textarea, TileInput, ToggleField, UploadTile } from '.'

const meta = {
  title: 'Library/Forms',
  component: Field,
  args: { label: 'First name', hint: 'As on your ID or passport.', children: <Input /> },
} satisfies Meta<typeof Field>
export default meta
type Story = StoryObj<typeof meta>

export const FieldWithInput: Story = { name: 'Field + Input' }
export const FieldError: Story = { args: { label: 'SA ID number', error: 'This ID number is not valid.', hint: undefined, children: <Input invalid mono inputMode="numeric" defaultValue="80010150" /> } }
export const Controls: Story = {
  render: () => (
    <Grid columns={{ base: 1, md: 2 }} gap={4} className="max-w-3xl">
      <Field label="Email"><Input type="email" placeholder="you@example.com" /></Field>
      <Field label="Pick-up date"><Input type="date" leadingIcon="calendar_today" /></Field>
      <Field label="Category"><Select placeholder="Any category" options={[{ value: 'Economy', label: 'Economy' }, { value: 'Compact', label: 'Compact' }, { value: 'Suv', label: 'SUV' }]} /></Field>
      <Field label="Registration"><Input mono uppercase defaultValue="ND 123-456" /></Field>
      <Field label="Disabled" hint="From your sign-in."><Input value="thabo@example.com" disabled /></Field>
      <Field label="Small control"><Input controlSize="sm" placeholder="controlSize=sm" /></Field>
      <Field label="Reason"><Textarea placeholder="Why is the booking cancelled?" /></Field>
    </Grid>
  ),
}
export const Choices: Story = {
  render: function Render() {
    const [extras, setExtras] = useState(true)
    const [gps, setGps] = useState(false)
    const [pay, setPay] = useState<'Card' | 'PayAtBranch'>('Card')
    const [transmission, setTransmission] = useState<'Any' | 'Automatic' | 'Manual'>('Any')
    const [answer, setAnswer] = useState<'Ok' | 'Damaged' | 'Missing' | 'NotApplicable'>('Ok')
    return (
      <Stack gap={6} className="max-w-2xl">
        <RadioCards label="How do you want to pay?" value={pay} onChange={setPay} options={[
          { value: 'Card', icon: 'credit_card', title: 'Card now', description: 'The total is held on your card.' },
          { value: 'PayAtBranch', icon: 'storefront', title: 'Pay at the branch', description: 'Pay by card at the counter at pickup.' },
        ]} />
        <CheckboxCard checked={extras} onChange={setExtras}>I accept the Roam rental terms and conditions.</CheckboxCard>
        <ToggleField checked={gps} onChange={setGps} label="Show free upgrades" />
        <ChipGroup label="Transmission" value={transmission} onChange={setTransmission} options={[{ value: 'Any', label: 'Any' }, { value: 'Automatic', label: 'Automatic' }, { value: 'Manual', label: 'Manual' }]} />
        <Segmented label="Exterior: front" options={['Ok', 'Damaged', 'Missing', 'NotApplicable'] as const} value={answer} onChange={setAnswer}
          labelOf={(a) => (a === 'NotApplicable' ? 'N/A' : a === 'Ok' ? 'OK' : a)} toneOf={(a) => (a === 'Ok' ? 'brand' : a === 'Damaged' ? 'danger' : a === 'Missing' ? 'warning' : 'neutral')} />
      </Stack>
    )
  },
}
export const Tiles: Story = {
  render: () => (
    <Grid columns={{ base: 2, md: 4 }} gap={3} className="max-w-3xl">
      <TileInput label="Pick up" icon="calendar_today" type="date" defaultValue="2026-11-10" />
      <TileInput label="Return" icon="calendar_today" type="date" defaultValue="2026-11-13" />
      <UploadTile title="Front" required accept="image/jpeg,image/png" inputLabel="Front photo" onFile={fn()} />
      <UploadTile title="Back" busy accept="image/jpeg,image/png" inputLabel="Back photo" onFile={fn()} />
    </Grid>
  ),
}
export const Actions: Story = {
  render: () => (
    <Stack gap={4} className="max-w-2xl">
      <FormActions><Button variant="secondary">Cancel</Button><Button>Save profile</Button></FormActions>
      <FormActions align="between"><Button variant="secondary" icon="arrow_back">Back</Button><FileButton accept="application/pdf" onFile={fn()}>Add file</FileButton></FormActions>
    </Stack>
  ),
}
