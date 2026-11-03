// Checkout and return inspections: /api/agent/inspections (staff) and
// /api/reservations/{id}/inspections (the customer's completed reports).

export type InspectionKind = 'Checkout' | 'Return'
export type ChecklistStatus = 'Unanswered' | 'Ok' | 'Damaged' | 'Missing' | 'NotApplicable'
export type ChecklistCode =
  | 'ExteriorFront' | 'ExteriorRear' | 'ExteriorLeft' | 'ExteriorRight' | 'Roof' | 'Windscreen' | 'WheelsAndTyres'
  | 'SpareWheelAndJack' | 'InteriorAndSeats' | 'WarningLights' | 'LicenceDiscValid' | 'DriverIdAndLicenceVerified'
export type AttachmentAngle = 'Front' | 'Back' | 'Left' | 'Right' | 'Interior' | 'Damage' | 'Document' | 'Other'
export type DamageSeverity = 'Minor' | 'Moderate' | 'Severe'

export interface ChecklistItem {
  code: ChecklistCode
  status: ChecklistStatus
  note: string | null
}

export interface DamageMark {
  panel: string
  description: string
  severity: DamageSeverity
}

export interface InspectionAttachment {
  id: string
  fileName: string
  contentType: string
  sizeBytes: number
  angle: AttachmentAngle
  uploadedAt: string
}

export interface NewDamage {
  item: string | null
  panel: string
  description: string
  severity: string | null
}

export interface Inspection {
  id: string
  reservationId: string
  rentalId: string | null
  vehicleId: string
  vehicleRegistration: string
  kind: InspectionKind
  status: 'Draft' | 'Completed'
  inspectedByName: string
  startedAt: string
  inspectedAt: string | null
  odometerKm: number | null
  fuelEighths: number | null
  checklist: ChecklistItem[]
  damage: DamageMark[]
  attachments: InspectionAttachment[]
  newDamage: NewDamage[]
  customerAcknowledgedAt: string | null
}

export interface InspectionSummary {
  id: string
  kind: InspectionKind
  status: 'Draft' | 'Completed'
  inspectedAt: string | null
  damagedItems: number
  newDamage: string[]
}

export interface UpdateInspectionRequest {
  odometerKm: number | null
  fuelEighths: number | null
  checklist: { code: ChecklistCode; status: ChecklistStatus; note: string | null }[]
  damage: DamageMark[]
}
