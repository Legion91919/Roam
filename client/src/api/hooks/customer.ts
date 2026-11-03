// The signed-in user: driver profile, account (roles) and where to land after signing in.
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { withNext } from '../../lib/router'
import { api, ApiError } from '../client'
import type { Account, CustomerProfile, SaveProfileRequest } from '../types'
import { queryKeys } from './queryKeys'

async function fetchProfile(): Promise<CustomerProfile | null> {
  try {
    return await api<CustomerProfile>('/api/customers/me')
  } catch (error) {
    if (error instanceof ApiError && error.status === 404) return null
    throw error
  }
}

/** The signed-in user's driver profile; `null` when they haven't created one yet (API returns 404). */
export function useProfile(enabled: boolean) {
  return useQuery({ queryKey: queryKeys.profile, queryFn: fetchProfile, enabled, retry: false })
}

/** Email + roles from the token. Lets the staff app check the Agent role (the API checks it again on every call). */
export function useAccount(enabled: boolean) {
  return useQuery({ queryKey: queryKeys.account, queryFn: () => api<Account>('/api/account/me'), enabled, retry: false, staleTime: 5 * 60 * 1000 })
}

/** Reads the signed-in account (email + roles) on demand, e.g. right after signing in. Cached as `account`. */
export function useFetchAccount() {
  const queryClient = useQueryClient()
  return () => queryClient.fetchQuery({ queryKey: queryKeys.account, queryFn: () => api<Account>('/api/account/me') })
}

/**
 * Where to go right after signing in or creating an account: staff to the staff app; a customer
 * without a driver profile to the profile page first (then on to `next`); everyone else to `next`.
 * The answers are cached, so the page they land on doesn't ask again.
 */
export function useLandingAfterSignIn() {
  const queryClient = useQueryClient()
  const fetchAccount = useFetchAccount()
  return async (next: string): Promise<string> => {
    const account = await fetchAccount()
    if (account.roles.includes('Agent')) return '/agent'
    const profile = await queryClient.fetchQuery({ queryKey: queryKeys.profile, queryFn: fetchProfile })
    if (!profile) return next ? withNext('/profile', next) : '/profile'
    return next || '/'
  }
}

export function useSaveProfile() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (profile: SaveProfileRequest) => api<CustomerProfile>('/api/customers/me', { method: 'PUT', body: JSON.stringify(profile) }),
    onSuccess: (profile) => {
      queryClient.setQueryData(queryKeys.profile, profile)
      queryClient.invalidateQueries({ queryKey: ['quote'] }) // age/licence feed the price
    },
  })
}
