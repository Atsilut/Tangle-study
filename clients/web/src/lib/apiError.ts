import { AxiosError } from 'axios'

interface ProblemDetails {
  title?: string
  detail?: string
}

// Extract a human-readable message from an axios error. The API returns RFC
// 7807 ProblemDetails for typed exceptions, plain text for some 400s.
export function getErrorMessage(error: unknown, fallback = 'Something went wrong.'): string {
  if (error instanceof AxiosError) {
    const data = error.response?.data as ProblemDetails | string | undefined
    if (typeof data === 'string' && data.trim()) return data
    if (data && typeof data === 'object') return data.detail || data.title || fallback
    if (error.message) return error.message
  }
  if (error instanceof Error) return error.message
  return fallback
}
