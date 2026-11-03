import { useEffect, useState } from 'react'
import { ApiError, describeError } from '../api/client'
import { useBranches, useCreateReservation, useProfile, useQuote, useTerms, useTokenizeCard } from '../api/hooks'
import type { CardDetails, PaymentOption } from '../api/types'
import { ExtrasList } from '../components/booking/ExtrasList'
import { Page } from '../components/booking/Layout'
import { PaymentCard } from '../components/booking/PaymentCard'
import { PriceBreakdown } from '../components/booking/PriceBreakdown'
import { BranchCard, CarSummary, DriverCard } from '../components/booking/ReviewParts'
import { TermsDialog } from '../components/booking/TermsDialog'
import { TripCard } from '../components/booking/TripCard'
import { BackLink, Banner, Button, Card, CardHeader, CheckboxCard, Inline, Skeleton, SplitLayout, Stack, Text, TextLink } from '../components/ui'
import { useAuth } from '../lib/auth'
import { validateCard } from '../lib/card'
import { useDraft } from '../lib/draft'
import { chargeableDays, formatMoney } from '../lib/format'
import { navigate, withNext } from '../lib/router'
import { ageOn } from '../lib/saId'

const EMPTY_CARD: CardDetails = { number: '', expiry: '', cvc: '' }

/**
 * Screen 3: review and book. Who drives comes from the VERIFIED profile (the server prices with it,
 * so a client can't claim a loyalty tier or a different age). The customer picks extras, reads and
 * accepts the current terms, and books.
 */
