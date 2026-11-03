import { forwardRef, type InputHTMLAttributes, type ReactNode, type SelectHTMLAttributes, type TextareaHTMLAttributes } from 'react'
import { cx } from './cx'
import { Icon } from './Icon'
import { Toggle } from './Toggle'

// ---------------------------------------------------------------------------------------------
// Form controls. Wrap them in <Field label hint error> for the label and messages. All native
// attributes (type, required, autoComplete, inputMode, min, maxLength, aria-*) pass through.
// ---------------------------------------------------------------------------------------------

interface ControlStyle {
  /** Red border and tint (pair with Field's `error`). */
  invalid?: boolean
  /** sm: 40px high (toolbars, filters, table rows); md: 44px (forms). */
  controlSize?: 'md' | 'sm'
  /** Monospace (plates, VINs, references, card numbers). */
  mono?: boolean
  /** Shows what is typed in capitals (plates, passport numbers, country codes). */
  uppercase?: boolean
}

const controlClass = ({ invalid, controlSize = 'md', mono, uppercase }: ControlStyle) =>
  cx('input disabled:bg-surface-container-low disabled:text-on-surface-variant', controlSize === 'sm' && '!h-10 !text-body-md', mono && 'font-mono tracking-wider', uppercase && 'uppercase', invalid && 'input-error')

export const Input = forwardRef<HTMLInputElement, InputHTMLAttributes<HTMLInputElement> & ControlStyle & { leadingIcon?: string; /** As wide as its content (a date in a toolbar), not the full row. */ fit?: boolean }>(
  function Input({ invalid, controlSize, mono, uppercase, leadingIcon, fit, className, ...rest }, ref) {
    const input = <input ref={ref} {...rest} className={cx(controlClass({ invalid, controlSize, mono, uppercase }), leadingIcon && 'pl-11', fit && '!w-auto', className)} />
    if (!leadingIcon) return input
    return (
      <span className={fit ? 'relative inline-block' : 'relative block'}>
        <Icon name={leadingIcon} className="pointer-events-none absolute left-3.5 top-1/2 -translate-y-1/2 text-secondary" />
        {input}
      </span>
    )
  },
)

export interface SelectOption { value: string; label: string; disabled?: boolean }

/** Native <select> (accessible, works with the phone's own picker). Give `options` or <option> children. */
export const Select = forwardRef<HTMLSelectElement, SelectHTMLAttributes<HTMLSelectElement> & ControlStyle & { options?: SelectOption[]; placeholder?: string; /** As wide as its longest option, not the full row. */ fit?: boolean }>(
  function Select({ invalid, controlSize, mono, uppercase: _uppercase, options, placeholder, fit, className, children, ...rest }, ref) {
    return (
      <select ref={ref} {...rest} className={cx(controlClass({ invalid, controlSize, mono }), fit && '!w-auto', className)}>
        {placeholder !== undefined && <option value="">{placeholder}</option>}
        {options?.map((o) => <option key={o.value} value={o.value} disabled={o.disabled}>{o.label}</option>)}
        {children}
      </select>
    )
  },
)

export const Textarea = forwardRef<HTMLTextAreaElement, TextareaHTMLAttributes<HTMLTextAreaElement> & Pick<ControlStyle, 'invalid'>>(
  function Textarea({ invalid, className, rows = 4, ...rest }, ref) {
    return <textarea ref={ref} rows={rows} {...rest} className={cx('input !h-auto py-2', invalid && 'input-error', className)} />
  },
)

/** A checkbox in a bordered box that highlights when ticked (accepting the terms). */
export function CheckboxCard({ checked, onChange, children }: { checked: boolean; onChange: (checked: boolean) => void; children: ReactNode }) {
  return (
    <label className="flex cursor-pointer items-start gap-3 rounded-card border border-line p-4 text-body-md text-ink has-[:checked]:border-primary-container has-[:checked]:bg-accent-surface/40">
      <input type="checkbox" className="mt-0.5 h-5 w-5 shrink-0 accent-[#005145]" checked={checked} onChange={(e) => onChange(e.target.checked)} />
      <span>{children}</span>
    </label>
  )
}

