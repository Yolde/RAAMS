import { createContext, useContext, useMemo } from 'react'
import type { ReactNode } from 'react'
import { useMe } from '../api/queries'
import type { Me } from '../api/types'
import { useAuth } from '../auth/AuthProvider'

interface RoleContextValue {
  me: Me | null
  roles: string[]
  permissions: string[]
  hasPermission: (permission: string) => boolean
  hasRole: (role: string) => boolean
  isLoading: boolean
  error: Error | null
  retry: () => void
}

const RoleContext = createContext<RoleContextValue | null>(null)

export function RoleProvider({ children }: { children: ReactNode }) {
  const { isAuthenticated } = useAuth()
  const query = useMe()
  const value = useMemo(() => {
    const me = isAuthenticated ? query.data ?? null : null
    const roles = me?.roles ?? []
    const permissions = me?.permissions ?? []
    return {
      me,
      roles,
      permissions,
      hasPermission: (permission: string) => permissions.includes(permission),
      hasRole: (role: string) => roles.includes(role),
      isLoading: isAuthenticated && query.isPending,
      error: isAuthenticated ? query.error : null,
      retry: () => { void query.refetch() },
    }
  }, [isAuthenticated, query.data, query.error, query.isPending, query.refetch])
  return <RoleContext.Provider value={value}>{children}</RoleContext.Provider>
}

export function usePermissions(): RoleContextValue {
  const context = useContext(RoleContext)
  if (!context) throw new Error('usePermissions must be used within RoleProvider.')
  return context
}
