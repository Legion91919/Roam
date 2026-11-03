// The mock payment gateway's hosted fields (card -> single-use token).
import { useMutation } from '@tanstack/react-query'
import { api } from '../client'
import type { CardDetails, CardToken } from '../types'

/**
 * Card details -> single-use token. The ONLY request that ever carries a card number; the booking
 * request carries the token. Nothing here caches the card: mutation variables are dropped on reset.
 */
export function useTokenizeCard() {
  return useMutation({
    mutationFn: (card: CardDetails) => api<CardToken>('/api/payments/tokenize', { method: 'POST', body: JSON.stringify(card) }),
    gcTime: 0,
  })
}
