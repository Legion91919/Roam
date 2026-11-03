import type { AttachmentAngle, ChecklistCode, ChecklistStatus } from '../../api/types'

/** Display copy for the fixed checklist catalogue (the server owns the codes and the rules). */
export const CHECKLIST_LABEL: Record<ChecklistCode, string> = {
  ExteriorFront: 'Exterior: front',
  ExteriorRear: 'Exterior: rear',
  ExteriorLeft: 'Exterior: left side',
  ExteriorRight: 'Exterior: right side',
  Roof: 'Roof',
  Windscreen: 'Windscreen',
  WheelsAndTyres: 'Wheels and tyres',
  SpareWheelAndJack: 'Spare wheel and jack',
  InteriorAndSeats: 'Interior and seats',
  WarningLights: 'Dashboard warning lights',
  LicenceDiscValid: 'Licence disc valid',
  DriverIdAndLicenceVerified: "Driver's ID and licence verified",
}

export const CHECKLIST_ORDER = Object.keys(CHECKLIST_LABEL) as ChecklistCode[]

export const STATUS_LABEL: Record<ChecklistStatus, string> = {
  Unanswered: 'Not answered',
  Ok: 'OK',
  Damaged: 'Damaged',
  Missing: 'Missing',
  NotApplicable: 'N/A',
}

export const STATUS_TONE: Record<ChecklistStatus, 'success' | 'danger' | 'warning' | 'neutral'> = {
  Unanswered: 'neutral',
  Ok: 'success',
  Damaged: 'danger',
  Missing: 'warning',
  NotApplicable: 'neutral',
}

export const REQUIRED_ANGLES: AttachmentAngle[] = ['Front', 'Back', 'Left', 'Right']

export const ANGLE_LABEL: Record<AttachmentAngle, string> = {
  Front: 'Front', Back: 'Back', Left: 'Left side', Right: 'Right side', Interior: 'Interior', Damage: 'Damage close-up', Document: 'Document', Other: 'Other',
}
