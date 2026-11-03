// Sample data for Storybook stories only (never imported by the app). Fictional people and cards.
import type { CategoryAvailability, CounterBooking, Inspection, Quote, Vehicle } from '../api/types'
import { CHECKLIST_ORDER } from '../components/inspection/inspectionText'

const day = (offset: number, hour: number) => {
  const d = new Date('2026-11-10T00:00:00+02:00')
  d.setDate(d.getDate() + offset)
  d.setHours(hour, 0, 0, 0)
  return d.toISOString()
}

export const availability: CategoryAvailability[] = [
  { category: 'Economy', availableCars: 6, dailyRate: 390, currency: 'ZAR' },
  { category: 'Compact', availableCars: 2, dailyRate: 450, currency: 'ZAR' },
  { category: 'Suv', availableCars: 0, dailyRate: 750, currency: 'ZAR' },
]

export const quote: Quote = {
  chargeableDays: 3,
  dailyRate: 450,
  total: 1489.5,
  currency: 'ZAR',
  lines: [
    { code: 'BASE', description: 'Compact × 3 days', amount: 1350 },
    { code: 'GPS', description: 'GPS navigation', amount: 180 },
    { code: 'LOYALTY', description: 'Gold member discount', amount: -40.5 },
  ],
}

export const booking: CounterBooking = {
  reservationId: '7c3e5a10-1111-4abc-9def-000000000001',
  reference: 'ROAM-7C3E-5A10-1111-4ABC',
  category: 'Compact',
  status: 'Confirmed',
  customerName: 'Thabo Nkosi',
  customerEmail: 'thabo@example.com',
  pickupBranchId: 'b1',
  pickupBranchName: 'King Shaka International Airport',
  returnBranchId: 'b1',
  returnBranchName: 'King Shaka International Airport',
  pickupAt: day(0, 9),
  returnAt: day(3, 9),
  total: 1350,
  currency: 'ZAR',
  payment: { status: 'Authorized', cardBrand: 'Visa', cardLast4: '4242', authorized: 1350, captured: 0, currency: 'ZAR', failureReason: null },
  rental: null,
  paymentOption: 'Card',
}

export const bookings: CounterBooking[] = [
  booking,
  { ...booking, reservationId: 'r2', reference: 'ROAM-5B1D-0C2E-4444-4ABC', customerName: 'Ayesha Patel', customerEmail: 'ayesha@example.com', pickupAt: day(0, 11), payment: null, paymentOption: 'PayAtBranch' },
  {
    ...booking, reservationId: 'r3', reference: 'ROAM-2F0D-6C1E-9B8A-4C7D', category: 'Suv', status: 'CheckedOut', customerName: 'Johan van Wyk', customerEmail: 'johan@example.com', pickupAt: day(0, 14),
    rental: { rentalId: 'rent3', status: 'Open', vehicleRegistration: 'ND 123-456', startOdometerKm: 18420, pickedUpAt: day(0, 14), dueBackAt: day(3, 14), isLate: false },
  },
]

export const inspection: Inspection = {
  id: 'insp1',
  reservationId: booking.reservationId,
  rentalId: 'rent3',
  vehicleId: 'v1',
  vehicleRegistration: 'ND 882-31',
  kind: 'Return',
  status: 'Completed',
  inspectedByName: 'Thandi Mokoena',
  startedAt: day(3, 9),
  inspectedAt: day(3, 9),
  odometerKm: 6640,
  fuelEighths: 6,
  checklist: CHECKLIST_ORDER.map((code) => code === 'ExteriorRear'
    ? { code, status: 'Damaged', note: '5 cm scratch on the rear bumper' }
    : { code, status: code === 'DriverIdAndLicenceVerified' ? 'NotApplicable' : 'Ok', note: null }),
  damage: [{ panel: 'Rear bumper', description: '5 cm scratch', severity: 'Minor' }],
  attachments: [],
  newDamage: [{ item: 'ExteriorRear', panel: 'Rear', description: '5 cm scratch on the rear bumper', severity: 'Minor' }],
  customerAcknowledgedAt: null,
}

export const vehicle: Vehicle = {
  id: 'v1',
  registration: 'ND 123-456',
  vin: 'AHTKB3CD8023456A1',
  make: 'Toyota',
  model: 'Fortuner 2.8 GD-6',
  year: 2024,
  category: 'Suv',
  transmission: 'Automatic',
  status: 'Available',
  homeBranchId: 'b1',
  homeBranchName: 'King Shaka International Airport',
  currentBranchId: 'b1',
  currentBranchName: 'King Shaka International Airport',
  odometerKm: 18420,
  createdAt: '2026-01-10T08:00:00+02:00',
  currentRentalId: null,
  currentReservationId: null,
}
