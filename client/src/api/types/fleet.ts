// Cars (Agent role): /api/agent/vehicles.
import type { VehicleCategory } from './common'
import type { InspectionSummary } from './inspections'

export type VehicleStatus = 'Available' | 'OnRent' | 'InService' | 'Retired'
export type Transmission = 'Manual' | 'Automatic'

export interface VehicleListItem {
  id: string
  registration: string
  make: string
  model: string
  year: number
  category: VehicleCategory
  transmission: Transmission
  status: VehicleStatus
  currentBranchId: string
  currentBranchCode: string
  currentBranchName: string
  odometerKm: number
}

export interface Vehicle {
  id: string
  registration: string
  vin: string
  make: string
  model: string
  year: number
  category: VehicleCategory
  transmission: Transmission
  status: VehicleStatus
  homeBranchId: string
  homeBranchName: string
  currentBranchId: string
  currentBranchName: string
  odometerKm: number
  createdAt: string
  currentRentalId: string | null
  currentReservationId: string | null
}

export interface VehicleRentalHistory {
  rentalId: string
  reservationId: string
  reference: string
  customerName: string
  pickedUpAt: string
  returnedAt: string | null
  odometerOut: number
  odometerIn: number | null
  distanceKm: number | null
  lateFee: number
  currency: string
  inspections: InspectionSummary[]
}

export interface VehicleHistoryEvent {
  kind: 'Registered' | 'Rental' | 'SentToService' | 'ReturnedFromService' | 'MovedBranch' | 'Retired'
  at: string
  odometerKm: number | null
  branchName: string | null
  note: string | null
  rental: VehicleRentalHistory | null
}

export interface VehicleHistory {
  vehicle: Vehicle
  timeline: VehicleHistoryEvent[]
}

export interface AvailableVehicle {
  id: string
  registration: string
  make: string
  model: string
  year: number
  category: VehicleCategory
  transmission: Transmission
  odometerKm: number
  isUpgrade: boolean
}

export interface RegisterVehicleRequest {
  registration: string
  vin: string
  make: string
  model: string
  year: number
  category: VehicleCategory
  transmission: Transmission
  homeBranchId: string
  odometerKm: number
}
