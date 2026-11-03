import { useEffect, useState, type FormEvent } from 'react'
import { describeError } from '../api/client'
import { useProfile, useSaveProfile } from '../api/hooks'
import { identityInvalid, IdentityFields, toDocumentType, type IdentityValue } from '../components/booking/IdentityFields'
import { Page } from '../components/booking/Layout'
import { Banner, Button, Callout, Card, CardHeader, Field, FormActions, Grid, GridItem, Icon, Input, Pill, QueryState, Skeleton, Stack } from '../components/ui'
import { useAuth } from '../lib/auth'
import { navigate, withNext } from '../lib/router'

interface ProfileForm extends IdentityValue {
  firstName: string
  lastName: string
  phoneNumber: string
}

const EMPTY: ProfileForm = { firstName: '', lastName: '', phoneNumber: '', doc: 'SA ID', documentNumber: '', issuingCountry: '', dateOfBirth: '', licenceIssuedOn: '' }

/**
 * Driver profile: who the main driver is. The SA ID number is checked as you type (date, checksum)
 * and fills in the date of birth; the server re-checks everything and stores the ID masked.
 */
export function ProfilePage({ next }: { next: string }) {
  const { session } = useAuth()
  const profile = useProfile(Boolean(session))
  const save = useSaveProfile()
  const [form, setForm] = useState<ProfileForm>(EMPTY)
  const set = (changes: Partial<ProfileForm>) => setForm((f) => ({ ...f, ...changes }))

  useEffect(() => {
    if (!session) navigate(withNext('/signin', '/profile'))
  }, [session])

  // Prefill from the saved profile (the full ID number is never sent back, so it must be re-entered).
  useEffect(() => {
    const p = profile.data
    if (p) setForm((f) => ({ ...f, firstName: p.firstName, lastName: p.lastName, phoneNumber: p.phoneNumber, doc: p.documentType === 'Passport' ? 'Passport' : 'SA ID', issuingCountry: p.issuingCountry, dateOfBirth: p.dateOfBirth, licenceIssuedOn: p.licenceIssuedOn }))
  }, [profile.data])

  if (!session) return null

  const submit = (e: FormEvent) => {
    e.preventDefault()
    save.mutate(
      { firstName: form.firstName, lastName: form.lastName, phoneNumber: form.phoneNumber, documentType: toDocumentType(form.doc), documentNumber: form.documentNumber, issuingCountry: form.doc === 'Passport' ? form.issuingCountry : null, dateOfBirth: form.dateOfBirth, licenceIssuedOn: form.licenceIssuedOn },
      { onSuccess: () => navigate(next || '/') },
    )
  }
  const tier = profile.data?.loyaltyTier

  return (
    <Page width="narrow">
      <QueryState query={profile} loading={<Card padding="lg"><Stack gap={4}><Skeleton size="title" /><Skeleton size="field" /><Skeleton size="field" /></Stack></Card>}>
        <Card padding="lg" gap={8}>
          <CardHeader level={1} size="lg" eyebrow={next ? 'Step 3 of 4' : 'Account'} title="Main driver details"
            subtitle="Must match the ID or passport and licence you show at pickup."
            actions={tier && <Pill tone={tier === 'None' ? 'neutral' : 'success'}><Icon name="workspace_premium" size={14} /> {tier === 'None' ? 'No loyalty tier' : `${tier} member`}</Pill>} />

          <Grid as="form" columns={{ base: 1, sm: 2 }} gap={5} onSubmit={submit}>
            <Field label="First name"><Input required autoComplete="given-name" value={form.firstName} onChange={(e) => set({ firstName: e.target.value })} /></Field>
            <Field label="Surname"><Input required autoComplete="family-name" value={form.lastName} onChange={(e) => set({ lastName: e.target.value })} /></Field>
            <Field label="Mobile number"><Input required autoComplete="tel" placeholder="+27 82 459 1102" value={form.phoneNumber} onChange={(e) => set({ phoneNumber: e.target.value })} /></Field>
            <Field label="Email" hint="From your sign-in."><Input value={session.email} disabled /></Field>

            <IdentityFields value={form} onChange={set}
              onFile={profile.data ? { documentType: profile.data.documentType, masked: profile.data.maskedDocumentNumber } : undefined} />

            <GridItem>
              <Callout icon="lock" size="sm">Your ID number is used only to verify the driver at pickup (POPIA) and is never shown in full.</Callout>
            </GridItem>
            {save.isError && <GridItem><Banner error={describeError(save.error)} onDismiss={() => save.reset()} /></GridItem>}
            <GridItem>
              <FormActions>
                {next && <Button variant="secondary" onClick={() => navigate('/results')}>Back</Button>}
                <Button type="submit" trailingIcon="arrow_forward" loading={save.isPending} loadingText="Saving…" disabled={identityInvalid(form)}>
                  {next ? 'Save and continue' : 'Save profile'}
                </Button>
              </FormActions>
            </GridItem>
          </Grid>
        </Card>
      </QueryState>
    </Page>
  )
}
