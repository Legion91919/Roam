import type { ChecklistCode, ChecklistStatus, DamageMark, Inspection, UpdateInspectionRequest } from '../../api/types'
import { CHECKLIST_ORDER } from './inspectionText'

/** What the agent is editing in the stepper (strings where the inputs are text). */
export interface InspectionDraft {
  checklist: Record<ChecklistCode, { status: ChecklistStatus; note: string }>
  odometer: string
  fuel: number | null
  damage: DamageMark[]
}

export function toDraft(report: Inspection): InspectionDraft {
  const checklist = Object.fromEntries(CHECKLIST_ORDER.map((code) => {
    const item = report.checklist.find((i) => i.code === code)
    return [code, { status: item?.status ?? 'Unanswered', note: item?.note ?? '' }]
  })) as InspectionDraft['checklist']
  return { checklist, odometer: report.odometerKm?.toString() ?? '', fuel: report.fuelEighths, damage: report.damage }
}

/** PUT /api/agent/inspections/{id} body. */
export function toRequest(draft: InspectionDraft): UpdateInspectionRequest {
  return {
    odometerKm: draft.odometer === '' ? null : Number(draft.odometer),
    fuelEighths: draft.fuel,
    checklist: CHECKLIST_ORDER.map((code) => ({ code, status: draft.checklist[code].status, note: draft.checklist[code].note.trim() || null })),
    damage: draft.damage,
  }
}

export const unansweredCount = (draft: InspectionDraft) => CHECKLIST_ORDER.filter((c) => draft.checklist[c].status === 'Unanswered').length

export interface StepProps {
  draft: InspectionDraft
  setDraft: (draft: InspectionDraft) => void
  locked: boolean
}
