import type { DescribedError } from '../../api/client'
import { Button } from './Button'
import { Icon } from './Icon'

const icons: Record<string, string> = {
  'inventory.sold_out': 'event_busy',
  'concurrency.conflict': 'directions_car',
  network: 'cloud_off',
}

/**
 * Error banner from the Stitch states board. Shows the API's machine-readable code as a chip,
 * which is handy in a demo: you can see exactly which backend rule fired.
 */
export function Banner({ error, onRetry, onDismiss }: { error: DescribedError; onRetry?: () => void; onDismiss?: () => void }) {
  return (
    <div role="alert" className="flex items-start gap-3 rounded-card border border-danger/20 bg-danger-surface p-4">
      <div className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-white/70 text-danger">
        <Icon name={icons[error.code] ?? 'error'} size={18} />
      </div>
      <div className="min-w-0 flex-1">
        <div className="flex flex-wrap items-center gap-2">
          <p className="font-medium text-danger">{error.title}</p>
          <code className="rounded bg-white/70 px-1.5 py-0.5 font-mono text-label-sm text-danger">{error.code}</code>
        </div>
        <p className="mt-0.5 text-body-md text-on-surface-variant">{error.message}</p>
      </div>
      {error.retryable && onRetry && (
        <Button variant="secondary" size="sm" icon="refresh" onClick={onRetry}>
          Retry
        </Button>
      )}
      {onDismiss && (
        <button type="button" onClick={onDismiss} aria-label="Dismiss" className="text-muted hover:text-ink">
          <Icon name="close" size={18} />
        </button>
      )}
    </div>
  )
}
