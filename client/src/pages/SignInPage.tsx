import { useState, type FormEvent } from 'react'
import { describeError } from '../api/client'
import { useLandingAfterSignIn } from '../api/hooks'
import { Page } from '../components/booking/Layout'
import { Banner, Button, Card, CardHeader, Field, Inline, Input, Segmented, Stack, Text } from '../components/ui'
import { useAuth } from '../lib/auth'
import { navigate } from '../lib/router'

const MODES = ['Sign in', 'Create account'] as const

/** Sign in or register against ASP.NET Core Identity (POST /api/auth/login, /api/auth/register). */
export function SignInPage({ next }: { next: string }) {
  const { signIn, register } = useAuth()
  const landing = useLandingAfterSignIn()
  const [mode, setMode] = useState<(typeof MODES)[number]>('Sign in')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<unknown>(null)

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    setBusy(true)
    setError(null)
    try {
      if (mode === 'Sign in') await signIn(email, password)
      else await register(email, password)
      // Staff go to the staff app; a new (or unfinished) customer fills in the driver profile first.
      navigate(await landing(next))
    } catch (err) {
      setError(err)
    } finally {
      setBusy(false)
    }
  }

  const described = error ? describeError(error) : null
  const loginFailed = mode === 'Sign in' && described?.code === 'auth.required'

  return (
    <Page width="compact">
      <Card padding="lg" gap={6}>
        <CardHeader level={1} size="lg" title={mode === 'Sign in' ? 'Welcome back' : 'Create your account'}
          subtitle={next ? 'Sign in to continue your booking.' : 'Manage your bookings and driver profile.'} />
        <Segmented label="Sign in or register" options={MODES} value={mode} onChange={(m) => { setMode(m); setError(null) }} />

        <Stack as="form" gap={4} onSubmit={submit}>
          <Field label="Email">
            <Input type="email" autoComplete="email" required value={email} onChange={(e) => setEmail(e.target.value)} />
          </Field>
          <Field label="Password" hint={mode === 'Create account' ? 'At least 8 characters with upper and lower case, a digit and a symbol.' : undefined}>
            <Input type="password" autoComplete={mode === 'Sign in' ? 'current-password' : 'new-password'} required value={password} onChange={(e) => setPassword(e.target.value)} />
          </Field>
          {described && <Banner error={loginFailed ? { ...described, title: 'Sign-in failed', message: 'Email or password is incorrect.' } : described} />}
          <Button type="submit" size="lg" block loading={busy} loadingText={mode === 'Sign in' ? 'Signing in…' : 'Creating account…'}>{mode}</Button>
        </Stack>

        {mode === 'Create account' && <SignUpExample onUse={(sample) => { setEmail(sample.email); setPassword(sample.password) }} />}
      </Card>
    </Page>
  )
}

/** Test SA ID numbers that pass the server's checks (date, citizenship digit, Luhn). */
const TEST_IDS = [
  { id: '9005105009088', dob: '1990-05-10', note: 'aged 25+: standard price' },
  { id: '0308125800083', dob: '2003-08-12', note: 'under 25: young driver fee' },
  { id: '8512310001082', dob: '1985-12-31', note: 'already used by gold@roam.test' },
] as const

/**
 * "Example" helper for trying the sign-up flow: fills a sample email and password, and lists valid
 * test SA ID numbers to use on the driver profile page next. Sample data only: never real people.
 */
function SignUpExample({ onUse }: { onUse: (sample: { email: string; password: string }) => void }) {
  const fill = () => onUse({ email: `driver.${Math.random().toString(36).slice(2, 7)}@example.com`, password: 'Roam_Test_123!' })
  return (
    <Card tone="muted" padding="sm" gap={2}>
      <Inline gap={2} justify="between">
        <Text tone="strong" weight="medium" icon="science">Example</Text>
        <Button variant="ghost" size="sm" icon="auto_fix_high" onClick={fill}>Fill sample email and password</Button>
      </Inline>
      <Text>After creating the account you'll fill in the driver profile. These test SA ID numbers are valid:</Text>
      <Stack as="ul" gap={1}>
        {TEST_IDS.map((t) => (
          <li key={t.id}>
            <Inline gap={3} align="baseline">
              <Text as="span" mono tone="strong">{t.id}</Text>
              <Text as="span" size="sm">born {t.dob} · {t.note}</Text>
            </Inline>
          </li>
        ))}
      </Stack>
    </Card>
  )
}
