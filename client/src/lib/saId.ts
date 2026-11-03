/**
 * Client-side mirror of the server's SouthAfricanIdNumber rules, for INSTANT feedback while typing.
 * The server re-validates everything: this only saves the user a round trip.
 */
export function validateSaId(raw: string, today = new Date()): { valid: boolean; reason?: string; dateOfBirth?: string } {
  const id = raw.replace(/\s/g, '')
  if (!/^\d{13}$/.test(id)) return { valid: false, reason: 'Must be exactly 13 digits.' }

  const yy = Number(id.slice(0, 2))
  let year = 2000 + yy
  if (year > today.getFullYear()) year -= 100
  const month = Number(id.slice(2, 4))
  const day = Number(id.slice(4, 6))
  const date = new Date(Date.UTC(year, month - 1, day))
  if (date.getUTCFullYear() !== year || date.getUTCMonth() !== month - 1 || date.getUTCDate() !== day || date > today)
    return { valid: false, reason: 'The first 6 digits must be a valid date of birth (YYMMDD).' }
  if (id[10] !== '0' && id[10] !== '1') return { valid: false, reason: 'Invalid citizenship digit.' }

  let sum = 0
  for (let i = 0; i < 13; i++) {
    let digit = Number(id[12 - i])
    if (i % 2 === 1) {
      digit *= 2
      if (digit > 9) digit -= 9
    }
    sum += digit
  }
  if (sum % 10 !== 0) return { valid: false, reason: 'Checksum failed: check for a typing error.' }

  const pad = (n: number) => String(n).padStart(2, '0')
  return { valid: true, dateOfBirth: `${year}-${pad(month)}-${pad(day)}` }
}

export function ageOn(dateOfBirth: string, on: Date = new Date()): number {
  const dob = new Date(dateOfBirth)
  let age = on.getFullYear() - dob.getFullYear()
  if (on.getMonth() < dob.getMonth() || (on.getMonth() === dob.getMonth() && on.getDate() < dob.getDate())) age--
  return age
}
