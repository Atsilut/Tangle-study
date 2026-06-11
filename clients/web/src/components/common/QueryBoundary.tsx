import type { ReactNode } from 'react'
import { ErrorState } from '@/components/ui'
import { CenteredSpinner } from './CenteredSpinner'

export interface QueryBoundaryProps {
  isLoading: boolean
  isError: boolean
  error?: unknown
  onRetry?: () => void
  children: ReactNode
}

// Recyclable loading/error gate for query-backed views. Render children only
// once data is available; otherwise show the shared Spinner / ErrorState.
export function QueryBoundary({
  isLoading,
  isError,
  onRetry,
  children,
}: QueryBoundaryProps) {
  if (isLoading) return <CenteredSpinner />
  if (isError) {
    return <ErrorState onRetry={onRetry} />
  }
  return <>{children}</>
}
