import type { ReactNode } from 'react'
import { ErrorState, Spinner } from '@/components/ui'

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
  if (isLoading) {
    return (
      <div className="flex justify-center py-10 text-gray-400">
        <Spinner size="lg" />
      </div>
    )
  }
  if (isError) {
    return <ErrorState onRetry={onRetry} />
  }
  return <>{children}</>
}
