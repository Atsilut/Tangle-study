import { Spinner } from '@/components/ui'

// Shared loading placeholder for pages that gate on a secondary query (e.g. role).
export function CenteredSpinner() {
  return (
    <div className="flex justify-center py-10 text-gray-400">
      <Spinner size="lg" />
    </div>
  )
}
