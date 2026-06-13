import { useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import {
  Badge,
  Button,
  Card,
  CardBody,
  CardHeader,
  ConfirmDialog,
  ErrorState,
} from '@/components/ui'
import { QueryBoundary } from '@/components/common/QueryBoundary'
import { getErrorMessage } from '@/lib/apiError'
import { formatDate } from '@/lib/format'
import { GroupInvitePolicy, GroupJoinPolicy, GroupRole, GroupVisibility } from '@/types/api'
import { useDeleteGroup, useGroup, useJoinGroup } from '../hooks'
import { useMyGroupRole } from '../membersHooks'
import { useApplyToGroup } from '../applicationsHooks'
import {
  canInviteToGroup,
  groupVisibilityLabels,
  invitePolicyLabels,
  joinPolicyLabels,
} from '../labels'

export function GroupDetailPage() {
  const { id } = useParams<{ id: string }>()
  const groupId = Number(id)
  const navigate = useNavigate()
  const { data: group, isLoading, isError, refetch } = useGroup(
    Number.isFinite(groupId) ? groupId : null,
  )
  const join = useJoinGroup(groupId)
  const apply = useApplyToGroup(groupId)
  const deleteGroup = useDeleteGroup()
  const { role } = useMyGroupRole(Number.isFinite(groupId) ? groupId : null)
  const [confirmOpen, setConfirmOpen] = useState(false)

  const isMember = role != null
  const isLimitedProfile =
    group?.isLimitedProfile ??
    (group?.visibility === GroupVisibility.Private && !isMember)
  const canEdit = role === GroupRole.Owner || role === GroupRole.Admin
  const canDelete = role === GroupRole.Owner
  const canInvite =
    !isLimitedProfile &&
    group != null &&
    canInviteToGroup(group.invitePolicy ?? GroupInvitePolicy.AdminsOnly, role)

  return (
    <div className="flex max-w-2xl flex-col gap-4">
      <Link to="/groups" className="text-sm text-blue-600 hover:underline">
        Back to groups
      </Link>
      <QueryBoundary isLoading={isLoading} isError={isError} onRetry={() => refetch()}>
        {group && (
          <Card>
            <CardHeader className="flex items-start gap-3">
              <div className="flex-1">
                <h1 className="text-xl font-bold text-gray-900">{group.name}</h1>
                <div className="mt-1 flex flex-wrap gap-2">
                  <Badge color={group.visibility ? 'green' : 'gray'}>
                    {groupVisibilityLabels[group.visibility]}
                  </Badge>
                  <Badge color="blue">{joinPolicyLabels[group.joinPolicy]}</Badge>
                  {!isLimitedProfile && (
                    <Badge color="gray">
                      {invitePolicyLabels[group.invitePolicy ?? GroupInvitePolicy.AdminsOnly]}
                    </Badge>
                  )}
                  <Badge>{group.memberCount} members</Badge>
                </div>
              </div>
              {!isMember && group.joinPolicy === GroupJoinPolicy.Open && (
                <Button size="sm" isLoading={join.isPending} onClick={() => join.mutate()}>
                  Join
                </Button>
              )}
              {!isMember &&
                group.joinPolicy === GroupJoinPolicy.Requestable &&
                (apply.isSuccess ? (
                  <Badge color="green">Applied</Badge>
                ) : (
                  <Button size="sm" isLoading={apply.isPending} onClick={() => apply.mutate()}>
                    Apply
                  </Button>
                ))}
            </CardHeader>
            <CardBody className="flex flex-col gap-3">
              {isLimitedProfile ? (
                <p className="text-sm text-gray-500">
                  This is a private group. Join to see the description and group content.
                </p>
              ) : (
                <>
                  <p className="whitespace-pre-wrap text-sm text-gray-700">{group.description}</p>
                  <p className="text-xs text-gray-400">Created {formatDate(group.createdAt)}</p>
                </>
              )}

              {join.isError && (
                <ErrorState title="Could not join" message={getErrorMessage(join.error)} />
              )}
              {apply.isError && (
                <ErrorState title="Could not apply" message={getErrorMessage(apply.error)} />
              )}

              {!isLimitedProfile && (
                <div className="flex flex-wrap gap-3 border-t border-gray-100 pt-3 text-sm">
                  <Link to={`/groups/${group.id}/members`} className="text-blue-600 hover:underline">
                    Members
                  </Link>
                  {canInvite && (
                    <Link
                      to={`/groups/${group.id}/members#invitations`}
                      className="text-blue-600 hover:underline"
                    >
                      Invitations
                    </Link>
                  )}
                  <Link to={`/groups/${group.id}/boards`} className="text-blue-600 hover:underline">
                    Boards
                  </Link>
                  {isMember && (
                    <Link
                      to={`/groups/${group.id}/chat-rooms`}
                      className="text-blue-600 hover:underline"
                    >
                      Chat rooms
                    </Link>
                  )}
                  {canEdit && (
                    <Link
                      to={`/groups/${group.id}/applications`}
                      className="text-blue-600 hover:underline"
                    >
                      Applications
                    </Link>
                  )}
                  {canDelete && (
                    <Link
                      to={`/groups/${group.id}/blacklist`}
                      className="text-blue-600 hover:underline"
                    >
                      Blacklist
                    </Link>
                  )}
                </div>
              )}

              {!isLimitedProfile && (canEdit || canDelete) && (
                <div className="flex flex-wrap gap-2 border-t border-gray-100 pt-3">
                  {canEdit && (
                    <Link to={`/groups/${group.id}/edit`}>
                      <Button size="sm" variant="secondary">
                        Edit
                      </Button>
                    </Link>
                  )}
                  {canDelete && (
                    <Button size="sm" variant="danger" onClick={() => setConfirmOpen(true)}>
                      Delete
                    </Button>
                  )}
                </div>
              )}
              {deleteGroup.isError && (
                <ErrorState title="Could not delete" message={getErrorMessage(deleteGroup.error)} />
              )}
            </CardBody>
          </Card>
        )}
      </QueryBoundary>

      <ConfirmDialog
        isOpen={confirmOpen}
        title="Delete group"
        message="This permanently deletes the group and its content."
        confirmLabel="Delete"
        destructive
        isLoading={deleteGroup.isPending}
        onConfirm={() =>
          deleteGroup.mutate(groupId, {
            onSuccess: () => navigate('/groups', { replace: true }),
            onError: () => setConfirmOpen(false),
          })
        }
        onCancel={() => setConfirmOpen(false)}
      />
    </div>
  )
}
