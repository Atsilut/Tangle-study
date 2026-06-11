import { Link, Navigate, useNavigate, useParams } from 'react-router-dom'
import { Badge, Card, CardBody, CardHeader, EmptyState } from '@/components/ui'
import { CenteredSpinner } from '@/components/common/CenteredSpinner'
import { QueryBoundary } from '@/components/common/QueryBoundary'
import { formatChatListTimestamp } from '@/lib/format'
import { useAuthStore } from '@/stores/authStore'
import { useGroupMembers, useMyGroupRole } from '@/features/groups'
import { CreateChatRoomForm } from '../components/CreateChatRoomForm'
import { useCreateGroupRoom, useGroupRooms, useMyRooms } from '../hooks'
import { chatRoomKindLabels, summaryLabel } from '../labels'

export function GroupChatRoomsPage() {
  const { id } = useParams<{ id: string }>()
  const groupId = Number(id)
  const valid = Number.isFinite(groupId)
  const { role, isLoading: roleLoading } = useMyGroupRole(valid ? groupId : null)
  const rooms = useGroupRooms(valid ? groupId : null)
  const myRooms = useMyRooms()
  const participantRoomIds = new Set(myRooms.data?.map((r) => r.id) ?? [])

  if (roleLoading) return <CenteredSpinner />
  // Group chat rooms are members-only.
  if (role == null) return <Navigate to={`/groups/${groupId}`} replace />

  return (
    <div className="flex max-w-2xl flex-col gap-4">
      <Link to={`/groups/${groupId}`} className="text-sm text-blue-600 hover:underline">
        Back to group
      </Link>
      <h1 className="text-2xl font-bold text-gray-900">Group chat rooms</h1>

      <CreateRoomCard groupId={groupId} />

      <QueryBoundary
        isLoading={rooms.isLoading}
        isError={rooms.isError}
        onRetry={() => rooms.refetch()}
      >
        {rooms.data && rooms.data.length > 0 ? (
          <ul className="flex flex-col gap-2">
            {rooms.data.map((room) => {
              const canEnter = participantRoomIds.has(room.id)
              const card = (
                <Card
                  className={`flex items-center gap-3 px-4 py-3 ${canEnter ? 'hover:bg-gray-50' : ''}`}
                >
                  <div className="min-w-0 flex-1">
                    <span className="block truncate text-sm font-medium text-gray-900">
                      {summaryLabel(room)}
                    </span>
                    <span className="shrink-0 text-xs text-gray-500">
                      {formatChatListTimestamp(room.updatedAt)}
                    </span>
                  </div>
                  <div className="flex shrink-0 items-center gap-2">
                    {!canEnter && <Badge color="gray">Not a participant</Badge>}
                    <Badge>{chatRoomKindLabels[room.kind]}</Badge>
                  </div>
                </Card>
              )
              return (
                <li key={room.id}>
                  {canEnter ? (
                    <Link to={`/chat/${room.id}`} className="block">
                      {card}
                    </Link>
                  ) : (
                    card
                  )}
                </li>
              )
            })}
          </ul>
        ) : (
          <EmptyState title="No chat rooms yet" description="Create one below." />
        )}
      </QueryBoundary>
    </div>
  )
}

function CreateRoomCard({ groupId }: { groupId: number }) {
  const currentUserId = useAuthStore((s) => s.userId)
  const members = useGroupMembers(groupId)
  const create = useCreateGroupRoom(groupId)
  const navigate = useNavigate()

  const others = (members.data ?? [])
    .filter((m) => m.userId !== currentUserId)
    .map((m) => ({ userId: m.userId, nickname: m.nickname }))

  return (
    <Card>
      <CardHeader>
        <h2 className="text-sm font-semibold text-gray-900">New chat room</h2>
      </CardHeader>
      <CardBody>
        <CreateChatRoomForm
          participants={others}
          isPending={create.isPending}
          error={create.isError ? create.error : undefined}
          onSubmit={(userIds, title) =>
            create.mutate({ userIds, title }, { onSuccess: (room) => navigate(`/chat/${room.id}`) })
          }
        />
      </CardBody>
    </Card>
  )
}