export function ReviewPage() {
  const { session } = useAuth()
  const { draft, update, trip, pickupAt, returnAt, returnBranchId } = useDraft()
  const branches = useBranches()
  const profile = useProfile(Boolean(session))
  const terms = useTerms()
  const quote = useQuote(session && profile.data ? trip : null)
  const book = useCreateReservation()
  const tokenize = useTokenizeCard()
  const [card, setCard] = useState<CardDetails>(EMPTY_CARD)
  const [paymentOption, setPaymentOption] = useState<PaymentOption>('Card')
  const [accepted, setAccepted] = useState(false)
  const [showTerms, setShowTerms] = useState(false)

  // Guards: trip -> signed in -> profile, each sending the user back here afterwards.
  useEffect(() => {
    if (!trip) navigate(draft.pickupBranchId ? '/results' : '/')
    else if (!session) navigate(withNext('/signin', '/review'))
    else if (profile.data === null) navigate(withNext('/profile', '/review'))
  }, [trip, session, profile.data, draft.pickupBranchId])

  if (!trip || !draft.category || !session) return null
  if (profile.isLoading || !profile.data) {
    return <Page><SplitLayout aside={<Skeleton size="page" />}><Skeleton size="page" /></SplitLayout></Page>
  }

  const days = chargeableDays(pickupAt, returnAt)
  const branch = branches.data?.find((b) => b.id === draft.pickupBranchId)
  const dropOff = branches.data?.find((b) => b.id === returnBranchId)
  const eligibility = quote.error instanceof ApiError && quote.error.code.startsWith('eligibility.') ? describeError(quote.error) : null
  const quoteError = quote.isError && !eligibility ? describeError(quote.error) : null

  const cardErrors = validateCard(card)
  const payAtBranch = paymentOption === 'PayAtBranch'
  const cardValid = payAtBranch || Object.keys(cardErrors).length === 0

  // Card: 1. card -> token (the only request that sees the card), 2. clear the card from state, 3. book with the token.
  // Pay at the branch: book straight away, with no card data at all.
  const submit = async () => {
    book.reset()
    const acceptedTermsVersion = accepted ? terms.data?.version ?? null : null
    const done = { onSuccess: (result: { reservationId: string }) => navigate(`/reservations/${result.reservationId}`) }
    if (payAtBranch) {
      setCard(EMPTY_CARD)
      book.mutate({ ...trip, acceptedTermsVersion, paymentOption, paymentToken: null }, done)
      return
    }
    let paymentToken: string
    try {
      paymentToken = (await tokenize.mutateAsync(card)).token
    } catch {
      return // shown by the tokenize error banner
    } finally {
      tokenize.reset()
    }
    setCard(EMPTY_CARD)
    book.mutate({ ...trip, acceptedTermsVersion, paymentOption, paymentToken }, done)
  }

  const summary = (
    <>
      <Card gap={5}>
        <CardHeader eyebrow="Summary & rates" title="Price breakdown" subtitle={`${days}-day rental · ${branch?.name ?? '…'}`} />
        <CarSummary category={draft.category} />
        {quoteError
          ? <Banner error={quoteError} onRetry={() => quote.refetch()} />
          : <PriceBreakdown quote={quote.data} loading={quote.isFetching} blocked={eligibility ? 'This driver is not eligible for this vehicle.' : undefined} />}

        <CheckboxCard checked={accepted} onChange={setAccepted}>
          I have read and accept the{' '}
          <TextLink onClick={(e) => { e.preventDefault(); setShowTerms(true) }}>rental terms and conditions</TextLink>
          {terms.data && <Text as="span" size="sm" tone="muted"> (version {terms.data.version})</Text>}.
        </CheckboxCard>

        {tokenize.isError && <Banner error={describeError(tokenize.error)} onDismiss={() => tokenize.reset()} />}
        {book.isError && (
          <Stack gap={2}>
            <Banner error={describeError(book.error)} onDismiss={() => book.reset()} />
            {book.error instanceof ApiError && book.error.status === 409 && (
              <Inline><Button variant="ghost" icon="search" onClick={() => navigate('/results')}>Search again</Button></Inline>
            )}
          </Stack>
        )}

        <Stack gap={3}>
          <Button block size="lg" icon={payAtBranch ? 'event_available' : 'lock'} loading={tokenize.isPending || book.isPending}
            loadingText={tokenize.isPending ? 'Securing card…' : payAtBranch ? 'Booking…' : 'Authorising…'}
            disabled={!quote.data || quote.isError || !accepted || !terms.data || !cardValid} onClick={submit}>
            {payAtBranch ? 'Book now, pay at the branch' : quote.data ? `Authorise ${formatMoney(quote.data.total)} and book` : 'Authorise and book'}
          </Button>
          {!cardValid && quote.data && <Text size="sm" tone="muted" align="center">Enter your card details to book.</Text>}
          <Text tone="brand" icon="check_circle" align="center">Free cancellation until pickup time</Text>
        </Stack>
      </Card>
      {branch && <BranchCard branch={branch} />}
    </>
  )

  return (
    <Page>
      <BackLink onClick={() => navigate('/results')}>Back to vehicles</BackLink>

      <SplitLayout sticky aside={summary}>
        <DriverCard driver={profile.data} age={ageOn(profile.data.dateOfBirth, new Date(pickupAt))} editHref={`#${withNext('/profile', '/review')}`} eligibility={eligibility} />

        <TripCard draft={draft} category={draft.category} pickup={branch} dropOff={dropOff}
          onApply={(dates) => update(dates)}
          onChooseAnotherCar={(dates) => { update({ ...dates, category: null }); navigate('/results') }} />

        <Card padding="lg" gap={5}>
          <CardHeader title="Extras" subtitle="Prepared inside the car before you arrive. Never discounted, always capped."
            actions={<Text as="span" size="label" tone="muted">Optional</Text>} />
          <ExtrasList value={{ gps: draft.gps, childSeat: draft.childSeat }} onChange={update} />
        </Card>

        <PaymentCard option={paymentOption} onOptionChange={setPaymentOption} value={card} onChange={setCard} errors={cardErrors} amount={quote.data?.total} />
      </SplitLayout>

      <TermsDialog terms={terms.data} open={showTerms} onClose={() => setShowTerms(false)} onAccept={() => setAccepted(true)} />
    </Page>
  )
}
