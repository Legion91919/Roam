// Searching, pricing and booking a car: /api/branches, /api/availability, /api/quotes,
// /api/terms, /api/reservations.
import type { VehicleCategory } from './common'
import type { PaymentOption, PaymentSummary } from './payments'

export type BranchType = 'Airport' | 'City'

export interface Branch {
  id: string
  code: string
  name: string
  city: string
  type: BranchType
  address: string
}

export interface CategoryAvailability {
  category: VehicleCategory
  availableCars: number
  dailyRate: number
  currency: string
}

export interface PriceLine {
  code: string
  description: string
  amount: number
}

export interface Quote {
  chargeableDays: number
  dailyRate: number
  total: number
  currency: string
  lines: PriceLine[]
}

/** What the customer chooses. Who drives (age, licence, loyalty) comes from their verified profile on the server. */
export interface TripDetails {
  category: VehicleCategory
  pickupBranchId: string
  returnBranchId: string
  pickupAt: string // ISO 8601 with offset
  returnAt: string
  extras: string // "None" | "Gps" | "ChildSeat" | "Gps, ChildSeat"
}

export interface CreateReservationRequest extends TripDetails {
  acceptedTermsVersion: string | null
  paymentOption: PaymentOption
  /** Card only: single-use token from POST /api/payments/tokenize. The card number itself never reaches the booking API. */
  paymentToken: string | null
}

export interface CreateReservationResult {
  reservationId: string
  price: Quote
  /** null when paying at the branch */
  payment: PaymentSummary | null
  paymentOption: PaymentOption
}

export type ReservationStatus = 'Confirmed' | 'Cancelled' | 'CheckedOut' | 'NoShow'

/** Who ended a booking and why, as the customer may see it (never a staff user id). */
export interface Cancellation {
  cancelledBy: 'Customer' | 'Agent'
  reason: string | null
  at: string
  noShow: boolean
}

export interface ReservationDetails {
  id: string
  category: VehicleCategory
  status: ReservationStatus
  pickupBranchId: string
  pickupBranchName: string
  returnBranchId: string
  returnBranchName: string
  pickupAt: string
  returnAt: string
  total: number
  currency: string
  driverName: string
  termsVersion: string
  createdAt: string
  payment: PaymentSummary | null
  cancellation: Cancellation | null
  hasInspectionReport: boolean
  paymentOption: PaymentOption
}

export interface TermsDocument {
  version: string
  title: string
  effectiveFrom: string
  sections: { heading: string; body: string }[]
}
