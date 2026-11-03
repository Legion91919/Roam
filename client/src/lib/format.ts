/** South African notation, as the design system specifies: "R 1 350,00". */
const money = new Intl.NumberFormat('en-ZA', { style: 'currency', currency: 'ZAR' })

export function formatMoney(amount: number): string {
  return money.format(amount).replace(/ /g, ' ')
}

export function formatDay(iso: string): string {
  return new Intl.DateTimeFormat('en-ZA', { weekday: 'short', day: 'numeric', month: 'short', year: 'numeric' }).format(new Date(iso))
}

export function formatShortDate(iso: string): string {
  return new Intl.DateTimeFormat('en-ZA', { day: 'numeric', month: 'short' }).format(new Date(iso))
}

export function formatTime(iso: string): string {
  return new Intl.DateTimeFormat('en-ZA', { hour: '2-digit', minute: '2-digit', hour12: false }).format(new Date(iso))
}

/** "2026-12-01" + "09:00" (browser time) -> "2026-12-01T09:00:00+02:00". */
export function toIsoWithOffset(date: string, time: string): string {
  if (!date || !time) return ''
  const local = new Date(`${date}T${time}`)
  const offset = -local.getTimezoneOffset()
  const pad = (n: number) => String(Math.floor(Math.abs(n))).padStart(2, '0')
  return `${date}T${time}:00${offset >= 0 ? '+' : '-'}${pad(offset / 60)}:${pad(offset % 60)}`
}

/** yyyy-MM-dd for a date N days from today (browser time). */
export function dateFromToday(days: number): string {
  const d = new Date()
  d.setDate(d.getDate() + days)
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
}

/**
 * Display-only mirror of the server rule (RentalPeriod.ChargeableDays): every started 24h is a day,
 * after a 59-minute grace, minimum 1. The server's quote is always the price of record.
 */
export function chargeableDays(pickupIso: string, returnIso: string): number {
  const minutes = (new Date(returnIso).getTime() - new Date(pickupIso).getTime()) / 60000
  if (!(minutes > 0)) return 0
  const full = Math.floor(minutes / 1440)
  return Math.max(1, minutes - full * 1440 > 59 ? full + 1 : full)
}

/** GUID -> "ROAM-8F4B-92A1-0C7E-449D" style display reference (first 16 hex chars). */
export function bookingReference(id: string): string {
  const hex = id.replace(/-/g, '').toUpperCase()
  return `ROAM-${hex.slice(0, 4)}-${hex.slice(4, 8)}-${hex.slice(8, 12)}-${hex.slice(12, 16)}`
}
