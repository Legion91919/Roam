import { cx } from './cx'
import { Icon } from './Icon'

/**
 * Step tabs for a multi-step flow (the inspection: Checklist → Fuel & odometer → Photos → Review).
 * Done steps show a tick; on phones only the numbers show.
 */
export function Steps({ steps, current, isDone, onSelect, disabled = false, label }: {
  steps: string[]
  current: number
  isDone: (index: number) => boolean
  onSelect: (index: number) => void
  disabled?: boolean
  label: string
}) {
  return (
    <ol className="flex border-b border-line" aria-label={label}>
      {steps.map((step, i) => {
        const done = isDone(i)
        const active = i === current
        return (
          <li key={step} className="flex-1">
            <button type="button" disabled={disabled} onClick={() => onSelect(i)} aria-current={active ? 'step' : undefined}
              className={cx('flex w-full items-center justify-center gap-2 border-b-2 px-2 py-3 text-body-sm sm:text-body-md',
                active ? 'border-primary-container font-medium text-primary-container' : 'border-transparent text-secondary hover:text-ink')}>
              <span className={cx('flex h-6 w-6 items-center justify-center rounded-full text-label-sm',
                done ? 'bg-primary-container text-on-primary' : active ? 'bg-accent-surface text-primary-container' : 'bg-surface-container text-secondary')}>
                {done ? <Icon name="check" size={14} /> : i + 1}
              </span>
              <span className="hidden sm:inline">{step}</span>
            </button>
          </li>
        )
      })}
    </ol>
  )
}
