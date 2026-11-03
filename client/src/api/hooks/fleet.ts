// Cars (Agent role): the fleet list, a car's history, registering a car and status changes.
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../client'
import type { AvailableVehicle, Paged, RegisterVehicleRequest, Vehicle, VehicleCategory, VehicleHistory, VehicleListItem, VehicleStatus } from '../types'
import { queryKeys } from './queryKeys'

/** Filters for GET /api/agent/vehicles (kept in the page's state and in the cache key). */
export interface VehicleFilter {
  branchId?: string
  category?: VehicleCategory | ''
  status?: VehicleStatus | ''
  search?: string
  page?: number
}

export function useVehicles(filter: VehicleFilter) {
  return useQuery({
    queryKey: queryKeys.vehicles(filter),
    queryFn: () => {
      const params = new URLSearchParams({ page: String(filter.page ?? 1), pageSize: '25' })
      if (filter.branchId) params.set('branchId', filter.branchId)
      if (filter.category) params.set('category', filter.category)
      if (filter.status) params.set('status', filter.status)
      if (filter.search?.trim()) params.set('search', filter.search.trim())
      return api<Paged<VehicleListItem>>(`/api/agent/vehicles?${params}`)
    },
    placeholderData: keepPreviousData,
  })
}

export function useVehicleHistory(id: string) {
  return useQuery({ queryKey: queryKeys.vehicleHistory(id), queryFn: () => api<VehicleHistory>(`/api/agent/vehicles/${id}/history`), enabled: Boolean(id) })
}

export function useRegisterVehicle() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (request: RegisterVehicleRequest) => api<Vehicle>('/api/agent/vehicles', { method: 'POST', body: JSON.stringify(request) }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['agent', 'vehicles'] }),
  })
}

export type VehicleAction =
  | { kind: 'service'; reason: string }
  | { kind: 'serviceReturn'; odometerKm: number }
  | { kind: 'retire'; reason: string }

export function useVehicleAction(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (action: VehicleAction) => {
      const [path, body] =
        action.kind === 'service' ? ['service', { reason: action.reason }] :
        action.kind === 'serviceReturn' ? ['service/return', { odometerKm: action.odometerKm }] :
        ['retire', { reason: action.reason }]
      return api<Vehicle>(`/api/agent/vehicles/${id}/${path}`, { method: 'POST', body: JSON.stringify(body) })
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['agent'] }),
  })
}

/** Cars that can be handed over for this booking: at the branch, Available, in category (plus upgrades on request). */
export function useAvailableVehicles(reservationId: string, includeUpgrades: boolean) {
  return useQuery({
    queryKey: queryKeys.availableVehicles(reservationId, includeUpgrades),
    queryFn: () => api<AvailableVehicle[]>(`/api/agent/vehicles/available-for/${reservationId}?includeUpgrades=${includeUpgrades}`),
    enabled: Boolean(reservationId),
  })
}
