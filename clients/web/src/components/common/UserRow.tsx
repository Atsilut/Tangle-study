import type { ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { Avatar, Card } from '@/components/ui'

export interface UserRowProps {
  userId?: number
  nickname: string
  subtitle?: string
  actions?: ReactNode
}

// Reusable row for any user listing (friends, requests, blocks, members).
// Links to the profile when a userId is provided.
export function UserRow({ userId, nickname, subtitle, actions }: UserRowProps) {
  return (
    <Card className="flex items-center gap-3 px-4 py-3">
      <Avatar name={nickname} size="sm" />
      <div className="min-w-0 flex-1">
        {userId != null ? (
          <Link
            to={`/users/${userId}`}
            className="block truncate text-sm font-medium text-gray-900 hover:underline"
          >
            {nickname}
          </Link>
        ) : (
          <span className="block truncate text-sm font-medium text-gray-900">{nickname}</span>
        )}
        {subtitle && <p className="truncate text-xs text-gray-500">{subtitle}</p>}
      </div>
      {actions && <div className="flex shrink-0 items-center gap-2">{actions}</div>}
    </Card>
  )
}
