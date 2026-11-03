import type { ProblemDetails } from './types'

/**
 * Error carrying the API's machine-readable `code` (e.g. "inventory.sold_out"), so the UI can
 * react to the reason without parsing English text.
 */
export class ApiError extends Error {
  readonly status: number
  readonly code: string
  readonly errors?: Record<string, string[]>
  readonly reason?: string

  constructor(problem: ProblemDetails) {
    super(problem.detail ?? problem.title)
    this.status = problem.status
    this.code = problem.code ?? (problem.status === 401 ? 'auth.required' : problem.errors ? 'validation.failed' : 'unknown')
    this.errors = problem.errors
    this.reason = problem.reason
  }
}

const baseUrl = import.meta.env.VITE_API_URL ?? ''

// ---- Bearer token (set by AuthProvider after POST /api/auth/login) ----
let accessToken: string | null = null
export function setAccessToken(token: string | null) {
  accessToken = token
}
/** AuthProvider listens for this to sign the user out when the token is rejected. */
export const UNAUTHORIZED_EVENT = 'roam:unauthorized'

/** Thin fetch wrapper: JSON in, JSON out, bearer token attached, ProblemDetails -> ApiError. */
export async function api<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`${baseUrl}${path}`, {
    ...init,
    headers: {
      'Content-Type': 'application/json',
      Accept: 'application/json',
      ...(accessToken ? { Authorization: `Bearer ${accessToken}` } : {}),
      ...init?.headers,
    },
  })

  if (!response.ok) {
    const problem: ProblemDetails = await response
      .json()
      .catch(() => ({ status: response.status, title: response.statusText }))
    if (response.status === 401 && accessToken) window.dispatchEvent(new Event(UNAUTHORIZED_EVENT))
    throw new ApiError({ ...problem, status: response.status })
  }

  const text = await response.text()
  return (text ? JSON.parse(text) : undefined) as T
}

async function throwProblem(response: Response): Promise<never> {
  const problem: ProblemDetails = await response.json().catch(() => ({ status: response.status, title: response.statusText }))
  if (response.status === 401 && accessToken) window.dispatchEvent(new Event(UNAUTHORIZED_EVENT))
  throw new ApiError({ ...problem, status: response.status })
}

/** multipart/form-data upload (the browser sets the boundary, so no Content-Type header here). */
export async function apiForm<T>(path: string, form: FormData): Promise<T> {
  const response = await fetch(`${baseUrl}${path}`, {
    method: 'POST',
    body: form,
    headers: { Accept: 'application/json', ...(accessToken ? { Authorization: `Bearer ${accessToken}` } : {}) },
  })
  if (!response.ok) await throwProblem(response)
  return (await response.json()) as T
}

/**
 * A protected file as a Blob. <img src> can't send the bearer token, so photos are fetched here and
 * shown through an object URL; the API still decides who may see them (owner after completion, staff).
 */
export async function apiBlob(path: string): Promise<Blob> {
  const response = await fetch(`${baseUrl}${path}`, { headers: accessToken ? { Authorization: `Bearer ${accessToken}` } : {} })
  if (!response.ok) await throwProblem(response)
  return response.blob()
}

