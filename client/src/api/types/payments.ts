// The mock payment gateway: /api/payments/tokenize, and what the API says about a booking's payment.
// The API never returns more card data than the brand and last four digits.

/** Card: the total is held on the card when booking. PayAtBranch: the agent takes the card at the counter. */
export type PaymentOption = 'Card' | 'PayAtBranch'

export type PaymentStatus = 'Pending' | 'Authorized' | 'Captured' | 'Voided' | 'Failed'

/** Brand + last four digits only: the API never returns more card data than this. */
export interface PaymentSummary {
  status: PaymentStatus
  cardBrand: string
  cardLast4: string
  authorized: number
  captured: number
  currency: string
  failureReason: string | null
}

export interface CardDetails {
  number: string
  expiry: string // MM/YY
  cvc: string
}

export interface CardToken {
  token: string
  brand: string
  last4: string
}
