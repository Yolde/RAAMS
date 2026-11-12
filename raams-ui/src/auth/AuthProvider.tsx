import { useQueryClient } from '@tanstack/react-query'
import { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react'
import type { ReactNode } from 'react'
import { clearToken, decodeToken, getToken, isExpired, setToken, TOKEN_CHANGED_EVENT } from './tokenStorage'

interface AuthUser {
  sub: string
  email: string
  name: string
  roles: string[]
}

interface AuthContextValue {
  token: string | null
  user: AuthUser | null
  isAuthenticated: boolean
  login: (token: string) => void
  logout: () => void
}

const AuthContext = createContext<AuthContextValue | null>(null)

function readSession(): { token: string; user: AuthUser; exp: number } | null {
  const token = getToken()
  if (!token) return null
  try {
    const payload = decodeToken(token)
    if (payload.exp <= Date.now() / 1000) return null
    return {
      token,
      user: {
        sub: payload.sub,
        email: payload.email,
        name: payload.name,
        roles: typeof payload.role === 'string' ? [payload.role] : payload.role ?? [],
      },
      exp: payload.exp,
    }
  } catch {
    return null
  }
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient()
  const [session, setSession] = useState(readSession)

  useEffect(() => {
    const sync = () => {
      const next = readSession()
      setSession((previous) => {
        if (previous?.token === next?.token) return previous
        return next
      })
    }
    window.addEventListener(TOKEN_CHANGED_EVENT, sync)
    window.addEventListener('storage', sync)
    if (!session && getToken()) clearToken()
    return () => {
      window.removeEventListener(TOKEN_CHANGED_EVENT, sync)
      window.removeEventListener('storage', sync)
    }
  }, [session])

  useEffect(() => {
    if (!session) return
    let timer: ReturnType<typeof setTimeout>
    const check = () => {
      const remaining = session.exp * 1000 - Date.now()
      if (remaining <= 0) {
        if (getToken() === session.token) {
          clearToken()
          queryClient.clear()
          window.location.assign('/login')
        }
      } else {
        timer = setTimeout(check, Math.min(remaining, 2_147_483_647))
      }
    }
    check()
    return () => clearTimeout(timer)
  }, [session, queryClient])

  const login = useCallback((value: string) => {
    const token = value.trim()
    decodeToken(token)
    if (isExpired(token)) throw new Error('This token has expired.')
    queryClient.clear()
    setToken(token)
    setSession(readSession())
  }, [queryClient])

  const logout = useCallback(() => {
    clearToken()
    setSession(null)
    queryClient.clear()
    window.location.assign('/login')
  }, [queryClient])

  const value = useMemo(() => ({
    token: session?.token ?? null,
    user: session?.user ?? null,
    isAuthenticated: session !== null,
    login,
    logout,
  }), [session, login, logout])

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth(): AuthContextValue {
  const context = useContext(AuthContext)
  if (!context) throw new Error('useAuth must be used within AuthProvider.')
  return context
}
