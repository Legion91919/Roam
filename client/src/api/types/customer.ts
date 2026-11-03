// The signed-in user: /api/customers/me (driver profile) and /api/account/me (email + roles).

export type LoyaltyTier = 'None' | 'Silver' | 'Gold'
export type DocumentType = 'SouthAfricanId' | 'Passport'

export interface CustomerProfile {
  customerId: string
  firstName: string
  lastName: string
  email: string
  phoneNumber: string
  documentType: DocumentType
  maskedDocumentNumber: string
  issuingCountry: string
  dateOfBirth: string // yyyy-MM-dd
  licenceIssuedOn: string
  loyaltyTier: LoyaltyTier
}

export interface SaveProfileRequest {
  firstName: string
  lastName: string
  phoneNumber: string
  documentType: DocumentType
  documentNumber: string
  issuingCountry: string | null
  dateOfBirth: string
  licenceIssuedOn: string
}

export interface Account {
  email: string
  roles: string[]
}
