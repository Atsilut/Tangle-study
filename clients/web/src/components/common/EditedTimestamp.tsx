import { formatDate, formatDateTime, formatUpdatedAt, isEdited } from '@/lib/format'

export interface EditedTimestampProps {
  createdAt: string
  updatedAt: string
  /** Use date-only formatting in compact list views. */
  variant?: 'date' | 'datetime'
  className?: string
}

export function EditedTimestamp({
  createdAt,
  updatedAt,
  variant = 'datetime',
  className = 'text-xs text-gray-400',
}: EditedTimestampProps) {
  const format = variant === 'date' ? formatDate : formatDateTime
  const edited = isEdited(createdAt, updatedAt)

  return (
    <span className={className}>
      {format(createdAt)}
      {edited && (
        <>
          {' '}
          <span className="text-gray-500">(Edited)</span> {formatUpdatedAt(createdAt, updatedAt, variant)}
        </>
      )}
    </span>
  )
}
