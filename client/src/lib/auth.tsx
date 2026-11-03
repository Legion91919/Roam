import { useQueryClient } from '@tanstack/react-query'
import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react'
import { api, setAccessToken, UNAUTHORIZED_EVENT } from '../api/client'

/**
 * Sign-in state. The API (ASP.NET Core Identity) issues a bearer token from POST /api/auth/login;
 * we keep it in sessionStorage (cleared when the tab closes) and attach it to every API call.
 * The browser never sees passwords again after login, and never decides who the user is: every
 * protected endpoint re-checks the token on the server.
 */
interface Session {
  email: string
  accessToken: string
  expiresAt: number
}

interface AuthContextValue {
  session: Session | null
  signIn: (email: string, password: string) => Promise<void>
  register: (email: string, password: string) => Promise<void>
  signOut: () => void
}

const KEY = 'roam.session'
const AuthContext = createContext<AuthContextValue | null>(null)

function loadSession(): Session | null {
  try {
    const raw = sessionStorage.getItem(KEY)
    const session = raw ? (JSON.parse(raw) as Session) : null
    return session && session.expiresAt > Date.now() ? session : null
  } catch {
    return null
  }
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient()
  const [session, setSession] = useState<Session | null>(() => {
    const s = loadSession()
    setAccessToken(s?.accessToken ?? null)
    return s
  })

  const save = useCallback((next: Session | null) => {
    setAccessToken(next?.accessToken ?? null)
    setSession(next)
    try {
      if (next) sessionStorage.setItem(KEY, JSON.stringify(next))
      else sessionStorage.removeItem(KEY)
    } catch {
      /* storage unavailable: session lives in memory only */
    }
  }, [])

  // Everything cached for the signed-in user. Cleared on sign-out AND before a different user signs
  // in, so a new account never sees the previous user's driver profile or bookings.
  const forgetUserData = useCallback(() => {
    for (const key of ['profile', 'reservations', 'reservation', 'quote', 'account', 'agent', 'attachment'])
      queryClient.removeQueries({ queryKey: [key] })
  }, [queryClient])

  const signOut = useCallback(() => {
    save(null)
    forgetUserData()
  }, [save, forgetUserData])

  // Token rejected by the API (expired, server restarted): sign out locally.
  useEffect(() => {
    window.addEventListener(UNAUTHORIZED_EVENT, signOut)
    return () => window.removeEventListener(UNAUTHORIZED_EVENT, signOut)
  }, [signOut])

  const value = useMemo<AuthContextValue>(() => {
    const signIn = async (email: string, password: string) => {
      const token = await api<{ accessToken: string; expiresIn: number }>('/api/auth/login', {
        method: 'POST',
        body: JSON.stringify({ email, password }),
      })
      forgetUserData()
      save({ email, accessToken: token.accessToken, expiresAt: Date.now() + token.expiresIn * 1000 })
    }
    return {
      session,
      signIn,
      register: async (email, password) => {
        await api<void>('/api/auth/register', { method: 'POST', body: JSON.stringify({ email, password }) })
        await signIn(email, password)
      },
      signOut,
    }
  }, [session, save, signOut, forgetUserData])

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth(): AuthContextValue {
  const context = useContext(AuthContext)
  if (!context) throw new Error('useAuth must be used inside <AuthProvider>')
  return context
}
