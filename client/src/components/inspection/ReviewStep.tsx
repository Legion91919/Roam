import type { Inspection } from '../../api/types'
import { BulletList, Callout, CheckList, DataList, Stack, Text } from '../ui'
import { ANGLE_LABEL, CHECKLIST_LABEL, REQUIRED_ANGLES } from './inspectionText'

/** Step 4: what is still missing (the server's completion rules, as a checklist), and the readings. */
export function ReviewStep({ report, unanswered }: { report: Inspection; unanswered: number }) {
  const problems = report.checklist.filter((i) => i.status === 'Damaged' || i.status === 'Missing')
  const missingAngles = report.kind === 'Checkout' ? REQUIRED_ANGLES.filter((a) => !report.attachments.some((x) => x.angle === a && x.contentType.startsWith('image/'))) : []
  const driver = report.checklist.find((i) => i.code === 'DriverIdAndLicenceVerified')?.status

  const checks = [
    { ok: unanswered === 0, text: unanswered === 0 ? 'Every checklist item answered' : `${unanswered} checklist item(s) not answered` },
    { ok: report.odometerKm !== null && report.fuelEighths !== null, text: 'Odometer and fuel recorded' },
    ...(report.kind === 'Checkout'
      ? [
          { ok: missingAngles.length === 0, text: missingAngles.length === 0 ? 'Front, back, left and right photos' : `Missing photos: ${missingAngles.map((a) => ANGLE_LABEL[a].toLowerCase()).join(', ')}` },
          { ok: driver === 'Ok', text: "Driver's ID and licence verified" },
        ]
      : []),
  ]

  return (
    <Stack gap={5}>
      {report.status === 'Completed'
        ? <Callout tone="success" icon="verified">Completed: this report is now evidence and can't be changed.</Callout>
        : <CheckList items={checks} />}
      <DataList columns={3} items={[
        { label: 'Odometer', value: <span className="tabular">{report.odometerKm?.toLocaleString('en-ZA') ?? '–'} km</span> },
        { label: 'Fuel', value: report.fuelEighths === null ? '–' : `${report.fuelEighths}/8` },
        { label: 'Photos and files', value: report.attachments.length },
      ]} />
      {problems.length > 0 && (
        <Stack gap={1}>
          <Text size="sm" tone="muted">Recorded problems</Text>
          <BulletList items={problems.map((d) => `${CHECKLIST_LABEL[d.code]}: ${d.note}`)} />
        </Stack>
      )}
      {report.newDamage.length > 0 && (
        <Callout tone="danger" title="New damage since checkout">
          <BulletList items={report.newDamage.map((d) => `${d.panel}: ${d.description}`)} />
        </Callout>
      )}
    </Stack>
  )
}
