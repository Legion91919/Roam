import { useState } from 'react'
import type { Inspection } from '../../api/types'
import { formatDay, formatTime } from '../../lib/format'
import { BulletList, Callout, DataList, Dialog, Grid, Inline, Pill, Stack, Text } from '../ui'
import { AttachmentTile } from './AttachmentTile'
import { FuelGauge } from './FuelGauge'
import { CHECKLIST_LABEL, CHECKLIST_ORDER, STATUS_LABEL, STATUS_TONE } from './inspectionText'

/**
 * Read-only inspection report: used by the customer after checkout/return and by staff for review.
 * Prints cleanly on A4 ("Print / Save as PDF"): see the print: utilities and @media print in styles.css.
 */
export function InspectionReportView({ report }: { report: Inspection }) {
  const [photo, setPhoto] = useState<string | null>(null)
  const items = CHECKLIST_ORDER.map((code) => report.checklist.find((i) => i.code === code) ?? { code, status: 'Unanswered' as const, note: null })

  return (
    <Stack gap={6} data-testid={`report-${report.kind.toLowerCase()}`}>
      <DataList columns={4} size="md" items={[
        { label: 'Inspected', value: report.inspectedAt ? `${formatDay(report.inspectedAt)} ${formatTime(report.inspectedAt)}` : 'Draft' },
        { label: 'By', value: report.inspectedByName },
        { label: 'Car', value: report.vehicleRegistration, mono: true },
        { label: 'Odometer', value: <span className="tabular">{report.odometerKm?.toLocaleString('en-ZA') ?? '–'} km</span> },
      ]} />

      <Stack gap={2} className="max-w-sm"><Text size="sm" tone="muted">Fuel</Text><FuelGauge value={report.fuelEighths} /></Stack>

      {report.newDamage.length > 0 && (
        <Callout tone="danger" title="New damage since checkout" role="note">
          <BulletList items={report.newDamage.map((d, i) => (
            <span key={i}><span className="font-medium">{d.item ? CHECKLIST_LABEL[d.item as keyof typeof CHECKLIST_LABEL] ?? d.panel : d.panel}</span>: {d.description}{d.severity ? ` (${d.severity.toLowerCase()})` : ''}</span>
          ))} />
        </Callout>
      )}

      <div className="overflow-hidden rounded-card border border-line">
        <table className="w-full text-left text-body-md">
          <thead className="bg-surface-container-low text-label-sm uppercase tracking-wider text-secondary">
            <tr><th className="px-4 py-2.5 font-semibold">Item</th><th className="px-4 py-2.5 font-semibold">Condition</th><th className="hidden px-4 py-2.5 font-semibold sm:table-cell">Note</th></tr>
          </thead>
          <tbody className="divide-y divide-line">
            {items.map((item) => (
              <tr key={item.code} className="print:break-inside-avoid">
                <td className="px-4 py-2.5 text-ink">{CHECKLIST_LABEL[item.code]}<span className="block text-body-sm text-secondary sm:hidden">{item.note}</span></td>
                <td className="px-4 py-2.5"><Pill tone={STATUS_TONE[item.status]}>{STATUS_LABEL[item.status]}</Pill></td>
                <td className="hidden px-4 py-2.5 text-secondary sm:table-cell">{item.note ?? ''}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {report.damage.length > 0 && (
        <Stack gap={2}>
          <Text size="sm" tone="muted">Damage marks</Text>
          <Stack as="ul" gap={1}>
            {report.damage.map((d, i) => <li key={i}><Inline gap={2}><Pill tone={d.severity === 'Severe' ? 'danger' : 'warning'}>{d.severity}</Pill><Text as="span" tone="strong">{d.panel}: {d.description}</Text></Inline></li>)}
          </Stack>
        </Stack>
      )}

      {report.attachments.length > 0 && (
        <Stack gap={2}>
          <Text size="sm" tone="muted">Photos and files ({report.attachments.length})</Text>
          <Grid columns={{ base: 2, sm: 4 }} gap={3}>
            {report.attachments.map((a) => <AttachmentTile key={a.id} inspectionId={report.id} attachment={a} onOpen={setPhoto} />)}
          </Grid>
        </Stack>
      )}

      <Dialog open={photo !== null} onClose={() => setPhoto(null)} title="Photo" size="lg">
        {photo && <img src={photo} alt="Inspection photo, enlarged" className="max-h-[70vh] w-full rounded-lg object-contain" />}
      </Dialog>
    </Stack>
  )
}
