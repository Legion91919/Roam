import { useEffect, useState } from 'react'

/**
 * Tiny hash router (#/results, #/reservations/<id>): enough for a PoC, no dependency, and deep
 * links survive a refresh. A larger app would use React Router or TanStack Router.
 */
export type Route =
  | { name: 'search' }
  | { name: 'results' }
  | { name: 'review' }
  | { name: 'reservation'; id: string }
  | { name: 'bookings' }
  | { name: 'signin'; next: string }
  | { name: 'profile'; next: string }
  | { name: 'agent'; page: StaffPage; id?: string; reference?: string }

/** Staff ("Roam Staff") area under #/agent/*. Kept apart from the customer screens. */
export type StaffPage = 'signin' | 'today' | 'checkout' | 'returns' | 'find' | 'customer' | 'fleet' | 'vehicle' | 'vehicleNew'

function parse(hash: string): Route {
  const [path = '/', query = ''] = (hash.replace(/^#/, '') || '/').split('?')
  const next = new URLSearchParams(query).get('next') ?? ''
  if (path === '/agent' || path.startsWith('/agent/')) return parseStaff(path, new URLSearchParams(query))
  const reservation = path.match(/^\/reservations\/([0-9a-fA-F-]{36})$/)
  if (reservation) return { name: 'reservation', id: reservation[1] }
  if (path === '/results') return { name: 'results' }
  if (path === '/review') return { name: 'review' }
  if (path === '/bookings') return { name: 'bookings' }
  if (path === '/signin') return { name: 'signin', next }
  if (path === '/profile') return { name: 'profile', next }
  return { name: 'search' }
}

function parseStaff(path: string, query: URLSearchParams): Route {
  const [, , section = 'today', id] = path.split('/')
  const valid = /^[0-9a-fA-F-]{36}$/.test(id ?? '') ? id : undefined
  switch (section) {
    case 'signin': return { name: 'agent', page: 'signin' }
    case 'checkout': return { name: 'agent', page: 'checkout', id: valid }
    case 'returns': return { name: 'agent', page: 'returns', id: valid }
    case 'find': return { name: 'agent', page: 'find', reference: query.get('ref') ?? '' }
    case 'customers': return { name: 'agent', page: 'customer' }
    case 'fleet':
      if (id === 'new') return { name: 'agent', page: 'vehicleNew' }
      return valid ? { name: 'agent', page: 'vehicle', id: valid } : { name: 'agent', page: 'fleet' }
    default: return { name: 'agent', page: 'today' }
  }
}

/** e.g. withNext('/signin', '/review') -> "/signin?next=%2Freview" */
export function withNext(path: string, next: string) {
  return `${path}?next=${encodeURIComponent(next)}`
}

export function navigate(path: string) {
  window.location.hash = path
  window.scrollTo({ top: 0 })
}

export function useRoute(): Route {
  const [route, setRoute] = useState(() => parse(window.location.hash))
  useEffect(() => {
    const onChange = () => setRoute(parse(window.location.hash))
    window.addEventListener('hashchange', onChange)
    return () => window.removeEventListener('hashchange', onChange)
  }, [])
  return route
}
