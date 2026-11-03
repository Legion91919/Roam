import { useEffect, useMemo, useState } from 'react'
import { useAvailability, useBranches } from '../api/hooks'
import { CategoryCard, CategoryCardSkeleton } from '../components/booking/CategoryCard'
import { Page, PageHeader } from '../components/booking/Layout'
import { FilterChips, NoResults, SearchSummary, SelectionBar, type Filter } from '../components/booking/ResultsParts'
import { Button, Callout, Grid, QueryError, Spacer } from '../components/ui'
import { CATEGORY_INFO, CATEGORY_LABEL } from '../lib/catalog'
import { useDraft } from '../lib/draft'
import { chargeableDays, formatMoney } from '../lib/format'
import { navigate } from '../lib/router'

const CARD_COLUMNS = { base: 1, sm: 2, xl: 4 } as const

/** Screen 2: what's available for the whole period (query side: GET /api/availability). */
export function ResultsPage() {
  const branches = useBranches()
  const { draft, update, pickupAt, returnAt, returnBranchId } = useDraft()
  const availability = useAvailability(draft.pickupBranchId, pickupAt, returnAt)
  const [filter, setFilter] = useState<Filter>('all')

  const days = chargeableDays(pickupAt, returnAt)
  const from = branches.data?.find((b) => b.id === draft.pickupBranchId)
  const to = branches.data?.find((b) => b.id === returnBranchId)

  const visible = useMemo(
    () =>
      (availability.data ?? []).filter((c) =>
        filter === 'automatic' ? CATEGORY_INFO[c.category].transmission === 'Automatic' : filter === 'budget' ? c.dailyRate < 500 : true,
      ),
    [availability.data, filter],
  )
  const selected = availability.data?.find((c) => c.category === draft.category && c.availableCars > 0)
  const nearby = (branches.data ?? []).filter((b) => b.id !== draft.pickupBranchId && (b.city === from?.city || b.type === 'Airport')).slice(0, 3)

  // Deep link without a search (e.g. refresh after clearing storage): start from the search screen.
  useEffect(() => {
    if (!draft.pickupBranchId) navigate('/')
  }, [draft.pickupBranchId])
  if (!draft.pickupBranchId) return null

  return (
    <Page>
      <SearchSummary from={from} to={to} pickupAt={pickupAt} returnAt={returnAt} days={days} onEdit={() => navigate('/')} />

      <PageHeader
        title={`Available fleet at ${from?.code ?? '…'} hub`}
        subtitle="Live availability for every day of your trip."
        actions={availability.data && availability.data.length > 0 && <FilterChips value={filter} onChange={setFilter} total={availability.data.length} />}
      />

      <QueryError query={availability} />

      {availability.isLoading ? (
        <Grid columns={CARD_COLUMNS} gap={5} aria-label="Loading vehicles">
          {Array.from({ length: 4 }, (_, i) => <CategoryCardSkeleton key={i} />)}
        </Grid>
      ) : availability.data?.length === 0 ? (
        <NoResults branches={nearby} currentBranchId={draft.pickupBranchId} onModify={() => navigate('/')} onTryBranch={(id) => update({ pickupBranchId: id, category: null })} />
      ) : (
        <Grid columns={CARD_COLUMNS} gap={5}>
          {visible.map((c) => (
            <CategoryCard key={c.category} availability={c} days={days} selected={c.category === draft.category} onSelect={() => update({ category: c.category })} />
          ))}
        </Grid>
      )}

      <Callout title="Rental policy">
        Drivers must be 21 or older with a licence held for at least one year. Luxury vehicles require a driver aged 25 or older. Drivers under 25 pay R 150,00 per day.
      </Callout>

      {/* Room for the fixed selection bar so it never covers the policy note. */}
      {selected && <Spacer />}

      {selected && (
        <SelectionBar
          title={`${CATEGORY_LABEL[selected.category]} · ${CATEGORY_INFO[selected.category].example}`}
          subtitle={`${from?.name ?? ''} · from ${formatMoney(selected.dailyRate * days)} for ${days} day${days === 1 ? '' : 's'}`}
          step="Step 2 of 4 · Vehicle selected"
          action={<Button trailingIcon="arrow_forward" shortLabel="Continue" onClick={() => navigate('/review')}>Continue to review</Button>}
        />
      )}
    </Page>
  )
}
