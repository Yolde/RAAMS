const TOKEN_KEY = 'raams_token'
export const TOKEN_CHANGED_EVENT = 'raams-token-changed'

export interface TokenPayload {
  sub: string
  email: string
  name: string
  role?: string | string[]
  exp: number
}

export function getToken(): string | null {
  return localStorage.getItem(TOKEN_KEY)
}

export function setToken(token: string): void {
  localStorage.setItem(TOKEN_KEY, token)
  window.dispatchEvent(new Event(TOKEN_CHANGED_EVENT))
}

export function clearToken(): void {
  localStorage.removeItem(TOKEN_KEY)
  window.dispatchEvent(new Event(TOKEN_CHANGED_EVENT))
}

export function decodeToken(token: string): TokenPayload {
  const parts = token.split('.')
  if (parts.length !== 3 || parts.some((part) => !part || !/^[A-Za-z0-9_-]+$/.test(part))) {
    throw new Error('Malformed JWT token.')
  }

  let payload: unknown
  try {
    const encoded = parts[1].replace(/-/g, '+').replace(/_/g, '/')
    const binary = atob(encoded.padEnd(Math.ceil(encoded.length / 4) * 4, '='))
    const bytes = Uint8Array.from(binary, (char) => char.charCodeAt(0))
    payload = JSON.parse(new TextDecoder('utf-8', { fatal: true }).decode(bytes))
  } catch {
    throw new Error('Malformed JWT token.')
  }
  if (typeof payload !== 'object' || payload === null || Array.isArray(payload)) {
    throw new Error('Malformed JWT payload.')
  }

  const claims = payload as Record<string, unknown>
  const name = claims.name ?? claims.unique_name
  const validRole = claims.role === undefined || typeof claims.role === 'string' ||
    (Array.isArray(claims.role) && claims.role.every((role) => typeof role === 'string'))
  if (typeof claims.sub !== 'string' || !claims.sub.trim() ||
      typeof claims.email !== 'string' || !claims.email.trim() ||
      typeof name !== 'string' || !name.trim() ||
      typeof claims.exp !== 'number' || !Number.isFinite(claims.exp) || !validRole) {
    throw new Error('JWT must contain valid sub, email, name, exp and role claims.')
  }

  return {
    sub: claims.sub,
    email: claims.email,
    name,
    role: claims.role as string | string[] | undefined,
    exp: claims.exp,
  }
}

export function isExpired(token: string): boolean {
  return decodeToken(token).exp <= Date.now() / 1000
}
