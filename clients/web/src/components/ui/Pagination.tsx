import { Button } from './Button'

export interface PaginationProps {
  // Cursor-style pagination matching the API (e.g. chat `before`/`limit`).
  hasOlder?: boolean
  hasNewer?: boolean
  onOlder?: () => void
  onNewer?: () => void
  isLoading?: boolean
  className?: string
}

export function Pagination({
  hasOlder,
  hasNewer,
  onOlder,
  onNewer,
  isLoading,
  className,
}: PaginationProps) {
  return (
    <nav className={className} aria-label="Pagination">
      <div className="flex items-center justify-between gap-2">
        <Button
          variant="secondary"
          size="sm"
          onClick={onNewer}
          disabled={!hasNewer || isLoading}
        >
          Newer
        </Button>
        <Button
          variant="secondary"
          size="sm"
          onClick={onOlder}
          disabled={!hasOlder || isLoading}
        >
          Older
        </Button>
      </div>
    </nav>
  )
}
