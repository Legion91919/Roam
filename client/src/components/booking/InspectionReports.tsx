import { describeError } from '../../api/client'
import { useAcknowledgeInspection, useMyInspections } from '../../api/hooks'
import type { Inspection } from '../../api/types'
import { formatDay, formatTime } from '../../lib/format'
import { InspectionReportView } from '../inspection/InspectionReportView'
import { Banner, Button, Card, CardFooter, CardHeader, CardSection, Pill, QueryState, Stack, Text } from '../ui'

/**
 * The customer's copy of the checkout and return inspections: the evidence of the car's condition
 * when they took it and when they brought it back. Only COMPLETED reports reach the customer (the
 * API returns 404 for drafts and for other people's bookings). "Print / Save as PDF" uses the
 * browser's print dialog with the @media print rules in styles.css.
 */
export function InspectionReports({ reservationId }: { reservationId: string }) {
  const reports = useMyInspections(reservationId, true)
  const ordered = [...(reports.data ?? [])].sort((a, b) => (a.kind === 'Checkout' ? -1 : 1) - (b.kind === 'Checkout' ? -1 : 1))

  return (
    <QueryState query={reports} loading="card">
      {ordered.length > 0 && (
        <Stack as="section" gap={6} aria-label="Inspection reports">
          {ordered.map((report) => <ReportCard key={report.id} reservationId={reservationId} report={report} />)}
        </Stack>
      )}
    </QueryState>
  )
}

function ReportCard({ reservationId, report }: { reservationId: string; report: Inspection }) {
  const acknowledge = useAcknowledgeInspection(reservationId)
  const acknowledgedAt = report.customerAcknowledgedAt

  return (
    <Card padding="none" className="print:border-0" data-testid="report-card">
      <CardHeader divider size="sm" icon="fact_check"
        title={report.kind === 'Checkout' ? 'Checkout inspection' : 'Return inspection'}
        subtitle="The condition of the car, recorded with you at the counter."
        actions={<>
          {report.newDamage.length > 0 && <Pill tone="danger" dot>New damage</Pill>}
          <Button variant="secondary" size="sm" icon="print" onClick={() => window.print()}>Print / Save as PDF</Button>
        </>} />
      <CardSection padding="lg"><InspectionReportView report={report} /></CardSection>
      <CardFooter tone="muted">
        {acknowledgedAt ? (
          <Text tone="brand" icon="verified">You confirmed this report on {formatDay(acknowledgedAt)} at {formatTime(acknowledgedAt)}.</Text>
        ) : (
          <>
            <Text tone="muted">Does this match the car? If something is missing, tell the branch before you drive off.</Text>
            <span className="print:hidden">
              <Button icon="check" loading={acknowledge.isPending} loadingText="Saving…" onClick={() => acknowledge.mutate(report.id)}>I've checked this report</Button>
            </span>
          </>
        )}
        {acknowledge.isError && <Banner error={describeError(acknowledge.error)} onDismiss={() => acknowledge.reset()} />}
      </CardFooter>
    </Card>
  )
}
