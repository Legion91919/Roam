// Inspections: the staff stepper (start, save, photos, complete) and the customer's completed reports.
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, apiBlob, apiForm } from '../client'
import type { AttachmentAngle, Inspection, InspectionKind, UpdateInspectionRequest } from '../types'
import { queryKeys } from './queryKeys'

export function useInspection(id: string | null) {
  return useQuery({ queryKey: queryKeys.inspection(id ?? ''), queryFn: () => api<Inspection>(`/api/agent/inspections/${id}`), enabled: Boolean(id) })
}

/** Start or resume the booking's report of this kind (the API returns the existing draft if there is one). */
export function useStartInspection() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (request: { reservationId: string; kind: InspectionKind; vehicleId: string | null }) =>
      api<Inspection>('/api/agent/inspections', { method: 'POST', body: JSON.stringify(request) }),
    onSuccess: (report) => queryClient.setQueryData(queryKeys.inspection(report.id), report),
  })
}

export function useUpdateInspection(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (request: UpdateInspectionRequest) => api<Inspection>(`/api/agent/inspections/${id}`, { method: 'PUT', body: JSON.stringify(request) }),
    onSuccess: (report) => queryClient.setQueryData(queryKeys.inspection(id), report),
  })
}

export function useUploadAttachment(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ file, angle }: { file: File; angle: AttachmentAngle }) => {
      const form = new FormData()
      form.append('file', file)
      form.append('angle', angle)
      return apiForm<Inspection>(`/api/agent/inspections/${id}/attachments`, form)
    },
    onSuccess: (report) => queryClient.setQueryData(queryKeys.inspection(id), report),
  })
}

export function useRemoveAttachment(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (attachmentId: string) => api<Inspection>(`/api/agent/inspections/${id}/attachments/${attachmentId}`, { method: 'DELETE' }),
    onSuccess: (report) => queryClient.setQueryData(queryKeys.inspection(id), report),
  })
}

export function useCompleteInspection(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: () => api<Inspection>(`/api/agent/inspections/${id}/complete`, { method: 'POST' }),
    onSuccess: (report) => {
      queryClient.setQueryData(queryKeys.inspection(id), report)
      queryClient.invalidateQueries({ queryKey: ['agent', 'booking'] })
    },
  })
}

/** Customer: the COMPLETED reports of their own booking (404 if it isn't theirs). */
export function useMyInspections(reservationId: string, enabled: boolean) {
  return useQuery({
    queryKey: queryKeys.myInspections(reservationId),
    queryFn: () => api<Inspection[]>(`/api/reservations/${reservationId}/inspections`),
    enabled,
  })
}

export function useAcknowledgeInspection(reservationId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (inspectionId: string) => api<void>(`/api/reservations/${reservationId}/inspections/${inspectionId}/acknowledge`, { method: 'POST' }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: queryKeys.myInspections(reservationId) }),
  })
}

/** A protected photo as an object URL (revoked when the component unmounts). */
export function useAttachmentUrl(inspectionId: string, attachmentId: string) {
  const query = useQuery({
    queryKey: ['attachment', inspectionId, attachmentId],
    queryFn: async () => URL.createObjectURL(await apiBlob(`/api/inspections/${inspectionId}/attachments/${attachmentId}`)),
    staleTime: Infinity,
    gcTime: 5 * 60 * 1000,
  })
  return query
}

export async function downloadAttachment(inspectionId: string, attachmentId: string, fileName: string) {
  const url = URL.createObjectURL(await apiBlob(`/api/inspections/${inspectionId}/attachments/${attachmentId}`))
  const link = document.createElement('a')
  link.href = url
  link.download = fileName
  link.click()
  setTimeout(() => URL.revokeObjectURL(url), 10_000)
}