/** A switch with its label next to it ("Show free upgrades"). */
export function ToggleField({ checked, onChange, label }: { checked: boolean; onChange: (checked: boolean) => void; label: string }) {
  return (
    <span className="flex items-center gap-3 text-body-md text-ink">
      <Toggle checked={checked} onChange={onChange} label={label} />
      {label}
    </span>
  )
}

export interface RadioCardOption<T extends string> {
  value: T
  /** Simple option: icon + title + description. */
  title?: ReactNode
  description?: ReactNode
  icon?: string
  /** Rich option: any content (a car with its plate and odometer). Replaces title/description. */
  content?: ReactNode
  disabled?: boolean
}

const radioCols = { 1: '', 2: 'sm:grid-cols-2', 3: 'sm:grid-cols-2 xl:grid-cols-3' } as const

/**
 * Choose one of a few options shown as cards (role="radiogroup"): how to pay, which car to hand
 * over. The selected card gets the brand border and tint.
 */
export function RadioCards<T extends string>({ label, options, value, onChange, columns = 2 }: {
  label: string
  options: RadioCardOption<T>[]
  value: T | null
  onChange: (value: T) => void
  columns?: keyof typeof radioCols
}) {
  return (
    <div role="radiogroup" aria-label={label} className={cx('grid gap-3', radioCols[columns])}>
      {options.map((o) => {
        const selected = o.value === value
        return (
          <button key={o.value} type="button" role="radio" aria-checked={selected} disabled={o.disabled} onClick={() => onChange(o.value)}
            className={cx('flex items-start gap-3 rounded-card border p-4 text-left transition-colors disabled:opacity-50',
              selected ? 'border-[1.5px] border-primary-container bg-accent-surface/40' : 'border-line bg-white hover:border-line-strong')}>
            {o.content ?? (
              <>
                <Icon name={selected ? 'radio_button_checked' : 'radio_button_unchecked'} size={20} className={selected ? 'text-primary-container' : 'text-secondary'} />
                <span className="min-w-0">
                  <span className="flex items-center gap-2 font-medium text-ink">{o.icon && <Icon name={o.icon} size={18} />} {o.title}</span>
                  {o.description && <span className="mt-0.5 block text-body-sm text-secondary">{o.description}</span>}
                </span>
              </>
            )}
          </button>
        )
      })}
    </div>
  )
}

/** Buttons at the end of a form or dialog step. */
export function FormActions({ children, align = 'end' }: { children: ReactNode; align?: 'end' | 'between' }) {
  return <div className={cx('flex flex-wrap gap-3', align === 'end' ? 'justify-end' : 'justify-between')}>{children}</div>
}

/** Filter chips: one of a few values, shown as pill buttons (aria-pressed). */
export function ChipGroup<T extends string>({ label, options, value, onChange }: {
  label: string
  options: { value: T; label: string }[]
  value: T
  onChange: (value: T) => void
}) {
  return (
    <div className="flex flex-wrap gap-2" role="group" aria-label={label}>
      {options.map((o) => (
        <button key={o.value} type="button" aria-pressed={value === o.value} onClick={() => onChange(o.value)}
          className={cx('h-9 rounded-full border px-4 text-body-md transition-colors',
            value === o.value ? 'border-primary bg-primary text-on-primary' : 'border-line bg-white text-ink hover:border-line-strong')}>
          {o.label}
        </button>
      ))}
    </div>
  )
}

/** A date or time input in a tinted tile with an uppercase label (the search card's trip dates). */
export function TileInput({ label, icon, ...rest }: InputHTMLAttributes<HTMLInputElement> & { label: string; icon: string }) {
  return (
    <label className="flex flex-col gap-1 rounded-card bg-surface-container-low px-3 py-2.5 focus-within:ring-1 focus-within:ring-primary-container">
      <span className="text-label-sm uppercase tracking-wider text-secondary">{label}</span>
      <span className="flex items-center gap-2">
        <Icon name={icon} size={18} className="text-ink" />
        <input {...rest} className="w-full bg-transparent text-body-lg font-medium text-ink outline-none" />
      </span>
    </label>
  )
}
