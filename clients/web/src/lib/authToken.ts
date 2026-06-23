import { jwtDecode } from 'jwt-decode'

interface JwtClaims {
  exp?: number
}

/** True when the access token is missing, malformed, or past expiry (with optional skew). */
export function isAccessTokenExpired(token: string, skewMs = 5_000): boolean {
  try {
    const { exp } = jwtDecode<JwtClaims>(token)
    if (exp == null) return false
    return Date.now() >= exp * 1000 - skewMs
  } catch {
    return true
  }
}
