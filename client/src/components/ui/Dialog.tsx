import { useEffect, useRef, type ReactNode } from 'react'
import { cx } from './cx'
import { Icon } from './Icon'

const widths = { md: 'w-[min(560px,calc(100vw-32px))]', lg: 'w-[min(720px,calc(100vw-32px))]', xl: 'w-[min(860px,calc(100vw-32px))]' } as const

/**
 * Native <dialog>: focus trapping, Escape to close and the backdrop come from the browser.
 * Long content scrolls inside the body; the header and the footer actions stay in view.
 */
export function Dialog({ open, onClose, title, eyebrow, children, footer, size = 'md' }: {
  open: boolean
  onClose: () => void
  title: string
  eyebrow?: ReactNode
  children: ReactNode
  footer?: ReactNode
  size?: keyof typeof widths
}) {
  const ref = useRef<HTMLDialogElement>(null)
  useEffect(() => {
    const dialog = ref.current
    if (!dialog) return
    if (open && !dialog.open) dialog.showModal()
    if (!open && dialog.open) dialog.close()
  }, [open])

  return (
    <dialog ref={ref} onClose={onClose} aria-labelledby="dialog-title" className={cx(widths[size], 'rounded-card border border-line p-0 shadow-overlay backdrop:bg-ink/40')}>
      <div className="flex items-start justify-between gap-4 border-b border-line px-6 py-4">
        <div>
          {eyebrow && <p className="text-label-sm uppercase tracking-wider text-secondary">{eyebrow}</p>}
          <h2 id="dialog-title" className={eyebrow ? 'text-headline-md text-ink' : 'text-headline-sm text-ink'}>{title}</h2>
        </div>
        <button type="button" onClick={onClose} aria-label="Close" className="text-muted hover:text-ink"><Icon name="close" /></button>
      </div>
      <div className="max-h-[70vh] overflow-auto px-6 py-5">{children}</div>
      {footer && <div className="flex flex-wrap justify-end gap-3 border-t border-line px-6 py-4">{footer}</div>}
    </dialog>
  )
}
