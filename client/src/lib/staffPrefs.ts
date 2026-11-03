import { useCallback, useSyncExternalStore } from 'react'

/**
 * Per-tab staff preferences (selected branch and day). One small store shared by every component
 * (the branch selector in the header and the page that uses the branch), backed by sessionStorage so
 * the choice survives moving between staff pages and a refresh. If storage is unavailable (private
 * mode, blocked), the values simply live in memory for this tab.
 */
const values = new Map<string, string>()
const listeners = new Set<() => void>()

function read(key: string, initial: () => string): string {
  if (!values.has(key)) {
    let stored: string | null = null
    try {
      stored = sessionStorage.getItem(key)
    } catch {
      /* storage unavailable */
    }
    values.set(key, stored ?? initial())
  }
  return values.get(key)!
}

function write(key: string, value: string) {
  values.set(key, value)
  try {
    sessionStorage.setItem(key, value)
  } catch {
    /* storage unavailable: keep it in memory */
  }
  listeners.forEach((notify) => notify())
}

function subscribe(listener: () => void) {
  listeners.add(listener)
  return () => listeners.delete(listener)
}

function useStaffValue(key: string, initial: () => string): [string, (value: string) => void] {
  const value = useSyncExternalStore(subscribe, () => read(key, initial))
  const set = useCallback((next: string) => write(key, next), [key])
  return [value, set]
}

/** yyyy-MM-dd in the browser's time zone. */
export function todayIso(): string {
  const d = new Date()
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
}

const noBranch = () => ''

/** The branch this agent is working at. Empty until they (or the default) pick one. */
export function useStaffBranch() {
  return useStaffValue('roam.staff.branch', noBranch)
}

/** The day the counter lists show. Starts at today; not persisted across tabs. */
export function useStaffDate() {
  return useStaffValue('roam.staff.date', todayIso)
}
