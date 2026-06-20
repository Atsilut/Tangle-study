import { Badge } from '@/components/ui'
import { useGroupMemberSharingStatus } from '../hooks'
import type { GroupMemberLocationStatus } from '../api'

export interface GroupSharingStatusListProps {
  groupId: number | null
  enabled: boolean
}

function formatNickname(nickname: string): string {
  const trimmed = nickname.trim() || 'Member'
  if (trimmed.length <= 20) return trimmed
  return `${trimmed.slice(0, 19)}…`
}

function statusLabel(member: GroupMemberLocationStatus): string {
  if (member.isSharing && member.updatedAt) {
    return `Updated ${new Date(member.updatedAt).toLocaleTimeString()}`
  }
  return 'Not sharing location'
}

export function GroupSharingStatusList({ groupId, enabled }: GroupSharingStatusListProps) {
  const { data: members = [], isLoading, isError } = useGroupMemberSharingStatus(groupId, enabled)

  if (!enabled || groupId == null) return null

  if (isLoading) {
    return (
      <div className="rounded-lg border border-gray-200 bg-white p-3 text-sm text-gray-600">
        Loading member sharing status…
      </div>
    )
  }

  if (isError) {
    return (
      <div className="rounded-lg border border-red-200 bg-red-50 p-3 text-sm text-red-800">
        Could not load member sharing status.
      </div>
    )
  }

  if (members.length === 0) {
    return (
      <div className="rounded-lg border border-gray-200 bg-gray-50 p-3 text-sm text-gray-600">
        No other members in this group.
      </div>
    )
  }

  const sharingCount = members.filter((member) => member.isSharing).length

  return (
    <div className="rounded-lg border border-gray-200 bg-white p-3">
      <div className="mb-2 flex flex-wrap items-center justify-between gap-2">
        <p className="text-sm font-medium text-gray-900">Group sharing status</p>
        <p className="text-xs text-gray-600">
          {sharingCount} of {members.length} sharing
        </p>
      </div>
      <ul className="flex flex-col gap-2">
        {members.map((member) => (
          <li
            key={member.userId}
            className="flex items-center justify-between gap-3 rounded-md border border-gray-100 px-3 py-2"
          >
            <div className="min-w-0">
              <p className="truncate text-sm font-medium text-gray-900">
                {formatNickname(member.userNickname)}
              </p>
              <p className="text-xs text-gray-600">{statusLabel(member)}</p>
            </div>
            <Badge color={member.isSharing ? 'green' : 'gray'}>
              {member.isSharing ? 'Sharing' : 'Not sharing'}
            </Badge>
          </li>
        ))}
      </ul>
    </div>
  )
}
