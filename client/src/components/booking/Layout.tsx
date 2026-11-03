import { useEffect, useRef, useState, type ReactNode } from 'react'
import { useAuth } from '../../lib/auth'
import { navigate } from '../../lib/router'
import { Eyebrow, Heading, Icon, Inline, Stack, Text } from '../ui'

// The customer app shell: fixed top bar, footer and the page container. This is the one place
// with fixed positioning and max widths; screens only use <Page> and <PageHeader>.

function AccountMenu({ email, onSignOut }: { email: string; onSignOut: () => void }) {
  const [open, setOpen] = useState(false)
  const ref = useRef<HTMLDivElement>(null)

  useEffect(() => {
    const close = (e: MouseEvent) => { if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false) }
    document.addEventListener('mousedown', close)
    return () => document.removeEventListener('mousedown', close)
  }, [])

  const item = 'flex w-full items-center gap-2 px-4 py-2 text-left text-body-md text-ink hover:bg-surface-container-low'
  return (
    <div ref={ref} className="relative">
      <button type="button" onClick={() => setOpen((o) => !o)} aria-label="Account menu" aria-expanded={open}
        className="flex h-8 w-8 items-center justify-center rounded-full bg-primary text-on-primary">
        <Icon name="person" size={18} />
      </button>
      {open && (
        <div className="absolute right-0 mt-2 w-60 rounded-card border border-line bg-white py-2 shadow-overlay">
          <Text size="sm" tone="muted" truncate className="px-4 pb-2">{email}</Text>
          <a href="#/profile" onClick={() => setOpen(false)} className={item}><Icon name="badge" size={18} /> Driver profile</a>
          <button type="button" onClick={() => { onSignOut(); setOpen(false) }} className={item}><Icon name="logout" size={18} /> Sign out</button>
        </div>
      )}
    </div>
  )
}

/** Fixed 72px top bar shared by every customer screen. */
export function TopBar() {
  const { session, signOut } = useAuth()
  return (
    <header className="fixed inset-x-0 top-0 z-50 border-b border-line bg-white/95 backdrop-blur">
      <div className="mx-auto flex h-18 max-w-[1440px] items-center justify-between gap-6 px-4 sm:px-8">
        <Inline gap={5}>
          <a href="#/" className="flex items-center gap-2 text-primary-container">
            <span className="flex h-8 w-8 items-center justify-center rounded-lg bg-primary text-on-primary"><Icon name="near_me" size={18} filled /></span>
            <span className="text-headline-sm font-semibold tracking-tight">Roam</span>
          </a>
          <span className="hidden items-center gap-2 border-l border-line pl-5 text-label-md text-secondary md:flex">
            <Icon name="flight" size={18} /> Airports & city branches
          </span>
        </Inline>
        <Inline gap={3}>
          {session ? (
            <>
              <a href="#/bookings" className="rounded-lg px-3 py-2 text-label-md text-on-surface-variant hover:bg-surface-container-low hover:text-ink">My bookings</a>
              <AccountMenu email={session.email} onSignOut={() => { signOut(); navigate('/') }} />
            </>
          ) : (
            <a href="#/signin" className="inline-flex h-9 items-center gap-2 rounded-card border border-line-strong px-3 text-label-md text-ink hover:bg-slate-50">
              <Icon name="login" size={18} /> Sign in
            </a>
          )}
        </Inline>
      </div>
    </header>
  )
}

export function Footer() {
  return (
    <footer className="mt-16 border-t border-line bg-white">
      <div className="mx-auto max-w-[1440px] px-4 py-6 sm:px-8">
        <Stack direction="responsive" gap={2} justify="between">
          <Inline gap={2}><span className="h-2 w-2 rounded-full bg-primary" /><Text as="span" size="sm" tone="muted">Johannesburg · Pretoria · Cape Town · Durban · Gqeberha</Text></Inline>
          <Inline gap={4}>
            <Text as="span" size="sm" tone="muted">Roam is a fictional brand · Clean Architecture proof of concept</Text>
            <a href="#/agent/signin" className="inline-flex items-center gap-1 text-body-sm text-secondary hover:text-ink hover:underline"><Icon name="badge" size={16} /> Staff sign-in</a>
          </Inline>
        </Stack>
      </div>
    </footer>
  )
}

const pageWidths = { wide: 'max-w-[1360px]', narrow: 'max-w-[760px]', compact: 'max-w-[480px]' } as const

/** The content area under the top bar. `narrow` for single-column flows (booking details); `compact` for sign-in. */
export function Page({ children, width = 'wide' }: { children: ReactNode; width?: keyof typeof pageWidths }) {
  return (
    <main className="flex-1 pt-18">
      <div className={`mx-auto px-4 py-6 sm:px-8 sm:py-8 ${pageWidths[width]}`}>
        <Stack gap={6}>{children}</Stack>
      </div>
    </main>
  )
}

/** Title block of a screen: eyebrow, h1, a line of explanation and actions on the right. */
export function PageHeader({ eyebrow, title, subtitle, actions }: { eyebrow?: ReactNode; title: ReactNode; subtitle?: ReactNode; actions?: ReactNode }) {
  return (
    <Stack direction="responsive" gap={4} justify="between" align={{ base: 'start', sm: 'end' }}>
      <Stack gap={1}>
        {eyebrow && <Eyebrow>{eyebrow}</Eyebrow>}
        <Heading level={1} size="lg">{title}</Heading>
        {subtitle && <Text size="lg" tone="muted">{subtitle}</Text>}
      </Stack>
      {actions}
    </Stack>
  )
}
