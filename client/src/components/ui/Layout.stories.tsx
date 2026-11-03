import type { Meta, StoryObj } from '@storybook/react'
import type { ReactNode } from 'react'
import { Card, Grid, GridItem, Inline, SplitLayout, Stack, Text } from '.'

/** A placeholder block so the spacing of the layout primitives is visible. */
const Box = ({ children }: { children: ReactNode }) => <Card padding="sm" tone="muted"><Text tone="strong">{children}</Text></Card>

const meta = { title: 'Library/Layout', component: Stack } satisfies Meta<typeof Stack>
export default meta
type Story = StoryObj<typeof meta>

export const StackColumn: Story = {
  name: 'Stack',
  args: { gap: 4 },
  argTypes: { gap: { control: 'select', options: [0, 1, 2, 3, 4, 5, 6, 8, 10] }, direction: { control: 'inline-radio', options: ['column', 'row', 'responsive'] } },
  render: (args) => <Stack {...args}><Box>One</Box><Box>Two</Box><Box>Three</Box></Stack>,
}
export const InlineRow: Story = {
  name: 'Inline',
  render: () => <Inline gap={2}>{['GPS', 'Child seat', 'Automatic', '5 seats', '3 bags'].map((t) => <Box key={t}>{t}</Box>)}</Inline>,
}
export const GridColumns: Story = {
  name: 'Grid',
  render: () => (
    <Grid columns={{ base: 1, sm: 2, lg: 4 }} gap={4}>
      {['Economy', 'Compact', 'SUV', 'Luxury'].map((t) => <Box key={t}>{t}</Box>)}
      <GridItem span="full"><Box>GridItem span="full"</Box></GridItem>
    </Grid>
  ),
}
export const Split: Story = {
  name: 'SplitLayout',
  render: () => (
    <SplitLayout aside={<Box>Aside: the price summary</Box>}>
      <Stack gap={4}><Box>Main column</Box><Box>More content</Box></Stack>
    </SplitLayout>
  ),
}
