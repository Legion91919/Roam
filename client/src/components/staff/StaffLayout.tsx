import { useEffect, type ReactNode } from 'react'
import { useBranches } from '../../api/hooks'
import { useAuth } from '../../lib/auth'
import { navigate, type StaffPage } from '../../lib/router'
import { useStaffBranch } from '../../lib/staffPrefs'
import { Heading, Icon, Select, Skeleton, Stack, Text } from '../ui'

const NAV: { page: StaffPage; href: string; label: string; icon: string }[] = [
  { page: 'today', href: '#/agent', label: 'Today', icon: 'today' },
  { page: 'checkout', href: '#/agent/checkout', label: 'Check out', icon: 'key' },
  { page: 'returns', href: '#/agent/returns', label: 'Returns', icon: 'assignment_return' },
  { page: 'find', href: '#/agent/find', label: 'Find booking', icon: 'search' },
  { page: 'fleet', href: '#/agent/fleet', label: 'Fleet', icon: 'directions_car' },
  { page: 'customer', href: '#/agent/customers/new', label: 'Add customer', icon: 'person_add' },
]

/** "Roam Staff" wordmark: deliberately different from the customer "Roam" logo. */
export function StaffWordmark() {
  return (
    <span className="flex items-center gap-2">
      <span className="flex h-8 w-8 items-center justify-center rounded-lg bg-primary-fixed text-primary"><Icon name="badge" size={18} filled /></span>
      <span className="text-headline-sm font-semibold tracking-tight">Roam <span className="font-mono text-label-md uppercase tracking-[0.18em] text-primary-fixed-dim">Staff</span></span>
    </span>
  )
}

/**
 * Staff shell: dark (inverse-surface) sidebar, branch selector on top, no marketing, no customer
 * booking links. The customer TopBar never links here; this shell never links to customer booking.
 */
export function StaffLayout({ page, title, subtitle, actions, children }: { page: StaffPage; title: string; subtitle?: string; actions?: ReactNode; children: ReactNode }) {
  const { session, signOut } = useAuth()
  return (
    <div className="flex min-h-screen flex-col bg-surface-container-low lg:flex-row">
      <aside className="flex shrink-0 flex-col bg-inverse-surface text-inverse-on-surface lg:sticky lg:top-0 lg:h-screen lg:w-64">
        <div className="flex items-center justify-between px-5 py-5"><StaffWordmark /></div>
        <nav aria-label="Staff" className="flex gap-1 overflow-x-auto px-3 pb-3 lg:flex-col lg:overflow-visible">
          {NAV.map((item) => {
            const active = item.page === page || (item.page === 'fleet' && (page === 'vehicle' || page === 'vehicleNew'))
            return (
              <a
                key={item.page}
                href={item.href}
                aria-current={active ? 'page' : undefined}
                className={`flex shrink-0 items-center gap-3 rounded-card px-3 py-2.5 text-body-md transition-colors ${active ? 'bg-primary-fixed text-primary font-medium' : 'text-inverse-on-surface/80 hover:bg-white/10 hover:text-inverse-on-surface'}`}
              >
                <Icon name={item.icon} size={20} filled={active} /> {item.label}
              </a>
            )
          })}
        </nav>
        <div className="mt-auto hidden border-t border-white/10 px-5 py-4 lg:block">
          <Text size="sm" tone="inverse" truncate className="opacity-70">{session?.email}</Text>
          <button type="button" onClick={() => { signOut(); navigate('/agent/signin') }} className="mt-2 flex items-center gap-2 text-body-md text-inverse-on-surface hover:underline">
            <Icon name="logout" size={18} /> Sign out
          </button>
        </div>
      </aside>

      <div className="min-w-0 flex-1">
        <header className="flex flex-wrap items-center justify-between gap-4 border-b border-line bg-white px-4 py-4 sm:px-8">
          <BranchSelector />
          <button type="button" onClick={() => { signOut(); navigate('/agent/signin') }} className="flex items-center gap-2 text-body-md text-secondary hover:text-ink lg:hidden">
            <Icon name="logout" size={18} /> Sign out
          </button>
        </header>
        <main className="px-4 py-6 sm:px-8 sm:py-8">
          <Stack gap={6}>
            <Stack direction="responsive" gap={4} justify="between" align={{ base: 'start', sm: 'end' }}>
              <Stack gap={1}>
                <Heading level={1} size="lg">{title}</Heading>
                {subtitle && <Text tone="muted">{subtitle}</Text>}
              </Stack>
              {actions}
            </Stack>
            {children}
          </Stack>
        </main>
      </div>
    </div>
  )
}

/** The branch this agent is working at (sessionStorage). Defaults to the first branch. */
function BranchSelector() {
  const branches = useBranches()
  const [branchId, setBranchId] = useStaffBranch()

  useEffect(() => {
    if (!branchId && branches.data?.length) setBranchId(branches.data[0].id)
  }, [branchId, branches.data, setBranchId])

  return (
    <label className="flex items-center gap-3">
      <Text as="span" size="label" tone="muted" icon="storefront">Branch</Text>
      <Select controlSize="sm" className="min-w-[16rem]" value={branchId} disabled={!branches.data} onChange={(e) => setBranchId(e.target.value)} aria-label="Branch you are working at"
        options={branches.data?.map((b) => ({ value: b.id, label: `${b.code} · ${b.name}` })) ?? [{ value: '', label: 'Loading branches…' }]} />
    </label>
  )
}

/** Full-screen dark frame for the staff sign-in: wordmark, a white card, and a way back to the customer site. */
export function StaffAuthLayout({ children }: { children: ReactNode }) {
  return (
    <div className="flex min-h-screen items-center justify-center bg-inverse-surface px-4 py-10">
      <div className="w-full max-w-md">
        <div className="mb-6 flex justify-center text-inverse-on-surface"><StaffWordmark /></div>
        <div className="rounded-card bg-white p-6 shadow-overlay sm:p-8">{children}</div>
        <p className="mt-6 text-center"><a href="#/" className="text-body-md text-inverse-on-surface/80 hover:text-inverse-on-surface hover:underline">← Back to the booking site</a></p>
      </div>
    </div>
  )
}

/** The staff shell while the account (role) is being checked. */
export function StaffShellSkeleton() {
  return (
    <div className="flex min-h-screen bg-surface-container-low">
      <div className="hidden w-64 bg-inverse-surface p-5 text-inverse-on-surface lg:block"><StaffWordmark /></div>
      <div className="flex-1 space-y-4 p-8"><Skeleton size="title" className="w-72" /><Skeleton size="panel" /></div>
    </div>
  )
}
