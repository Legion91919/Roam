import type { ReactNode } from 'react'
import { cx } from './cx'
import { Icon } from './Icon'

// ---------------------------------------------------------------------------------------------
// File pickers. Both are a <label> around a visually hidden <input type="file">, so they work with
// the keyboard, a screen reader and the phone camera (`capture="environment"`). They hand the
// chosen file to `onFile` and reset the input so the same file can be chosen again.
// ---------------------------------------------------------------------------------------------

interface FileInputProps {
  accept: string
  onFile: (file: File) => void
  disabled?: boolean
  /** Open the back camera directly on phones and tablets. */
  capture?: 'environment' | 'user'
  /** Accessible name of the hidden input ("Front photo"). */
  inputLabel?: string
}

function HiddenInput({ accept, onFile, disabled, capture, inputLabel }: FileInputProps) {
  return (
    <input type="file" accept={accept} capture={capture} disabled={disabled} aria-label={inputLabel} className="sr-only"
      onChange={(e) => { const file = e.target.files?.[0]; if (file) onFile(file); e.target.value = '' }} />
  )
}

/** A dashed 4:3 tile to add one photo (a required angle of the car). */
export function UploadTile({ title, required = false, busy = false, icon = 'add_a_photo', ...input }: FileInputProps & { title: ReactNode; required?: boolean; busy?: boolean; icon?: string }) {
  return (
    <label className={cx('flex aspect-[4/3] cursor-pointer flex-col items-center justify-center gap-2 rounded-card border-2 border-dashed border-line-strong bg-surface-container-low text-center text-body-md text-on-surface-variant hover:border-primary-container',
      input.disabled && 'pointer-events-none opacity-50')}>
      {busy ? <span className="text-primary-container">Uploading…</span> : (
        <>
          <Icon name={icon} size={28} />
          <span>{title}{required && <span className="block text-body-sm text-danger">Required</span>}</span>
        </>
      )}
      <HiddenInput {...input} />
    </label>
  )
}

/** A secondary button that opens the file picker ("Add file"). */
export function FileButton({ children, busy = false, icon = 'upload', ...input }: FileInputProps & { children: ReactNode; busy?: boolean; icon?: string }) {
  return (
    <label className={cx('inline-flex h-10 cursor-pointer items-center gap-2 rounded-card border border-line-strong bg-white px-4 text-body-md text-ink hover:bg-slate-50',
      input.disabled && 'pointer-events-none opacity-50')}>
      <Icon name={icon} size={18} /> {busy ? 'Uploading…' : children}
      <HiddenInput {...input} />
    </label>
  )
}
