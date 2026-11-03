import type { ChecklistCode, ChecklistStatus } from '../../api/types'
import { Button, Inline, Input, Segmented, Stack, Text, type SegmentTone } from '../ui'
import type { InspectionDraft, StepProps } from './inspectionDraft'
import { CHECKLIST_LABEL, CHECKLIST_ORDER, STATUS_LABEL } from './inspectionText'

const ANSWERS: ChecklistStatus[] = ['Ok', 'Damaged', 'Missing', 'NotApplicable']
const ANSWER_TONE: Record<ChecklistStatus, SegmentTone> = { Ok: 'brand', Damaged: 'danger', Missing: 'warning', NotApplicable: 'neutral', Unanswered: 'neutral' }

/** Step 1: every item answered; Damaged/Missing need a note (the server's rule, shown as you go). */
export function ChecklistStep({ draft, setDraft, locked }: StepProps) {
  const set = (code: ChecklistCode, changes: Partial<{ status: ChecklistStatus; note: string }>) =>
    setDraft({ ...draft, checklist: { ...draft.checklist, [code]: { ...draft.checklist[code], ...changes } } })
  const allOk = () => setDraft({
    ...draft,
    checklist: Object.fromEntries(CHECKLIST_ORDER.map((c) => [c, { status: draft.checklist[c].status === 'Unanswered' ? 'Ok' : draft.checklist[c].status, note: draft.checklist[c].note }])) as InspectionDraft['checklist'],
  })

  return (
    <Stack gap={3}>
      <Inline gap={2} justify="between">
        <Text tone="muted">Walk around the car with the customer. Damaged or missing items need a note.</Text>
        {!locked && <Button variant="ghost" size="sm" icon="done_all" onClick={allOk}>Mark the rest OK</Button>}
      </Inline>
      <ul className="divide-y divide-line rounded-card border border-line">
        {CHECKLIST_ORDER.map((code) => {
          const item = draft.checklist[code]
          const needsNote = item.status === 'Damaged' || item.status === 'Missing'
          return (
            <li key={code} className="px-4 py-3">
              <Stack gap={2}>
                <div className="flex flex-col gap-2 md:flex-row md:items-center md:justify-between">
                  <Stack gap={0}>
                    <Text tone="strong">{CHECKLIST_LABEL[code]}</Text>
                    {code === 'DriverIdAndLicenceVerified' && <Text size="sm" tone="muted">Must be OK to hand over the keys.</Text>}
                  </Stack>
                  <div className="md:w-[22rem]">
                    <Segmented size="sm" label={CHECKLIST_LABEL[code]} options={ANSWERS} value={item.status} disabled={locked}
                      labelOf={(a) => STATUS_LABEL[a]} toneOf={(a) => ANSWER_TONE[a]} onChange={(status) => set(code, { status })} />
                  </div>
                </div>
                {needsNote && (
                  <Input controlSize="sm" invalid={!item.note.trim()} maxLength={300} disabled={locked}
                    placeholder={`What is ${item.status.toLowerCase()}? e.g. "Scratch on the rear door"`}
                    aria-label={`Note for ${CHECKLIST_LABEL[code]}`} value={item.note} onChange={(e) => set(code, { note: e.target.value })} />
                )}
              </Stack>
            </li>
          )
        })}
      </ul>
    </Stack>
  )
}
