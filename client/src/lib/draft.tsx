import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from 'react'
import type { TripDetails, VehicleCategory } from '../api/types'
import { dateFromToday, toIsoWithOffset } from './format'

/**
 * The booking the customer is building across Search -> Results -> Driver.
 * Client-only state (server state lives in TanStack Query). Kept in sessionStorage so a refresh
 * mid-flow doesn't lose it.
 */
export interface BookingDraft {
  pickupBranchId: string
  returnBranchId: string
  oneWay: boolean
  pickupDate: string
  pickupTime: string
  returnDate: string
  returnTime: string
  category: VehicleCategory | null
  gps: boolean
  childSeat: boolean
}

const initialDraft: BookingDraft = {
  pickupBranchId: '',
  returnBranchId: '',
  oneWay: false,
  pickupDate: dateFromToday(14),
  pickupTime: '09:00',
  returnDate: dateFromToday(17),
  returnTime: '09:00',
  category: null,
  gps: false,
  childSeat: false,
}

const STORAGE_KEY = 'roam.draft.v2'

function load(): BookingDraft {
  try {
    const raw = sessionStorage.getItem(STORAGE_KEY)
    return raw ? { ...initialDraft, ...JSON.parse(raw) } : initialDraft
  } catch {
    return initialDraft
  }
}

interface DraftContextValue {
  draft: BookingDraft
  update: (changes: Partial<BookingDraft>) => void
  reset: () => void
  pickupAt: string
  returnAt: string
  returnBranchId: string
  trip: TripDetails | null
}

const DraftContext = createContext<DraftContextValue | null>(null)

export function DraftProvider({ children }: { children: ReactNode }) {
  const [draft, setDraft] = useState<BookingDraft>(load)

  useEffect(() => {
    try {
      sessionStorage.setItem(STORAGE_KEY, JSON.stringify(draft))
    } catch {
      /* storage unavailable: keep working in memory */
    }
  }, [draft])

  const value = useMemo<DraftContextValue>(() => {
    const pickupAt = toIsoWithOffset(draft.pickupDate, draft.pickupTime)
    const returnAt = toIsoWithOffset(draft.returnDate, draft.returnTime)
    const returnBranchId = draft.oneWay && draft.returnBranchId ? draft.returnBranchId : draft.pickupBranchId
    const extras = [draft.gps && 'Gps', draft.childSeat && 'ChildSeat'].filter(Boolean).join(', ') || 'None'
    const trip: TripDetails | null =
      draft.category && draft.pickupBranchId && pickupAt && returnAt
        ? {
            category: draft.category,
            pickupBranchId: draft.pickupBranchId,
            returnBranchId,
            pickupAt,
            returnAt,
            extras,
          }
        : null
    return {
      draft,
      update: (changes) => setDraft((d) => ({ ...d, ...changes })),
      reset: () => setDraft((d) => ({ ...initialDraft, pickupBranchId: d.pickupBranchId })),
      pickupAt,
      returnAt,
      returnBranchId,
      trip,
    }
  }, [draft])

  return <DraftContext.Provider value={value}>{children}</DraftContext.Provider>
}

export function useDraft(): DraftContextValue {
  const context = useContext(DraftContext)
  if (!context) throw new Error('useDraft must be used inside <DraftProvider>')
  return context
}
