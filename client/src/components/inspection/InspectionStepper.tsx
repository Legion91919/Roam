import { useEffect, useRef, useState } from 'react'
import { describeError } from '../../api/client'
import { useCompleteInspection, useInspection, useStartInspection, useUpdateInspection } from '../../api/hooks'
import type { Inspection, InspectionKind } from '../../api/types'
import { Banner, Button, Card, CardHeader, CardSection, FormActions, Pill, Skeleton, Stack, Steps } from '../ui'
import { ChecklistStep } from './ChecklistStep'
import { toDraft, toRequest, unansweredCount } from './inspectionDraft'
import { PhotosStep } from './PhotosStep'
import { ReadingsStep } from './ReadingsStep'
import { ReviewStep } from './ReviewStep'

const STEPS = ['Checklist', 'Fuel & odometer', 'Photos', 'Review']

/**
 * The counter inspection, one step at a time: checklist → fuel & odometer → photos → review & complete.
 * Shared by checkout and return. Every "Next" saves the draft to the server, so an agent can leave and
 * resume. The rules (all answered, notes for damage, 4 photos, driver verified) are the SERVER's: the
 * stepper only guides; completing shows the API's error code if something is missing.
 */
export function InspectionStepper({ reservationId, kind, vehicleId, minimumOdometer, onCompleted }: {
  reservationId: string
  kind: InspectionKind
  vehicleId: string | null
  /** Odometer the reading can't be below (the car's current reading). Guidance only. */
  minimumOdometer?: number
  onCompleted: (report: Inspection) => void
}) {
  const start = useStartInspection()
  const [reportId, setReportId] = useState<string | null>(null)
  const startedFor = useRef<string | null>(null)

  useEffect(() => {
    const key = `${reservationId}:${kind}:${vehicleId}`
    if (startedFor.current === key) return
    if (kind === 'Checkout' && !vehicleId) return
    startedFor.current = key
    start.mutate({ reservationId, kind, vehicleId }, { onSuccess: (report) => setReportId(report.id) })
  }, [reservationId, kind, vehicleId, start])

  const report = useInspection(reportId)

  if (start.isError) return <Banner error={describeError(start.error)} onRetry={() => { startedFor.current = null; start.reset() }} />
  if (!report.data) return <Skeleton size="page" />
  return <InspectionSteps key={report.data.id} report={report.data} minimumOdometer={minimumOdometer} onCompleted={onCompleted} />
}

function InspectionSteps({ report, minimumOdometer, onCompleted }: { report: Inspection; minimumOdometer?: number; onCompleted: (report: Inspection) => void }) {
  const [step, setStep] = useState(report.status === 'Completed' ? 3 : 0)
  const [draft, setDraft] = useState(() => toDraft(report))
  const update = useUpdateInspection(report.id)
  const complete = useCompleteInspection(report.id)
  const locked = report.status === 'Completed'

  useEffect(() => {
    if (locked) onCompleted(report)
  }, [locked, report, onCompleted])

  // Every move forward saves the draft first; going back (or anything once locked) just moves.
  const save = (next: number) => {
    if (locked) return setStep(next)
    update.mutate(toRequest(draft), { onSuccess: () => setStep(next) })
  }
  const goTo = (i: number) => (i < step || locked ? setStep(i) : save(i))
  const error = update.isError ? describeError(update.error) : complete.isError ? describeError(complete.error) : null
  const stepProps = { draft, setDraft, locked }

  return (
    <Card padding="none">
      <Steps label="Inspection steps" steps={STEPS} current={step} isDone={(i) => i < step || locked} onSelect={goTo} disabled={update.isPending} />
      <CardSection gap={4}>
        <CardHeader size="sm"
          title={`${report.kind} inspection · ${report.vehicleRegistration}`}
          actions={<Pill tone={locked ? 'success' : 'neutral'} dot>{locked ? 'Completed' : 'Draft'}</Pill>} />

        {step === 0 && <ChecklistStep {...stepProps} />}
        {step === 1 && <ReadingsStep {...stepProps} minimumOdometer={minimumOdometer} />}
        {step === 2 && <PhotosStep report={report} locked={locked} />}
        {step === 3 && <ReviewStep report={report} unanswered={unansweredCount(draft)} />}

        {error && <Banner error={error} onDismiss={() => { update.reset(); complete.reset() }} />}

        <Stack className="mt-2">
          <FormActions align="between">
            <Button variant="secondary" icon="arrow_back" disabled={step === 0} onClick={() => setStep(step - 1)}>Back</Button>
            {step < 3 ? (
              <Button trailingIcon="arrow_forward" loading={update.isPending} loadingText="Saving…" onClick={() => save(step + 1)}>
                {locked ? 'Next' : 'Save and continue'}
              </Button>
            ) : !locked && (
              <Button icon="task_alt" loading={complete.isPending} loadingText="Completing…" onClick={() => complete.mutate()}>
                Complete inspection
              </Button>
            )}
          </FormActions>
        </Stack>
      </CardSection>
    </Card>
  )
}
