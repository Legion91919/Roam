import type { VehicleCategory } from '../api/types'

/**
 * Display catalogue per category (marketing copy and specs). Prices and availability always come
 * from the API; this is only what the API doesn't model.
 */
export interface CategoryInfo {
  code: string
  example: string
  seats: number
  bags: number
  transmission: 'Manual' | 'Automatic'
  features: string[]
  note?: string
}

export const CATEGORY_INFO: Record<VehicleCategory, CategoryInfo> = {
  Economy: { code: 'CAT · A', example: 'VW Polo Vivo or similar', seats: 4, bags: 2, transmission: 'Manual', features: ['AC incl.', '5 doors · Hatchback'] },
  Compact: { code: 'CAT · B', example: 'VW Polo TSI or similar', seats: 5, bags: 3, transmission: 'Automatic', features: ['Climate control', 'Apple CarPlay / Android Auto'] },
  Suv: { code: 'CAT · E', example: 'Toyota Fortuner or similar', seats: 7, bags: 4, transmission: 'Automatic', features: ['Diesel / Petrol', 'Adaptive cruise · 4WD'] },
  Luxury: { code: 'CAT · L', example: 'Mercedes-Benz C200 or similar', seats: 5, bags: 3, transmission: 'Automatic', features: ['Premium audio'], note: 'Driver age 25+ required' },
}

export const CATEGORY_LABEL: Record<VehicleCategory, string> = {
  Economy: 'Economy',
  Compact: 'Compact',
  Suv: 'SUV',
  Luxury: 'Luxury',
}
