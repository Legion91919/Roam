// The counter (Agent role): today's pickups and returns, finding a booking, payment at the branch,
// handing over and taking back the car, cancel / no-show, walk-in customers.
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, ApiError } from '../client'
import type { CheckOutRequest, CounterBooking, PaymentSummary, RegisterCustomerRequest, RegisteredCustomer, ReturnResult } from '../types'
import { queryKeys } from './queryKeys'

export function useBranchPickups(branchId: string, date: string) {
  return useQuery({
    queryKey: queryKeys.pickups(branchId, date),
    queryFn: () => api<CounterBooking[]>(`/api/agent/branches/${branchId}/pickups?date=${date}`),
    enabled: Boolean(branchId && date),
  })
}

export function useBranchReturns(branchId: string, date: string) {
  return useQuery({
    queryKey: queryKeys.returns(branchId, date),
    queryFn: () => api<CounterBooking[]>(`/api/agent/branches/${branchId}/returns?date=${date}`),
    enabled: Boolean(branchId && date),
  })
}

/** Any customer's booking by ROAM-XXXX-XXXX-XXXX-XXXX reference or id; `null` when not found. */
export function useCounterBooking(reference: string) {
  return useQuery({
    queryKey: queryKeys.counterBooking(reference),
    queryFn: async () => {
      try {
        return await api<CounterBooking>(`/api/agent/reservations/${encodeURIComponent(reference)}`)
      } catch (error) {
        if (error instanceof ApiError && error.status === 404) return null
        throw error
      }
    },
    enabled: Boolean(reference),
    retry: false,
  })
}

export function useAgentCancel() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ reservationId, reason }: { reservationId: string; reason: string }) =>
      api<void>(`/api/agent/reservations/${reservationId}/cancel`, { method: 'POST', body: JSON.stringify({ reason }) }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['agent'] }),
  })
}

export function useMarkNoShow() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (reservationId: string) => api<void>(`/api/agent/reservations/${reservationId}/no-show`, { method: 'POST' }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['agent'] }),
  })
}

/**
 * Pay at the branch: hold the booking total on the customer's card at the counter. The card goes to
 * the gateway's hosted fields first (useTokenizeCard); only the single-use token is sent here.
 */
export function useTakeCounterPayment() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ reservationId, paymentToken }: { reservationId: string; paymentToken: string }) =>
      api<PaymentSummary>(`/api/agent/reservations/${reservationId}/payment`, { method: 'POST', body: JSON.stringify({ paymentToken }) }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['agent'] }),
  })
}

/** Hand over the keys: the reservation becomes a rental. */
export function useCheckOut() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (request: CheckOutRequest) => api<{ rentalId: string }>('/api/rentals', { method: 'POST', body: JSON.stringify(request) }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['agent'] }),
  })
}

/** Take the car back: late fee and payment capture come back from the server. */
export function useReturnVehicle() {
  const queryClient = useQueryClient()
  return useMutation({
    // The end odometer comes from the completed return inspection: no body.
    mutationFn: (rentalId: string) => api<ReturnResult>(`/api/rentals/${rentalId}/return`, { method: 'POST' }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['agent'] }),
  })
}

/** Walk-in customer: account + driver profile; the temporary password comes back once. */
export function useRegisterCustomer() {
  return useMutation({
    mutationFn: (request: RegisterCustomerRequest) =>
      api<RegisteredCustomer>('/api/agent/customers', { method: 'POST', body: JSON.stringify(request) }),
    gcTime: 0,
  })
}
