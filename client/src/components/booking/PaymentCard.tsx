import { useEffect, useState } from 'react'
import type { CardDetails, PaymentOption, PaymentSummary } from '../../api/types'
import { cardBrand, cardDigits, formatCardNumber, formatExpiry, TEST_CARDS, type CardFormErrors } from '../../lib/card'
import { Callout, Card, CardHeader, Field, Grid, GridItem, Icon, Inline, Input, Money, Pill, RadioCards, Stack, Text, TextLink } from '../ui'

/**
 * Card entry (mock "hosted fields"): number, expiry, CVC and the sandbox test cards. Used by the
 * customer on the review page and by the agent at the counter. The parent keeps the values only until
 * it has a token, then clears them; they are sent to POST /api/payments/tokenize and nowhere else.
 */
export function CardFields({ value, onChange, errors }: { value: CardDetails; onChange: (card: CardDetails) => void; errors: CardFormErrors }) {
  const [touched, setTouched] = useState({ number: false, expiry: false, cvc: false })
  const digits = cardDigits(value.number)
  const show = (field: keyof CardFormErrors) => (touched[field] ? errors[field] : undefined)
  const touch = (field: keyof typeof touched) => () => setTouched((t) => ({ ...t, [field]: true }))

  // Cleared by the page after tokenising (or after a decline): start fresh, without red errors.
  const empty = !value.number && !value.expiry && !value.cvc
  useEffect(() => {
    if (empty) setTouched({ number: false, expiry: false, cvc: false })
  }, [empty])

  return (
    <Stack gap={5}>
      <Grid columns={{ base: 1, sm: 2 }}>
        <GridItem>
          <Field label="Card number" aside={digits.length >= 2 ? cardBrand(digits) : undefined} error={show('number')}>
            <Input mono invalid={Boolean(show('number'))} inputMode="numeric" autoComplete="cc-number" placeholder="4242 4242 4242 4242"
              value={formatCardNumber(value.number)} onChange={(e) => onChange({ ...value, number: cardDigits(e.target.value) })} onBlur={touch('number')} />
          </Field>
        </GridItem>
        <Field label="Expiry" error={show('expiry')}>
          <Input mono invalid={Boolean(show('expiry'))} inputMode="numeric" autoComplete="cc-exp" placeholder="MM/YY"
            value={value.expiry} onChange={(e) => onChange({ ...value, expiry: formatExpiry(e.target.value) })} onBlur={touch('expiry')} />
        </Field>
        <Field label="CVC" error={show('cvc')}>
          <Input mono invalid={Boolean(show('cvc'))} type="password" inputMode="numeric" autoComplete="cc-csc" placeholder="123" maxLength={4}
            value={value.cvc} onChange={(e) => onChange({ ...value, cvc: e.target.value.replace(/\D/g, '') })} onBlur={touch('cvc')} />
        </Field>
      </Grid>

      <details className="rounded-card bg-surface-container-low p-4">
        <summary className="cursor-pointer font-medium text-ink">Test cards (sandbox: no real money moves)</summary>
        <Stack as="ul" gap={2} className="mt-3">
          {TEST_CARDS.map((card) => (
            <li key={card.number}>
              <Inline gap={2} justify="between">
                <TextLink onClick={() => onChange({ number: cardDigits(card.number), expiry: '12/34', cvc: '123' })}><span className="font-mono text-mono-code">{card.number}</span></TextLink>
                <Text as="span" size="sm">{card.label}</Text>
              </Inline>
            </li>
          ))}
        </Stack>
        <Text size="sm" className="mt-3">Any future expiry and any 3-digit CVC.</Text>
      </details>
    </Stack>
  )
}

/**
 * Payment section of the review page: card now (hold on the card) or pay at the branch (the agent
 * takes the card at pickup). Either way the price is the same and the car is reserved.
 */
export function PaymentCard({ option, onOptionChange, value, onChange, errors, amount }: {
  option: PaymentOption
  onOptionChange: (option: PaymentOption) => void
  value: CardDetails
  onChange: (card: CardDetails) => void
  errors: CardFormErrors
  amount?: number
}) {
  const total = amount !== undefined ? <Money amount={amount} weight="medium" /> : 'the total'
  return (
    <Card padding="lg" gap={5}>
      <CardHeader title="Payment" subtitle="Same price either way. Free cancellation until pickup."
        actions={<Pill tone="neutral"><Icon name="lock" size={14} /> Card details go to the payment gateway only</Pill>} />

      <RadioCards<PaymentOption> label="How do you want to pay?" value={option} onChange={onOptionChange} options={[
        { value: 'Card', icon: 'credit_card', title: 'Pay by card now', description: 'The total is held on your card and taken when you return the car.' },
        { value: 'PayAtBranch', icon: 'storefront', title: 'Pay at the branch', description: 'Nothing is taken now. Bring your card to the counter at pickup.' },
      ]} />

      {option === 'Card' ? (
        <Stack gap={4}>
          <Text tone="muted">We hold {total} on your card now and take it when you return the car. Cancel before pickup and the hold is released.</Text>
          <CardFields value={value} onChange={onChange} errors={errors} />
        </Stack>
      ) : (
        <Callout icon="storefront" data-testid="pay-at-branch-note">
          At pickup the agent holds {total} on your card (debit or credit, in the main driver's name) before handing
          over the keys, and takes it when you return the car. No card details are needed now.
        </Callout>
      )}
    </Card>
  )
}

/** "Pay at the branch" in place of a card line, before the counter payment is taken. */
export function PayAtBranchLine({ size = 'md' }: { size?: 'md' | 'sm' }) {
  return (
    <Inline gap={2}>
      <Text as="span" size={size} icon="storefront">Pay at the branch</Text>
      <Pill tone="warning">Not paid yet</Pill>
    </Inline>
  )
}

const statusText: Record<PaymentSummary['status'], string> = {
  Pending: 'Pending',
  Authorized: 'Authorised',
  Captured: 'Captured',
  Voided: 'Released',
  Failed: 'Failed',
}

/** "Card •••• 4242 · Authorised" line for confirmation pages, booking lists and the staff app. */
export function PaymentLine({ payment, size = 'md' }: { payment: PaymentSummary; size?: 'md' | 'sm' }) {
  const tone = payment.status === 'Failed' ? 'danger' : payment.status === 'Voided' ? 'neutral' : 'success'
  const amount = payment.status === 'Captured' ? payment.captured : payment.authorized
  return (
    <Inline gap={2}>
      <Text as="span" size={size} icon="credit_card">{payment.cardBrand} <span className="font-mono">•••• {payment.cardLast4}</span></Text>
      <Pill tone={tone}>{statusText[payment.status]}</Pill>
      {payment.status !== 'Voided' && payment.status !== 'Failed' && <Money amount={amount} size={size} tone="default" />}
    </Inline>
  )
}

