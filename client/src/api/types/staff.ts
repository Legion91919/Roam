// The counter (Agent role): /api/agent/branches, /api/agent/reservations, /api/agent/customers,
// /api/rentals. Staff see a customer's name and email, never ID numbers or card data.
import type { VehicleCategory } from './common'
import type { SaveProfileRequest } from './customer'
import type { Cancellation, ReservationDetails } from './booking'
import type { InspectionSummary } from './inspections'
import type { PaymentOption, PaymentSummary } from './payments'

export interface CounterRental {
  rentalId: string
  status: 'Open' | 'Closed'
  vehicleRegistration: string
  startOdometerKm: number
  pickedUpAt: string
  dueBackAt: string
  isLate: boolean
}

/** Any customer's booking as counter staff see it (name and email, never ID numbers or card data). */
export interface CounterBooking {
  reservationId: string
  reference: string
  category: VehicleCategory
  status: ReservationDetails['status']
  customerName: string
  customerEmail: string
  pickupBranchId: string
  pickupBranchName: string
  returnBranchId: string
  returnBranchName: string
  pickupAt: string
  returnAt: string
  total: number
  currency: string
  payment: PaymentSummary | null
  rental: CounterRental | null
  cancellation?: Cancellation | null
  inspections?: InspectionSummary[] | null
  vehicleId?: string | null
  paymentOption?: PaymentOption
}

export interface CheckOutRequest {
  reservationId: string
  vehicleId: string
  freeUpgrade: boolean
}

export interface ReturnResult {
  rentalId: string
  distanceKm: number
  lateFee: number
  currency: string
  returnedAt: string
  amountDue: number
  payment: PaymentSummary | null
}

export interface RegisterCustomerRequest extends SaveProfileRequest {
  email: string
}

export interface RegisteredCustomer {
  customerId: string
  email: string
  temporaryPassword: string
}
