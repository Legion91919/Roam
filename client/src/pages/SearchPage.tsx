import { useEffect } from 'react'
import { useBranches } from '../api/hooks'
import { Page } from '../components/booking/Layout'
import { SearchCard, SearchHero, TrustPoints } from '../components/booking/SearchCard'
import { QueryError } from '../components/ui'
import { useDraft } from '../lib/draft'
import { navigate } from '../lib/router'

/** Screen 1: where and when. */
export function SearchPage() {
  const branches = useBranches()
  const { draft, update, pickupAt, returnAt } = useDraft()

  // Default to the first branch once branches arrive.
  useEffect(() => {
    if (!draft.pickupBranchId && branches.data?.length) update({ pickupBranchId: branches.data[0].id })
  }, [branches.data, draft.pickupBranchId, update])

  const invalidDates = !pickupAt || !returnAt || returnAt <= pickupAt

  return (
    <Page>
      <SearchHero>
        <QueryError query={branches} />
        <SearchCard branches={branches.data} draft={draft} onChange={update} invalidDates={invalidDates} onSearch={() => navigate('/results')} />
      </SearchHero>
      <TrustPoints />
    </Page>
  )
}
