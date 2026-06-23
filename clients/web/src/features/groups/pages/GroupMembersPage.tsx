import { type FormEvent, useMemo, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import {
  Badge,
  Button,
  Card,
  CardBody,
  CardHeader,
  ConfirmDialog,
  EmptyState,
  ErrorState,
  FormField,
  Input,
  Select,
} from '@/components/ui'
import { QueryBoundary } from '@/components/common/QueryBoundary'
import { UserRow } from '@/components/common/UserRow'
import { getErrorMessage } from '@/lib/apiError'
import { formatDateTime } from '@/lib/format'
import { useAuthStore } from '@/stores/authStore'
import { useUsers } from '@/features/users/hooks'
import { GroupInvitePolicy, GroupRole } from '@/types/api'
import {
  useGroupMembers,
  useMyGroupRole,
  useRemoveMember,
  useUpdateMemberRole,
} from '../membersHooks'
import {
  useCancelInvitation,
  useGroupInvitations,
  useInviteToGroup,
} from '../invitationsHooks'
import { useGroup, useTransferOwnership } from '../hooks'
import { canInviteToGroup, groupRoleLabels } from '../labels'
import type { GroupMember } from '../membersApi'

export function GroupMembersPage() {
  const { id } = useParams<{ id: string }>()
  const groupId = Number(id)
  const currentUserId = useAuthStore((s) => s.userId)
  const group = useGroup(Number.isFinite(groupId) ? groupId : null)
  const members = useGroupMembers(Number.isFinite(groupId) ? groupId : null)
  const { role: myRole } = useMyGroupRole(Number.isFinite(groupId) ? groupId : null)

  const isOwner = myRole === GroupRole.Owner
  const isAdmin = myRole === GroupRole.Admin || isOwner
  const canInvite = canInviteToGroup(
    group.data?.invitePolicy ?? GroupInvitePolicy.AdminsOnly,
    myRole,
  )

  return (
    <div className="flex max-w-2xl flex-col gap-4">
      <Link to={`/groups/${groupId}`} className="text-sm text-blue-600 hover:underline">
        Back to group
      </Link>
      <h1 className="text-2xl font-bold text-gray-900">Members</h1>
      {canInvite && <InviteCard groupId={groupId} />}
      {canInvite && <PendingInvitationsCard groupId={groupId} />}
      <QueryBoundary
        isLoading={members.isLoading}
        isError={members.isError}
        error={members.error}
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

function InviteCard({ groupId }: { groupId: number }) {
  const invite = useInviteToGroup(groupId)
  const users = useUsers()
  const members = useGroupMembers(groupId)
  const [nickname, setNickname] = useState('')
  const [lookupError, setLookupError] = useState<string | null>(null)

  const memberIds = useMemo(
    () => new Set(members.data?.map((m) => m.userId) ?? []),
    [members.data],
  )

  const suggestions = useMemo(
    () =>
      (users.data ?? [])
        .filter((u) => !memberIds.has(u.id))
        .map((u) => u.nickname)
        .sort((a, b) => a.localeCompare(b)),
    [users.data, memberIds],
  )

  const onSubmit = (e: FormEvent) => {
    e.preventDefault()
    setLookupError(null)
    const trimmed = nickname.trim()
    if (trimmed === '') return

    const match = users.data?.find(
      (u) => u.nickname.localeCompare(trimmed, undefined, { sensitivity: 'accent' }) === 0,
    )
    if (!match) {
      setLookupError('No user found with that nickname.')
      return
    }
    if (memberIds.has(match.id)) {
      setLookupError('That user is already a member.')
      return
    }

    invite.mutate(match.id, {
      onSuccess: () => setNickname(''),
      onError: () => setLookupError(null),
    })
  }

  return (
    <Card>
      <CardHeader>
        <h2 className="text-sm font-semibold text-gray-900">Invite a user</h2>
      </CardHeader>
      <CardBody className="flex flex-col gap-3">
        <form className="flex items-end gap-2" onSubmit={onSubmit}>
          <FormField label="Nickname" className="flex-1">
            {({ id }) => (
              <>
                <Input
                  id={id}
                  list="group-invite-nicknames"
                  value={nickname}
                  onChange={(e) => setNickname(e.target.value)}
                  placeholder="Search by nickname"
                  autoComplete="off"
                />
                <datalist id="group-invite-nicknames">
                  {suggestions.map((name) => (
                    <option key={name} value={name} />
                  ))}
                </datalist>
              </>
            )}
          </FormField>
          <Button type="submit" isLoading={invite.isPending} disabled={nickname.trim() === ''}>
            Invite
          </Button>
        </form>
        {lookupError && (
          <p className="text-sm text-red-600" role="alert">
            {lookupError}
          </p>
        )}
        {invite.isError && (
          <ErrorState title="Could not invite" message={getErrorMessage(invite.error)} />
        )}
        {invite.isSuccess && <Badge color="green">Invitation sent</Badge>}
      </CardBody>
    </Card>
  )
}

function PendingInvitationsCard({ groupId }: { groupId: number }) {
  const invitations = useGroupInvitations(groupId)
  const cancel = useCancelInvitation(groupId)
  const [cancelId, setCancelId] = useState<number | null>(null)

  return (
    <Card id="invitations">
      <CardHeader>
        <h2 className="text-sm font-semibold text-gray-900">Pending invitations</h2>
      </CardHeader>
      <CardBody>
        <QueryBoundary
          isLoading={invitations.isLoading}
          isError={invitations.isError}
          error={invitations.error}
          onRetry={() => invitations.refetch()}
        >
          {invitations.data && invitations.data.length > 0 ? (
            <ul className="flex flex-col gap-2">
              {invitations.data.map((invitation) => (
                <li key={invitation.id}>
                  <UserRow
                    userId={invitation.inviteeId}
                    nickname={invitation.inviteeNickname}
                    subtitle={`Invited by ${invitation.inviterNickname} · ${formatDateTime(invitation.createdAt)}`}
                    actions={
                      <Button
                        size="sm"
                        variant="danger"
                        onClick={() => setCancelId(invitation.id)}
                      >
                        Cancel
                      </Button>
                    }
                  />
                </li>
              ))}
            </ul>
          ) : (
            <EmptyState title="No pending invitations" />
          )}
        </QueryBoundary>
      </CardBody>

      <ConfirmDialog
        isOpen={cancelId != null}
        title="Cancel invitation"
        message="The invitee will no longer be able to accept this invitation."
        confirmLabel="Cancel invitation"
        destructive
        isLoading={cancel.isPending}
        onConfirm={() => {
          if (cancelId == null) return
          cancel.mutate(cancelId, { onSuccess: () => setCancelId(null) })
        }}
        onCancel={() => setCancelId(null)}
      />
    </Card>
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

  const canChangeRole = isOwner && !isSelf && !memberIsOwner
  const canTransfer = isOwner && !isSelf && !memberIsOwner
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
