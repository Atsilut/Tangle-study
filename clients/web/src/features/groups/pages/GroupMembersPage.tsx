import { useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { Badge, Button, ConfirmDialog, EmptyState, Select } from '@/components/ui'
import { QueryBoundary } from '@/components/common/QueryBoundary'
import { UserRow } from '@/components/common/UserRow'
import { useAuthStore } from '@/stores/authStore'
import { GroupRole } from '@/types/api'
import {
  useGroupMembers,
  useMyGroupRole,
  useRemoveMember,
  useUpdateMemberRole,
} from '../membersHooks'
import { useTransferOwnership } from '../hooks'
import { groupRoleLabels } from '../labels'
import type { GroupMember } from '../membersApi'

export function GroupMembersPage() {
  const { id } = useParams<{ id: string }>()
  const groupId = Number(id)
  const currentUserId = useAuthStore((s) => s.userId)
  const members = useGroupMembers(Number.isFinite(groupId) ? groupId : null)
  const { role: myRole } = useMyGroupRole(Number.isFinite(groupId) ? groupId : null)

  const isOwner = myRole === GroupRole.Owner
  const isAdmin = myRole === GroupRole.Admin || isOwner

  return (
    <div className="flex max-w-2xl flex-col gap-4">
      <Link to={`/groups/${groupId}`} className="text-sm text-blue-600 hover:underline">
        Back to group
      </Link>
      <h1 className="text-2xl font-bold text-gray-900">Members</h1>
      <QueryBoundary
        isLoading={members.isLoading}
        isError={members.isError}
        onRetry={() => members.refetch()}
      >
        {members.data && members.data.length > 0 ? (
          <ul className="flex flex-col gap-2">
            {members.data.map((member) => (
              <li key={member.userId}>
                <MemberRow
                  groupId={groupId}
                  member={member}
                  isSelf={member.userId === currentUserId}
                  isOwner={isOwner}
                  isAdmin={isAdmin}
                />
              </li>
            ))}
          </ul>
        ) : (
          <EmptyState title="No members" />
        )}
      </QueryBoundary>
    </div>
  )
}

interface MemberRowProps {
  groupId: number
  member: GroupMember
  isSelf: boolean
  isOwner: boolean
  isAdmin: boolean
}

function MemberRow({ groupId, member, isSelf, isOwner, isAdmin }: MemberRowProps) {
  const navigate = useNavigate()
  const updateRole = useUpdateMemberRole(groupId)
  const removeMember = useRemoveMember(groupId)
  const transfer = useTransferOwnership(groupId)
  const [confirm, setConfirm] = useState<'leave' | 'remove' | 'transfer' | null>(null)

  const memberIsOwner = member.role === GroupRole.Owner

  // Owner can promote/demote non-owner members between Member and Admin.
  const canChangeRole = isOwner && !isSelf && !memberIsOwner
  // Owner can transfer ownership to another member.
  const canTransfer = isOwner && !isSelf && !memberIsOwner
  // Admin/owner can kick; owner additionally can remove admins. Never the owner.
  const canRemove = !isSelf && !memberIsOwner && isAdmin && (isOwner || member.role === GroupRole.Member)

  return (
    <>
      <UserRow
        userId={member.userId}
        nickname={member.nickname}
        subtitle={groupRoleLabels[member.role]}
        actions={
          <>
            {!isSelf && !canChangeRole && <Badge>{groupRoleLabels[member.role]}</Badge>}
            {canChangeRole && (
              <Select
                aria-label={`Role for ${member.nickname}`}
                value={member.role}
                onChange={(e) =>
                  updateRole.mutate({ userId: member.userId, role: Number(e.target.value) })
                }
                className="w-32"
              >
                <option value={GroupRole.Member}>{groupRoleLabels[GroupRole.Member]}</option>
                <option value={GroupRole.Admin}>{groupRoleLabels[GroupRole.Admin]}</option>
              </Select>
            )}
            {canTransfer && (
              <Button size="sm" variant="secondary" onClick={() => setConfirm('transfer')}>
                Make owner
              </Button>
            )}
            {canRemove && (
              <Button size="sm" variant="danger" onClick={() => setConfirm('remove')}>
                Remove
              </Button>
            )}
            {isSelf && !isOwner && (
              <Button size="sm" variant="secondary" onClick={() => setConfirm('leave')}>
                Leave
              </Button>
            )}
          </>
        }
      />

      <ConfirmDialog
        isOpen={confirm === 'leave'}
        title="Leave group"
        message="You will lose access to this group."
        confirmLabel="Leave"
        destructive
        isLoading={removeMember.isPending}
        onConfirm={() =>
          removeMember.mutate(member.userId, { onSuccess: () => navigate(`/groups/${groupId}`) })
        }
        onCancel={() => setConfirm(null)}
      />
      <ConfirmDialog
        isOpen={confirm === 'remove'}
        title={`Remove ${member.nickname}`}
        message="This removes the member from the group."
        confirmLabel="Remove"
        destructive
        isLoading={removeMember.isPending}
        onConfirm={() =>
          removeMember.mutate(member.userId, { onSuccess: () => setConfirm(null) })
        }
        onCancel={() => setConfirm(null)}
      />
      <ConfirmDialog
        isOpen={confirm === 'transfer'}
        title={`Make ${member.nickname} owner`}
        message="You will become an admin and this member will become the group owner."
        confirmLabel="Transfer ownership"
        isLoading={transfer.isPending}
        onConfirm={() =>
          transfer.mutate(member.userId, { onSuccess: () => setConfirm(null) })
        }
        onCancel={() => setConfirm(null)}
      />
    </>
  )
}
