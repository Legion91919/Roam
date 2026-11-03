import type { Meta, StoryObj } from '@storybook/react'
import { fn } from '@storybook/test'
import { availability, quote } from '../../stories/fixtures'
import { Grid } from '../ui'
import { CategoryCard, CategoryCardSkeleton } from './CategoryCard'
import { PriceBreakdown } from './PriceBreakdown'

// Feature components for the customer booking flow, built only from components/ui.
const meta = {
  title: 'Booking/CategoryCard',
  component: CategoryCard,
  args: { availability: availability[1], days: 3, selected: false, onSelect: fn() },
} satisfies Meta<typeof CategoryCard>
export default meta
type Story = StoryObj<typeof meta>

/** One card is shown at the width it has in the results grid. */
const oneCard = [(Story: () => JSX.Element) => <div className="max-w-sm"><Story /></div>]

export const Available: Story = { decorators: oneCard }
export const Selected: Story = { args: { selected: true }, decorators: oneCard }
export const SoldOut: Story = { args: { availability: availability[2] }, decorators: oneCard }
export const Loading: Story = { render: () => <CategoryCardSkeleton />, decorators: oneCard }
export const Results: Story = {
  render: (args) => (
    <Grid columns={{ base: 1, md: 3 }} gap={4}>
      {availability.map((a, i) => <CategoryCard key={a.category} {...args} availability={a} selected={i === 1} />)}
    </Grid>
  ),
}
export const Price: Story = { name: 'PriceBreakdown', render: () => <PriceBreakdown quote={quote} loading={false} /> }
export const PriceLoading: Story = { name: 'PriceBreakdown (loading)', render: () => <PriceBreakdown quote={undefined} loading /> }
