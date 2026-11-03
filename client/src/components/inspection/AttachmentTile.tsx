import { downloadAttachment, useAttachmentUrl } from '../../api/hooks'
import type { InspectionAttachment } from '../../api/types'
import { Icon, Skeleton } from '../ui'
import { ANGLE_LABEL } from './inspectionText'

/**
 * One photo or PDF from an inspection. Images are fetched with the bearer token (the API decides who
 * may see them) and shown from an object URL; PDFs offer a download.
 */
export function AttachmentTile({ inspectionId, attachment, onOpen, onRemove }: {
  inspectionId: string
  attachment: InspectionAttachment
  onOpen?: (url: string) => void
  onRemove?: () => void
}) {
  const isImage = attachment.contentType.startsWith('image/')
  const url = useAttachmentUrl(inspectionId, attachment.id)

  return (
    <figure className="group relative overflow-hidden rounded-card border border-line bg-white print:break-inside-avoid">
      {isImage ? (
        url.data ? (
          <button type="button" className="block w-full" onClick={() => onOpen?.(url.data!)} aria-label={`Open ${ANGLE_LABEL[attachment.angle]} photo`}>
            <img src={url.data} alt={`${ANGLE_LABEL[attachment.angle]} of the car`} className="aspect-[4/3] w-full bg-surface-container-low object-cover" />
          </button>
        ) : url.isError ? (
          <div className="flex aspect-[4/3] items-center justify-center bg-surface-container-low text-muted"><Icon name="broken_image" size={28} /></div>
        ) : (
          <Skeleton className="aspect-[4/3] w-full rounded-none" />
        )
      ) : (
        <button type="button" onClick={() => downloadAttachment(inspectionId, attachment.id, attachment.fileName)}
          className="flex aspect-[4/3] w-full flex-col items-center justify-center gap-2 bg-surface-container-low text-on-surface-variant hover:bg-surface-container">
          <Icon name="picture_as_pdf" size={32} /> <span className="text-body-sm">Download PDF</span>
        </button>
      )}
      <figcaption className="flex items-center justify-between gap-2 px-3 py-2 text-body-sm">
        <span className="truncate text-ink">{ANGLE_LABEL[attachment.angle]}</span>
        <span className="flex items-center gap-1 print:hidden">
          <button type="button" onClick={() => downloadAttachment(inspectionId, attachment.id, attachment.fileName)} aria-label={`Download ${attachment.fileName}`} className="text-secondary hover:text-ink"><Icon name="download" size={18} /></button>
          {onRemove && <button type="button" onClick={onRemove} aria-label={`Remove ${attachment.fileName}`} className="text-secondary hover:text-danger"><Icon name="delete" size={18} /></button>}
        </span>
      </figcaption>
    </figure>
  )
}
