import type { ReactNode } from 'react'
import { Icon } from './Icon'

/** Label + control + hint/error, consistently spaced. The control is passed as children. */
export function Field({ label, hint, aside, error, children }: { label: string; hint?: string; aside?: ReactNode; error?: string; children: ReactNode }) {
  return (
    <label className="flex flex-col gap-1.5">
      <span className="flex items-baseline justify-between gap-3">
        <span className="text-label-md text-ink">{label}</span>
        {aside && <span className="text-body-sm text-muted">{aside}</span>}
      </span>
      {children}
      {error ? (
        <span className="flex items-center gap-1.5 text-body-sm text-danger">
          <Icon name="warning" size={16} /> {error}
        </span>
      ) : (
        hint && <span className="text-body-sm text-muted">{hint}</span>
      )}
    </label>
  )
}
