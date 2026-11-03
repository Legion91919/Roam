import type { Quote } from '../../api/types'
import { formatMoney } from '../../lib/format'
import { Callout, Heading, Inline, Money, Skeleton, Stack, Text } from '../ui'

/** Server descriptions use invariant money ("ZAR 450.00"); show them in local notation ("R 450,00"). */
const localise = (text: string) => text.replace(/ZAR (\d+(?:\.\d+)?)/g, (_, n: string) => formatMoney(Number(n)))

/**
 * Screen 3 "Price breakdown". Every line comes from the API: one line per pricing rule that
 * applied (IPriceRule on the server), so the UI never re-implements pricing.
 */
export function PriceBreakdown({ quote, loading, blocked }: { quote: Quote | undefined; loading: boolean; blocked?: string }) {
  if (blocked) return <Callout icon="lock">{blocked}</Callout>
  if (!quote) {
    return (
      <Stack gap={3} aria-busy="true">
        <Skeleton /><Skeleton className="w-4/5" /><Skeleton className="w-3/5" /><Skeleton size="block" />
      </Stack>
    )
  }
  return (
    <Stack gap={5} className={loading ? 'opacity-60 transition-opacity' : 'transition-opacity'}>
      <dl className="space-y-3">
        {quote.lines.map((line) => (
          <div key={line.code} className="flex items-start justify-between gap-4">
            <Text as="dt">{localise(line.description)}</Text>
            <dd className={`tabular shrink-0 font-mono text-mono-code ${line.amount < 0 ? 'rounded bg-accent-surface px-1.5 py-0.5 text-primary-container' : 'text-ink'}`}>
              {line.amount < 0 ? `-${formatMoney(-line.amount)}` : formatMoney(line.amount)}
            </dd>
          </div>
        ))}
      </dl>
      <div className="rounded-card bg-surface-container-low p-4">
        <Inline gap={3} justify="between" align="end">
          <div>
            <Text size="sm" tone="muted">Total for {quote.chargeableDays} day{quote.chargeableDays === 1 ? '' : 's'}</Text>
            <Heading level={3} size="sm">Total price</Heading>
          </div>
          <span data-testid="quote-total"><Money amount={quote.total} size="display" tone="brand" /></span>
        </Inline>
      </div>
    </Stack>
  )
}
