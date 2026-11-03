import { useEffect, useMemo } from 'react'
import type { DocumentType } from '../../api/types'
import { ageOn, validateSaId } from '../../lib/saId'
import { Field, GridItem, Input, Segmented, Stack, Text } from '../ui'

export const DOCUMENTS = ['SA ID', 'Passport'] as const
export type DocumentChoice = (typeof DOCUMENTS)[number]

export interface IdentityValue {
  doc: DocumentChoice
  documentNumber: string
  issuingCountry: string
  dateOfBirth: string
  licenceIssuedOn: string
}

export const toDocumentType = (doc: DocumentChoice): DocumentType => (doc === 'SA ID' ? 'SouthAfricanId' : 'Passport')

/** True while an SA ID number has been typed but fails the checks (date, citizenship digit, Luhn). */
export function identityInvalid(value: IdentityValue): boolean {
  return value.doc === 'SA ID' && value.documentNumber !== '' && !validateSaId(value.documentNumber).valid
}

/**
 * The driver's identity document, date of birth and licence date, shared by the customer's profile
 * and the agent's "Add customer" form. The SA ID number is checked as it is typed and fills in the
 * date of birth; the server re-checks everything. Renders cells for a two-column <Grid>.
 */
export function IdentityFields({ value, onChange, onFile, audience = 'customer' }: {
  value: IdentityValue
  onChange: (changes: Partial<IdentityValue>) => void
  /** The masked number already on file (the full number is never sent back, so it must be re-entered). */
  onFile?: { documentType: DocumentType; masked: string }
  audience?: 'customer' | 'agent'
}) {
  const saId = useMemo(() => (value.doc === 'SA ID' && value.documentNumber ? validateSaId(value.documentNumber) : null), [value.doc, value.documentNumber])
  useEffect(() => {
    if (saId?.valid && saId.dateOfBirth && saId.dateOfBirth !== value.dateOfBirth) onChange({ dateOfBirth: saId.dateOfBirth })
  }, [saId, value.dateOfBirth, onChange])

  const age = value.dateOfBirth ? ageOn(value.dateOfBirth) : null
  const ageHint = age === null ? undefined
    : `${age} years old today.${age < 21 ? ' Drivers must be 21 or older.' : age < 25 ? ' Young driver fee applies (under 25).' : ''}`
  const idHint = saId?.valid ? 'Valid ID number. Date of birth filled in from it.'
    : audience === 'agent' ? '13 digits, e.g. 9005105009088.'
    : onFile ? 'Re-enter your 13-digit ID number to save changes.'
    : 'The 13 digits on your green ID book or smart ID card.'

  return (
    <>
      <GridItem>
        <Stack gap={2}>
          <Text as="span" size="label" tone="strong">{audience === 'agent' ? 'Identity document (check the original)' : 'Identity document'}</Text>
          <Segmented label="Identity document type" options={DOCUMENTS} value={value.doc} onChange={(doc) => onChange({ doc, documentNumber: '' })} />
        </Stack>
      </GridItem>

      {value.doc === 'SA ID' ? (
        <GridItem>
          <Field label="South African ID number" error={saId && !saId.valid ? saId.reason : undefined} hint={idHint}
            aside={onFile?.documentType === 'SouthAfricanId' ? `On file: ${onFile.masked}` : undefined}>
            <Input mono invalid={Boolean(saId && !saId.valid)} inputMode="numeric" maxLength={13} required placeholder="9005105009088"
              value={value.documentNumber} onChange={(e) => onChange({ documentNumber: e.target.value.replace(/\D/g, '') })} />
          </Field>
        </GridItem>
      ) : (
        <>
          <Field label="Passport number" aside={onFile?.documentType === 'Passport' ? `On file: ${onFile.masked}` : undefined}>
            <Input mono uppercase required maxLength={9} value={value.documentNumber} onChange={(e) => onChange({ documentNumber: e.target.value.toUpperCase() })} />
          </Field>
          <Field label="Issuing country" hint="3-letter code, e.g. GBR, USA, DEU.">
            <Input mono uppercase required maxLength={3} value={value.issuingCountry} onChange={(e) => onChange({ issuingCountry: e.target.value.toUpperCase() })} />
          </Field>
        </>
      )}

      <Field label="Date of birth" hint={ageHint}>
        <Input type="date" required value={value.dateOfBirth} disabled={Boolean(saId?.valid)} onChange={(e) => onChange({ dateOfBirth: e.target.value })} />
      </Field>
      <Field label="Licence issue date" hint="Must be held for at least 1 year before pickup.">
        <Input type="date" required value={value.licenceIssuedOn} onChange={(e) => onChange({ licenceIssuedOn: e.target.value })} />
      </Field>
    </>
  )
}