/** Friendly copy for the domain error codes the API can return (see ApiExceptionHandler). */
export const errorMessages: Record<string, { title: string; message: string }> = {
  'inventory.sold_out': { title: 'Inventory collision', message: 'Sorry, that category is sold out on at least one of your days.' },
  'inventory.not_offered': { title: 'Not offered', message: 'That category is not offered at this branch for those dates.' },
  'concurrency.conflict': { title: 'Real-time stock exhausted', message: 'Someone else just booked the last car. Please search again.' },
  'eligibility.under_age': { title: 'Driver not eligible', message: 'Drivers must be at least 21 on the pickup date.' },
  'eligibility.licence_too_new': { title: 'Driver not eligible', message: 'The driver must have held a licence for at least one year.' },
  'eligibility.luxury_under_age': { title: 'Driver not eligible', message: 'Luxury vehicles require a driver aged 25 or older.' },
  'reservation.pickup_in_past': { title: 'Check your dates', message: 'Pickup time is in the past.' },
  'reservation.not_cancellable': { title: 'Cannot cancel', message: 'This reservation can no longer be cancelled.' },
  'reservation.too_late_to_cancel': { title: 'Cannot cancel', message: 'Reservations can only be cancelled before the pickup time.' },
  'terms.not_accepted': { title: 'Terms not accepted', message: 'Please read and accept the rental terms to book.' },
  'terms.outdated': { title: 'Terms updated', message: 'The rental terms changed. Please review and accept the current version.' },
  'customer.profile_required': { title: 'Profile needed', message: 'Complete your driver profile before booking.' },
  'customer.birth_date_mismatch': { title: 'Check your details', message: 'Your date of birth does not match your ID number.' },
  'customer.document_in_use': { title: 'Already registered', message: 'This ID or passport is already linked to another account.' },
  'identity.invalid_sa_id': { title: 'Invalid ID number', message: 'That South African ID number is not valid. Check for a typing error.' },
  'identity.invalid_passport': { title: 'Invalid passport', message: 'Passport numbers are 6 to 9 letters or digits.' },
  'identity.invalid_country': { title: 'Invalid country', message: 'Use the 3-letter country code from your passport, e.g. GBR.' },
  'customer.invalid_phone': { title: 'Invalid phone number', message: 'Enter a valid mobile number.' },
  'driver.licence_in_future': { title: 'Check your licence date', message: 'The licence issue date cannot be in the future.' },
  'branch.not_found': { title: 'Unknown branch', message: 'The selected return branch does not exist.' },
  'payment.declined': { title: 'Card declined', message: 'Your bank declined the card. Nothing was booked: try another card.' },
  'payment.invalid_card': { title: 'Check the card details', message: 'The card number, expiry or CVC is not valid.' },
  'payment.capture_exceeds_authorization': { title: 'Amount above the card hold', message: 'The total with late fees is more than 15% above the amount held on the card. Take the balance as a separate payment.' },
  'payment.invalid_transition': { title: 'Payment already settled', message: 'This payment was already captured, released or failed.' },
  'payment.invalid_amount': { title: 'Invalid amount', message: 'A payment amount must be greater than zero.' },
  'money.currency_mismatch': { title: 'Currency mismatch', message: 'Amounts in different currencies cannot be combined.' },
  'account.email_in_use': { title: 'Email already registered', message: 'An account with this email already exists. Ask the customer to sign in instead.' },
  'rental.too_early': { title: 'Too early', message: 'The car can be collected at most 2 hours before the pickup time.' },
  'rental.reservation_expired': { title: 'Booking period over', message: 'The reservation period has already ended.' },
  'rental.registration_required': { title: 'Registration needed', message: 'Enter the vehicle registration.' },
  'rental.negative_odometer': { title: 'Check the odometer', message: 'The odometer reading cannot be negative.' },
  'rental.odometer_decreased': { title: 'Check the odometer', message: 'The odometer cannot be lower than at checkout.' },
  'rental.already_returned': { title: 'Already returned', message: 'This car has already been returned.' },
  'rental.return_before_pickup': { title: 'Check the time', message: 'The return time cannot be before the pickup time.' },
  'reservation.not_checkoutable': { title: 'Cannot check out', message: 'Only a confirmed reservation can be checked out.' },
  'customer.name_required': { title: 'Name required', message: 'Enter the first name and surname.' },
  'customer.invalid_email': { title: 'Invalid email', message: 'Enter a valid email address.' },
  'driver.licence_before_birth': { title: 'Check the licence date', message: 'The licence cannot be issued before the date of birth.' },
  'auth.required': { title: 'Sign in required', message: 'Please sign in to continue.' },
  'resource.not_found': { title: 'Not found', message: 'We could not find that booking.' },
  'validation.failed': { title: 'Check your details', message: 'Some details are missing or invalid.' },
  // Fleet
  'vehicle.invalid_registration': { title: 'Check the registration', message: 'Use the plate as on the licence disc, e.g. CA 123-456 or ND 45678.' },
  'vehicle.invalid_vin': { title: 'Check the VIN', message: 'A VIN is 17 letters and digits (never I, O or Q).' },
  'vehicle.too_old': { title: 'Too old for the fleet', message: 'Rental cars may be at most 10 years old.' },
  'vehicle.invalid_year': { title: 'Check the year', message: 'The model year cannot be in the future.' },
  'vehicle.duplicate_registration': { title: 'Already in the fleet', message: 'A car with this registration is already registered.' },
  'vehicle.duplicate_vin': { title: 'Already in the fleet', message: 'A car with this VIN is already registered.' },
  'vehicle.not_available': { title: 'Car not available', message: 'This car is on rent, in service or retired.' },
  'vehicle.on_rent': { title: 'Car is on rent', message: 'Take the return first.' },
  'vehicle.odometer_decreased': { title: 'Check the odometer', message: 'The odometer cannot be lower than the last reading.' },
  'vehicle.reason_required': { title: 'Reason needed', message: 'Give a reason for this change.' },
  'vehicle.retired': { title: 'Retired', message: 'A retired car cannot change status.' },
  'vehicle.not_in_service': { title: 'Not in service', message: 'This car is not in service.' },
  'vehicle.not_on_rent': { title: 'Not on rent', message: 'This car is not out on a rental.' },
  'vehicle.make_required': { title: 'Make required', message: 'Enter the make, e.g. Toyota.' },
  'vehicle.model_required': { title: 'Model required', message: 'Enter the model, e.g. Corolla Cross.' },
  'vehicle.negative_odometer': { title: 'Check the odometer', message: 'The odometer reading cannot be negative.' },
  'inventory.negative_capacity': { title: 'Capacity error', message: 'A branch cannot have fewer than zero cars in a category.' },
  'inventory.nothing_to_release': { title: 'Nothing to release', message: 'These days hold no booked car to give back.' },
  'reservation.negative_total': { title: 'Price error', message: 'The booking total cannot be negative.' },
  'inventory.capacity_below_reserved': { title: 'Cars already promised', message: 'Customers have bookings that need this car. It can be retired once those days are past or covered.' },
  // Checkout / return
  'rental.inspection_required': { title: 'Inspection needed', message: 'Complete the inspection of this car before handing over or taking back the keys.' },
  'rental.category_mismatch': { title: 'Wrong category', message: 'Choose a car in the booked category, or tick free upgrade.' },
  'rental.vehicle_wrong_branch': { title: 'Car is elsewhere', message: 'This car is not at the pickup branch.' },
  'rental.wrong_vehicle': { title: 'Wrong car', message: 'This rental is for a different car.' },
  // Inspections
  'inspection.incomplete': { title: 'Inspection incomplete', message: 'Answer every checklist item and record the odometer and fuel level.' },
  'inspection.note_required': { title: 'Describe the problem', message: 'Damaged or missing items need a short note.' },
  'inspection.photos_required': { title: 'Photos needed', message: 'A checkout report needs photos of the front, back, left and right.' },
  'inspection.driver_not_verified': { title: 'Driver not verified', message: "Check the driver's ID and licence before handing over the keys." },
  'inspection.locked': { title: 'Report locked', message: 'A completed inspection is evidence and cannot be changed.' },
  'inspection.too_many_attachments': { title: 'Too many files', message: 'A report can have at most 20 photos and files.' },
  'inspection.checkout_report_required': { title: 'Checkout report missing', message: 'The completed checkout report is needed to compare damage.' },
  'inspection.rental_required': { title: 'Not checked out', message: 'This booking has not been checked out yet.' },
  'inspection.note_too_long': { title: 'Note too long', message: 'Notes are at most 300 characters.' },
  'inspection.invalid_fuel': { title: 'Check the fuel level', message: 'Fuel is recorded in eighths, from empty (0) to full (8).' },
  'inspection.invalid_odometer': { title: 'Check the odometer', message: 'The odometer reading cannot be negative.' },
  'inspection.damage_panel_required': { title: 'Damage needs a panel', message: 'Say where the damage is.' },
  'inspection.damage_description_required': { title: 'Describe the damage', message: 'Add a short description of the damage.' },
  'inspection.not_completed': { title: 'Not completed', message: 'Only a completed report can be acknowledged.' },
  'inspection.attachment_not_found': { title: 'File not found', message: 'That photo or file is not on this report.' },
  'inspection.invalid_checklist': { title: 'Check the checklist', message: 'Every checklist item must be answered exactly once.' },
  'inspection.rental_mismatch': { title: 'Wrong rental', message: 'This return report belongs to a different rental.' },
  'file.type_not_allowed': { title: 'File type not allowed', message: 'Only JPEG and PNG photos and PDF documents can be uploaded.' },
  'file.too_large': { title: 'File too large', message: 'Files can be at most 10 MB.' },
  'file.empty': { title: 'Empty file', message: 'The file is empty.' },
  // Pay at the branch
  'rental.payment_required': { title: 'Payment needed', message: 'This booking is paid at the branch: take the card payment before handing over the keys.' },
  'payment.paid_online': { title: 'Already paid online', message: 'This booking was paid by card online; the hold is already on the card.' },
  'payment.already_taken': { title: 'Already paid', message: 'The payment for this booking has already been taken.' },
  'reservation.not_payable': { title: 'Cannot take payment', message: 'Only a confirmed booking can be paid for.' },
  'reservation.invalid_payment_option': { title: 'Choose how to pay', message: 'Pay by card now or at the branch.' },
  // Cancellation
  'reservation.cancel_reason_required': { title: 'Reason needed', message: 'Give the reason the customer will see.' },
  'reservation.no_show_too_early': { title: 'Too early for a no-show', message: 'A no-show can be recorded from 2 hours after the pickup time.' },
  network: { title: 'Network interruption', message: "We can't reach the booking service. Is the API running on port 5080?" },
}

export interface DescribedError {
  code: string
  title: string
  message: string
  retryable: boolean
}

/** Why a card was declined, in the customer's words (gateway reason codes). */
export const declineReasons: Record<string, string> = {
  card_declined: 'The card issuer declined the payment.',
  insufficient_funds: 'There are not enough funds available on the card.',
  invalid_token: 'The card details expired. Enter them again.',
}

export function describeError(error: unknown): DescribedError {
  if (error instanceof ApiError && error.status < 500) {
    const known = errorMessages[error.code]
    const fieldErrors = error.errors ? Object.values(error.errors).flat().join(' ') : ''
    const decline = error.reason ? `${declineReasons[error.reason] ?? `Reason: ${error.reason}.`} Nothing was booked.` : ''
    return {
      code: error.code,
      title: known?.title ?? 'Request failed',
      message: decline || fieldErrors || known?.message || error.message,
      retryable: false,
    }
  }
  return { code: 'network', ...errorMessages.network, retryable: true }
}
