// Cache keys for every query. Shared, so a command in one area can invalidate another area's data
// (handing over the keys refreshes every ['agent', …] list). auth.tsx clears the per-user ones on sign-out.
import type { VehicleFilter } from './fleet'

export const queryKeys = {
  branches: ['branches'] as const,
  availability: (branchId: string, pickupAt: string, returnAt: string) => ['availability', branchId, pickupAt, returnAt] as const,
  reservation: (id: string) => ['reservation', id] as const,
  myReservations: ['reservations', 'mine'] as const,
  profile: ['profile'] as const,
  terms: ['terms'] as const,
  account: ['account'] as const,
  pickups: (branchId: string, date: string) => ['agent', 'pickups', branchId, date] as const,
  returns: (branchId: string, date: string) => ['agent', 'returns', branchId, date] as const,
  counterBooking: (reference: string) => ['agent', 'booking', reference] as const,
  vehicles: (filter: VehicleFilter) => ['agent', 'vehicles', filter] as const,
  vehicle: (id: string) => ['agent', 'vehicle', id] as const,
  vehicleHistory: (id: string) => ['agent', 'vehicle', id, 'history'] as const,
  availableVehicles: (reservationId: string, upgrades: boolean) => ['agent', 'available', reservationId, upgrades] as const,
  inspection: (id: string) => ['agent', 'inspection', id] as const,
  myInspections: (reservationId: string) => ['reservation', reservationId, 'inspections'] as const,
}
