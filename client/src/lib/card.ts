/**
 * Instant feedback for the card form. The server (mock hosted fields) re-validates; this only saves
 * a round trip. Nothing here stores or logs card data.
 */
export function cardDigits(raw: string): string {
  return raw.replace(/\D/g, '').slice(0, 19)
}

/** "4242424242424242" -> "4242 4242 4242 4242" */
export function formatCardNumber(raw: string): string {
  return cardDigits(raw).replace(/(\d{4})(?=\d)/g, '$1 ')
}

/** "1234" -> "12/34" while typing */
export function formatExpiry(raw: string): string {
  const digits = raw.replace(/\D/g, '').slice(0, 4)
  return digits.length > 2 ? `${digits.slice(0, 2)}/${digits.slice(2)}` : digits
}

export function passesLuhn(digits: string): boolean {
  let sum = 0
  for (let i = 0; i < digits.length; i++) {
    let digit = Number(digits[digits.length - 1 - i])
    if (i % 2 === 1 && (digit *= 2) > 9) digit -= 9
    sum += digit
  }
  return digits.length >= 12 && sum % 10 === 0
}

export function cardBrand(digits: string): string {
  if (/^4/.test(digits)) return 'Visa'
  if (/^5[1-5]/.test(digits)) return 'Mastercard'
  if (/^3[47]/.test(digits)) return 'Amex'
  return 'Card'
}

export interface CardFormErrors {
  number?: string
  expiry?: string
  cvc?: string
}

export function validateCard(card: { number: string; expiry: string; cvc: string }, today = new Date()): CardFormErrors {
  const errors: CardFormErrors = {}
  const digits = cardDigits(card.number)
  if (!passesLuhn(digits)) errors.number = 'Card number is not valid.'
  const match = /^(\d{2})\/(\d{2})$/.exec(card.expiry)
  if (!match || Number(match[1]) < 1 || Number(match[1]) > 12) errors.expiry = 'Use MM/YY.'
  else if (new Date(2000 + Number(match[2]), Number(match[1]), 1) <= today) errors.expiry = 'This card has expired.'
  if (!/^\d{3,4}$/.test(card.cvc)) errors.cvc = '3 or 4 digits.'
  return errors
}

/** Sandbox cards the mock gateway understands (shown as a hint on the payment card). */
export const TEST_CARDS = [
  { number: '4242 4242 4242 4242', label: 'Visa: approved' },
  { number: '5555 5555 5555 4444', label: 'Mastercard: approved' },
  { number: '4000 0000 0000 0002', label: 'Declined (card_declined)' },
  { number: '4000 0000 0000 9995', label: 'Declined (insufficient_funds)' },
  { number: '4000 0000 0000 0077', label: 'Approved after 2 s (slow)' },
] as const
