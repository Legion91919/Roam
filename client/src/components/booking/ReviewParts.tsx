import type { DescribedError } from '../../api/client'
import type { Branch, CustomerProfile, VehicleCategory } from '../../api/types'
import { CATEGORY_INFO, CATEGORY_LABEL } from '../../lib/catalog'
import { formatDay } from '../../lib/format'
import { Banner, Card, CardHeader, DataList, IconBadge, Inline, Pill, Stack, Text, TextLink } from '../ui'
import { CarThumb } from './CarIllustration'

/** "Review and book": the verified driver the price is based on, with a link to edit the profile. */
export function DriverCard({ driver, age, editHref, eligibility }: { driver: CustomerProfile; age: number; editHref: string; eligibility: DescribedError | null }) {
  return (
    <Card padding="lg" gap={6}>
      <CardHeader level={1} size="lg" eyebrow="Step 4 of 4" title="Review and book"
        subtitle="Age, licence and loyalty come from your verified driver profile."
        actions={<TextLink href={editHref} icon="edit">Edit profile</TextLink>} />
      <DataList panel items={[
        { label: 'Main driver', value: <Text as="span" size="lg" tone="strong" weight="medium">{driver.firstName} {driver.lastName}</Text> },
        { label: driver.documentType === 'Passport' ? `Passport (${driver.issuingCountry})` : 'SA ID number', value: driver.maskedDocumentNumber, mono: true },
        { label: 'Age at pickup', value: <Inline gap={2}>{age}{age < 25 && <Pill tone="warning">Young driver fee</Pill>}</Inline> },
        { label: 'Licence issued', value: formatDay(driver.licenceIssuedOn) },
        {
          label: 'Loyalty', wide: true,
          value: <Text as="span">{driver.loyaltyTier === 'None' ? 'No tier yet. Tiers are awarded by Roam and apply automatically.' : `${driver.loyaltyTier} member: discount applied after the weekly discount.`}</Text>,
        },
      ]} />
      {eligibility && <Banner error={eligibility} />}
    </Card>
  )
}

/** The chosen category in the price summary. */
export function CarSummary({ category }: { category: VehicleCategory }) {
  const info = CATEGORY_INFO[category]
  return (
    <div className="flex items-center gap-4 rounded-card bg-surface-container-low p-3">
      <CarThumb category={category} onWhite />
      <Stack gap={0} grow>
        <Text tone="strong" weight="medium" truncate>{CATEGORY_LABEL[category]} · {info.example}</Text>
        <Text size="sm" tone="muted">{info.transmission} · {info.seats} seats · {info.bags} bags</Text>
      </Stack>
    </div>
  )
}

/** Where to collect the car. */
export function BranchCard({ branch }: { branch: Branch }) {
  return (
    <Card padding="sm">
      <div className="flex items-center gap-4">
        <IconBadge icon={branch.type === 'Airport' ? 'flight' : 'storefront'} />
        <Stack gap={0}>
          <Text tone="strong" weight="medium">{branch.name} ({branch.code})</Text>
          <Text size="sm" tone="muted">{branch.address}</Text>
        </Stack>
      </div>
    </Card>
  )
}
