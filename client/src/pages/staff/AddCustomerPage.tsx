import { useState, type FormEvent } from 'react'
import { describeError } from '../../api/client'
import { useRegisterCustomer } from '../../api/hooks'
import type { RegisteredCustomer } from '../../api/types'
import { identityInvalid, IdentityFields, toDocumentType, type IdentityValue } from '../../components/booking/IdentityFields'
import { StaffLayout } from '../../components/staff/StaffLayout'
import { Banner, Button, Callout, Card, CopyButton, Field, FormActions, Grid, GridItem, Input, Inline, Stack, Text } from '../../components/ui'

interface WalkInForm extends IdentityValue {
  email: string
  firstName: string
  lastName: string
  phoneNumber: string
}

const EMPTY: WalkInForm = { email: '', firstName: '', lastName: '', phoneNumber: '', doc: 'SA ID', documentNumber: '', issuingCountry: '', dateOfBirth: '', licenceIssuedOn: '' }

/**
 * Register a walk-in customer at the counter: same validation UX as the customer's own profile page
 * (shared IdentityFields: SA ID checked as you type, DOB filled from it); the server re-checks
 * everything. The temporary password is shown ONCE in a dismissible callout and is not kept anywhere
 * after that.
 */
export function AddCustomerPage() {
  const register = useRegisterCustomer()
  const [form, setForm] = useState<WalkInForm>(EMPTY)
  const [created, setCreated] = useState<RegisteredCustomer | null>(null)
  const set = (changes: Partial<WalkInForm>) => setForm((f) => ({ ...f, ...changes }))

  const submit = (e: FormEvent) => {
    e.preventDefault()
    register.mutate(
      {
        email: form.email.trim(), firstName: form.firstName, lastName: form.lastName, phoneNumber: form.phoneNumber,
        documentType: toDocumentType(form.doc), documentNumber: form.documentNumber,
        issuingCountry: form.doc === 'Passport' ? form.issuingCountry : null, dateOfBirth: form.dateOfBirth, licenceIssuedOn: form.licenceIssuedOn,
      },
      { onSuccess: (result) => { setCreated(result); setForm(EMPTY) } },
    )
  }
  const dismiss = () => { setCreated(null); register.reset() }

  return (
    <StaffLayout page="customer" title="Add customer" subtitle="Register a walk-in customer: a sign-in account and their driver profile.">
      {created && (
        <div data-testid="temporary-password">
          <Callout tone="success" title={`${created.email} is registered`}
            action={<Button variant="ghost" size="sm" icon="close" aria-label="Dismiss" onClick={dismiss}>Dismiss</Button>}>
            <Stack gap={3}>
              <Text>Give the customer this one-time password. It is shown only now; they should change it after signing in.</Text>
            <Inline gap={3}>
              <Card padding="sm"><Text as="span" mono size="lg" tone="strong">{created.temporaryPassword}</Text></Card>
              <CopyButton text={created.temporaryPassword} label="Copy password" />
            </Inline>
            </Stack>
          </Callout>
        </div>
      )}

      <Card padding="lg" width="form">
        <Grid as="form" columns={{ base: 1, sm: 2 }} gap={5} onSubmit={submit}>
          <GridItem>
            <Field label="Customer email" hint="Their sign-in. Production would email them an invitation link instead of a password.">
              <Input type="email" required autoComplete="off" value={form.email} onChange={(e) => set({ email: e.target.value })} />
            </Field>
          </GridItem>
          <Field label="First name"><Input required autoComplete="off" value={form.firstName} onChange={(e) => set({ firstName: e.target.value })} /></Field>
          <Field label="Surname"><Input required autoComplete="off" value={form.lastName} onChange={(e) => set({ lastName: e.target.value })} /></Field>
          <GridItem>
            <Field label="Mobile number"><Input required autoComplete="off" placeholder="+27 82 459 1102" value={form.phoneNumber} onChange={(e) => set({ phoneNumber: e.target.value })} /></Field>
          </GridItem>

          <IdentityFields value={form} onChange={set} audience="agent" />

          {register.isError && <GridItem><Banner error={describeError(register.error)} onDismiss={() => register.reset()} /></GridItem>}
          <GridItem>
            <FormActions>
              <Button type="submit" icon="person_add" loading={register.isPending} loadingText="Registering…" disabled={identityInvalid(form)}>Register customer</Button>
            </FormActions>
          </GridItem>
        </Grid>
      </Card>
    </StaffLayout>
  )
}
