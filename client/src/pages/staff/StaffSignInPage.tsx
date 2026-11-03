import { useEffect, useState, type FormEvent } from 'react'
import { describeError } from '../../api/client'
import { useFetchAccount } from '../../api/hooks'
import { StaffAuthLayout } from '../../components/staff/StaffLayout'
import { Banner, Button, Callout, CardHeader, Field, Input, Stack } from '../../components/ui'
import { useAuth } from '../../lib/auth'
import { navigate } from '../../lib/router'

const NOT_STAFF = { code: 'auth.not_staff', title: 'Not a staff account', message: 'This account is not a staff account. Use the customer sign-in instead.', retryable: false }

/**
 * Staff sign-in: same POST /api/auth/login as customers, then GET /api/account/me. Without the Agent
 * role the session is signed out again. (The API enforces the role on every staff call anyway.)
 */
export function StaffSignInPage({ notStaff = false }: { notStaff?: boolean }) {
  const { signIn, signOut, session } = useAuth()
  const fetchAccount = useFetchAccount()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<ReturnType<typeof describeError> | null>(notStaff ? NOT_STAFF : null)

  // A customer who opened #/agent/* arrives here signed in: sign them out of this tab's session.
  useEffect(() => {
    if (notStaff && session) signOut()
  }, [notStaff, session, signOut])

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    setBusy(true)
    setError(null)
    try {
      await signIn(email, password)
      const account = await fetchAccount()
      if (!account.roles.includes('Agent')) {
        signOut()
        setError(NOT_STAFF)
        return
      }
      navigate('/agent')
    } catch (err) {
      const described = describeError(err)
      setError(described.code === 'auth.required' ? { ...described, title: 'Sign-in failed', message: 'Email or password is incorrect.' } : described)
    } finally {
      setBusy(false)
    }
  }

  return (
    <StaffAuthLayout>
      <Stack gap={6}>
        <CardHeader level={1} size="lg" title="Staff sign-in" subtitle="Counter staff only. Customers sign in on the booking site." />
        <Stack as="form" gap={4} onSubmit={submit}>
          <Field label="Work email">
            <Input type="email" autoComplete="username" required value={email} onChange={(e) => setEmail(e.target.value)} />
          </Field>
          <Field label="Password">
            <Input type="password" autoComplete="current-password" required value={password} onChange={(e) => setPassword(e.target.value)} />
          </Field>
          {error && <Banner error={error} />}
          <Button type="submit" size="lg" block icon="badge" loading={busy} loadingText="Signing in…">Sign in to Roam Staff</Button>
        </Stack>
        <Callout size="sm">Development demo account: agent@roam.test (password from Seed:DemoPassword).</Callout>
      </Stack>
    </StaffAuthLayout>
  )
}
