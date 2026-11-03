import { useInspection } from '../../api/hooks'
import type { InspectionSummary, VehicleHistory as History, VehicleHistoryEvent } from '../../api/types'
import { formatMoney, formatShortDate, formatTime } from '../../lib/format'
import { InspectionReportView } from '../inspection/InspectionReportView'
import { Button, Card, CardHeader, Dialog, Icon, Inline, QueryState, Text, TextLink, Timeline, TimelineItem } from '../ui'
import { km } from './VehicleParts'

const EVENT: Record<VehicleHistoryEvent['kind'], { icon: string; label: string }> = {
  Registered: { icon: 'add_circle', label: 'Added to the fleet' },
  Rental: { icon: 'key', label: 'Rental' },
  SentToService: { icon: 'build', label: 'Sent to service' },
  ReturnedFromService: { icon: 'check_circle', label: 'Back from service' },
  MovedBranch: { icon: 'local_shipping', label: 'Moved branch' },
  Retired: { icon: 'block', label: 'Retired' },
}

/** The car's logbook and rentals, newest first; each rental opens its inspection reports. */
export function VehicleTimeline({ history, onOpenReport }: { history: History; onOpenReport: (inspectionId: string) => void }) {
  return (
    <Card gap={5}>
      <CardHeader size="sm" icon="history" title="History" />
      {history.timeline.length === 0 ? <Text tone="muted">Nothing recorded yet.</Text> : (
        <Timeline>
          {history.timeline.map((event, i) => <HistoryEntry key={i} event={event} onOpenReport={onOpenReport} />)}
        </Timeline>
      )}
    </Card>
  )
}

function HistoryEntry({ event, onOpenReport }: { event: VehicleHistoryEvent; onOpenReport: (id: string) => void }) {
  const meta = EVENT[event.kind] ?? { icon: 'radio_button_checked', label: event.kind }
  const rental = event.rental
  return (
    <TimelineItem icon={meta.icon} meta={`${formatShortDate(event.at)} ${formatTime(event.at)}`}
      title={<>{meta.label}{event.branchName && <Text as="span" tone="muted"> · {event.branchName}</Text>}</>}>
      {event.note && <Text>{event.note}</Text>}
      {!rental && event.odometerKm != null && <Text size="sm" tone="muted">{km(event.odometerKm)}</Text>}
      {rental && (
        <Card padding="sm" gap={3}>
          <Inline gap={6} rowGap={1}>
            <Text as="span" mono><TextLink href={`#/agent/find?ref=${rental.reference}`}>{rental.reference}</TextLink></Text>
            <Text as="span" tone="strong">{rental.customerName}</Text>
            <Text as="span" tone="muted" tabular>{km(rental.odometerOut)} → {rental.returnedAt ? km(rental.odometerIn) : 'on rent'}</Text>
            {rental.distanceKm != null && <Text as="span" tone="muted" tabular>{km(rental.distanceKm)} driven</Text>}
            {rental.lateFee > 0 && <Text as="span" tone="warning">Late fee {formatMoney(rental.lateFee)}</Text>}
          </Inline>
          {rental.inspections.length > 0 && (
            <Inline gap={2}>
              {rental.inspections.map((s) => <ReportChip key={s.id} summary={s} onOpen={() => onOpenReport(s.id)} />)}
            </Inline>
          )}
        </Card>
      )}
    </TimelineItem>
  )
}

function ReportChip({ summary, onOpen }: { summary: InspectionSummary; onOpen: () => void }) {
  const damaged = summary.newDamage.length > 0
  return (
    <button type="button" onClick={onOpen} className={`inline-flex items-center gap-1.5 rounded-full border px-3 py-1 text-label-md ${damaged ? 'border-danger/30 bg-danger-surface text-danger' : 'border-line bg-surface-container-low text-on-surface-variant'} hover:border-primary-container`}>
      <Icon name="fact_check" size={16} /> {summary.kind} report
      {summary.status === 'Draft' && ' (draft)'}
      {damaged && ` · ${summary.newDamage.length} new damage`}
    </button>
  )
}

/** One inspection report in a dialog (staff see drafts too). */
export function InspectionReportDialog({ id, onClose }: { id: string | null; onClose: () => void }) {
  const report = useInspection(id)
  return (
    <Dialog open={id !== null} onClose={onClose} size="xl" title={report.data ? `${report.data.kind} inspection · ${report.data.vehicleRegistration}` : 'Inspection report'}
      footer={<Button variant="secondary" onClick={onClose}>Close</Button>}>
      <QueryState query={report} loading="card">
        {report.data && <InspectionReportView report={report.data} />}
      </QueryState>
    </Dialog>
  )
}
