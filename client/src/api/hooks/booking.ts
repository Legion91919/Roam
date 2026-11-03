// Searching, pricing and booking (customer).
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../client'
import type { Branch, CategoryAvailability, CreateReservationRequest, CreateReservationResult, Quote, ReservationDetails, TermsDocument, TripDetails } from '../types'
import { queryKeys } from './queryKeys'

export function useBranches() {
  return useQuery({
    queryKey: queryKeys.branches,
    queryFn: () => api<Branch[]>('/api/branches'),
    staleTime: 60 * 60 * 1000, // reference data: an hour is fine
  })
}

export function useAvailability(branchId: string, pickupAt: string, returnAt: string) {
  return useQuery({
    queryKey: queryKeys.availability(branchId, pickupAt, returnAt),
    queryFn: () => {
      const params = new URLSearchParams({ branchId, pickupAt, returnAt })
      return api<CategoryAvailability[]>(`/api/availability?${params}`)
    },
    enabled: Boolean(branchId && pickupAt && returnAt && returnAt > pickupAt),
    placeholderData: keepPreviousData,
    staleTime: 15 * 1000,
  })
}

/** The price for the signed-in customer (age, licence and loyalty from their profile). Re-fetches when the trip changes. */
export function useQuote(trip: TripDetails | null) {
  return useQuery({
    queryKey: ['quote', trip],
    queryFn: () => api<Quote>('/api/quotes', { method: 'POST', body: JSON.stringify(trip) }),
    enabled: trip !== null,
    placeholderData: keepPreviousData,
    retry: false,
  })
}

export function useTerms() {
  return useQuery({ queryKey: queryKeys.terms, queryFn: () => api<TermsDocument>('/api/terms/current'), staleTime: 10 * 60 * 1000 })
}

export function useCreateReservation() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (request: CreateReservationRequest) =>
      api<CreateReservationResult>('/api/reservations', { method: 'POST', body: JSON.stringify(request) }),
    onSettled: () => {
      queryClient.invalidateQueries({ queryKey: ['availability'] })
      queryClient.invalidateQueries({ queryKey: queryKeys.myReservations })
    },
  })
}

export function useMyReservations(enabled: boolean) {
  return useQuery({ queryKey: queryKeys.myReservations, queryFn: () => api<ReservationDetails[]>('/api/reservations/mine'), enabled })
}

export function useReservation(id: string | undefined) {
  return useQuery({
    queryKey: queryKeys.reservation(id ?? ''),
    queryFn: () => api<ReservationDetails>(`/api/reservations/${id}`),
    enabled: Boolean(id),
    retry: false,
  })
}

export function useCancelReservation() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => api<void>(`/api/reservations/${id}/cancel`, { method: 'POST' }),
    onSuccess: (_data, id) => {
      queryClient.invalidateQueries({ queryKey: queryKeys.reservation(id) })
      queryClient.invalidateQueries({ queryKey: queryKeys.myReservations })
      queryClient.invalidateQueries({ queryKey: ['availability'] })
    },
  })
}
