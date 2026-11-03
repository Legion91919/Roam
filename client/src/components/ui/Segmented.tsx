import { cx } from './cx'
import { Icon } from './Icon'

export type SegmentTone = 'brand' | 'danger' | 'warning' | 'neutral'

const selectedTones: Record<SegmentTone, string> = {
  brand: 'bg-primary text-on-primary',
  danger: 'bg-danger text-white',
  warning: 'bg-warning text-white',
  neutral: 'bg-inverse-surface text-inverse-on-surface',
}

/**
 * Segmented control (role="radiogroup"): one of a few short options side by side. `toneOf` colours
 * the selected option by meaning (OK green, Damaged red) for the inspection checklist; `size="sm"`
 * is the compact row version without the tick.
 */
export function Segmented<T extends string>({ options, value, onChange, label, labelOf, toneOf, size = 'md', disabled = false }: {
  options: readonly T[]
  value: T
  onChange: (value: T) => void
  label: string
  /** Display text for an option (defaults to the value). */
  labelOf?: (option: T) => string
  toneOf?: (option: T) => SegmentTone
  size?: 'md' | 'sm'
  disabled?: boolean
}) {
  return (
    <div role="radiogroup" aria-label={label} className="grid gap-1 rounded-card bg-surface-container-low p-1" style={{ gridTemplateColumns: `repeat(${options.length}, minmax(0, 1fr))` }}>
      {options.map((option) => {
        const selected = option === value
        return (
          <button
            key={option}
            type="button"
            role="radio"
            aria-checked={selected}
            disabled={disabled}
            onClick={() => onChange(option)}
            className={cx(
              'flex items-center justify-center gap-1.5 rounded-lg transition-colors',
              size === 'sm' ? 'h-9 text-body-sm' : 'h-10 text-body-md',
              selected ? cx(selectedTones[toneOf?.(option) ?? 'brand'], 'font-medium') : 'text-on-surface-variant hover:bg-surface-container',
            )}
          >
            {selected && size === 'md' && <Icon name="verified" size={16} />}
            {labelOf ? labelOf(option) : option}
          </button>
        )
      })}
    </div>
  )
}
