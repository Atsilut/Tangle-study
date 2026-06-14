import type { ReactNode } from 'react'
import { ErrorState } from '@/components/ui'
import { getErrorMessage } from '@/lib/apiError'
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
  error,
  onRetry,
  children,
}: QueryBoundaryProps) {
  if (isLoading) return <CenteredSpinner />
  if (isError) {
    return (
      <ErrorState
        message={getErrorMessage(error, 'Please try again.')}
        onRetry={onRetry}
      />
    )
  }
  return <>{children}</>
}
