import { useState } from 'react'
import { describeError } from '../../api/client'
import { useRemoveAttachment, useUploadAttachment } from '../../api/hooks'
import type { AttachmentAngle, Inspection } from '../../api/types'
import { Banner, FileButton, Grid, Inline, Select, Stack, Text, UploadTile } from '../ui'
import { AttachmentTile } from './AttachmentTile'
import { ANGLE_LABEL, REQUIRED_ANGLES } from './inspectionText'

const EXTRA_ANGLES: AttachmentAngle[] = ['Damage', 'Interior', 'Document', 'Other', 'Front', 'Back', 'Left', 'Right']
const TILE_COLUMNS = { base: 2, md: 4 } as const

/**
 * Step 3: the required front/back/left/right photos at checkout (the phone camera opens directly),
 * plus any extra photos or PDFs. Uploads go straight to the server (type checked by magic bytes,
 * 10 MB, metadata stripped there).
 */
export function PhotosStep({ report, locked }: { report: Inspection; locked: boolean }) {
  const upload = useUploadAttachment(report.id)
  const remove = useRemoveAttachment(report.id)
  const [extraAngle, setExtraAngle] = useState<AttachmentAngle>('Damage')
  const [uploading, setUploading] = useState<AttachmentAngle | null>(null)
  const required = report.kind === 'Checkout' ? REQUIRED_ANGLES : []
  const extras = report.attachments.filter((a) => !required.includes(a.angle) || report.attachments.filter((b) => b.angle === a.angle)[0] !== a)

  const send = (file: File, angle: AttachmentAngle) => {
    setUploading(angle)
    upload.mutate({ file, angle }, { onSettled: () => setUploading(null) })
  }
  const removeFile = locked ? undefined : (id: string) => () => remove.mutate(id)

  return (
    <Stack gap={6}>
      <Text tone="muted">
        {report.kind === 'Checkout'
          ? 'Photograph the front, back, left and right of the car. On a phone, the camera opens directly. JPEG, PNG or PDF, up to 10 MB each.'
          : 'Photograph any damage or anything you want on record. Optional for returns.'}
      </Text>

      {required.length > 0 && (
        <Grid columns={TILE_COLUMNS} gap={3}>
          {required.map((angle) => {
            const photo = report.attachments.find((a) => a.angle === angle)
            return photo
              ? <AttachmentTile key={angle} inspectionId={report.id} attachment={photo} onRemove={removeFile?.(photo.id)} />
              : <UploadTile key={angle} title={ANGLE_LABEL[angle]} required busy={uploading === angle} disabled={locked}
                  accept="image/jpeg,image/png" capture="environment" inputLabel={`${ANGLE_LABEL[angle]} photo`} onFile={(file) => send(file, angle)} />
          })}
        </Grid>
      )}

      {(extras.length > 0 || !locked) && (
        <Stack gap={3}>
          <Text as="span" size="label" tone="strong">More photos and documents</Text>
          {extras.length > 0 && (
            <Grid columns={TILE_COLUMNS} gap={3}>
              {extras.map((a) => <AttachmentTile key={a.id} inspectionId={report.id} attachment={a} onRemove={removeFile?.(a.id)} />)}
            </Grid>
          )}
          {!locked && (
            <Inline gap={2}>
              <Select controlSize="sm" fit aria-label="What the file shows" value={extraAngle} onChange={(e) => setExtraAngle(e.target.value as AttachmentAngle)}
                options={EXTRA_ANGLES.map((a) => ({ value: a, label: ANGLE_LABEL[a] }))} />
              <FileButton accept="image/jpeg,image/png,application/pdf" busy={uploading === extraAngle} onFile={(file) => send(file, extraAngle)}>Add file</FileButton>
              <Text as="span" size="sm" tone="muted">{report.attachments.length}/20 files</Text>
            </Inline>
          )}
        </Stack>
      )}

      {upload.isError && <Banner error={describeError(upload.error)} onDismiss={() => upload.reset()} />}
      {remove.isError && <Banner error={describeError(remove.error)} onDismiss={() => remove.reset()} />}
    </Stack>
  )
}
