import { Link, useNavigate } from 'react-router-dom'
import { Badge, Card, CardBody, CardHeader, EmptyState } from '@/components/ui'
import { QueryBoundary } from '@/components/common/QueryBoundary'
import { UserRow } from '@/components/common/UserRow'
import { Button } from '@/components/ui'
import { formatChatListTimestamp } from '@/lib/format'
import { useMyFriends } from '@/features/friends/hooks'
import { CreateChatRoomForm } from '../components/CreateChatRoomForm'
import {
  useChatRoomsRealtimeSync,
  useCreateMultiRoom,
  useGetOrCreateDirectRoom,
  useMyRooms,
} from '../hooks'
import { chatRoomKindLabels, summaryLabel } from '../labels'

export function ChatPage() {
  const rooms = useMyRooms()
  useChatRoomsRealtimeSync(rooms.data?.map((r) => r.id) ?? [])
  const friends = useMyFriends()
  const navigate = useNavigate()
  const openDirect = useGetOrCreateDirectRoom()
  const createMulti = useCreateMultiRoom()

  const startDirect = (otherUserId: number) => {
    openDirect.mutate(otherUserId, {
      onSuccess: (room) => navigate(`/chat/${room.id}`),
    })
  }

  return (
    <div className="flex max-w-2xl flex-col gap-6">
      <section className="flex flex-col gap-3">
        <h1 className="text-2xl font-bold text-gray-900">Chats</h1>
        <QueryBoundary
          isLoading={rooms.isLoading}
          isError={rooms.isError}
          onRetry={() => rooms.refetch()}
        >
          {rooms.data && rooms.data.length > 0 ? (
            <ul className="flex flex-col gap-2">
              {rooms.data.map((room) => (
                <li key={room.id}>
                  <Link to={`/chat/${room.id}`} className="block">
                    <Card className="flex items-center gap-3 px-4 py-3 hover:bg-gray-50">
                      <div className="min-w-0 flex-1">
                        <span className="block truncate text-sm font-medium text-gray-900">
                          {summaryLabel(room)}
                        </span>
                        <span className="shrink-0 text-xs text-gray-500">
                          {formatChatListTimestamp(room.updatedAt)}
                        </span>
                      </div>
                      <Badge>{chatRoomKindLabels[room.kind]}</Badge>
                    </Card>
                  </Link>
                </li>
              ))}
            </ul>
          ) : (
            <EmptyState
              title="No chats yet"
              description="Start a direct chat with a friend below."
            />
          )}
        </QueryBoundary>
      </section>

      <section className="flex flex-col gap-3">
        <h2 className="text-lg font-semibold text-gray-900">Start a group chat</h2>
        <QueryBoundary
          isLoading={friends.isLoading}
          isError={friends.isError}
          onRetry={() => friends.refetch()}
        >
          <Card>
            <CardHeader>
              <p className="text-sm text-gray-600">
                Pick one or more friends. You are added automatically as the room owner.
              </p>
            </CardHeader>
            <CardBody>
              <CreateChatRoomForm
                participants={(friends.data ?? []).map((friend) => ({
                  userId: friend.otherUserId,
                  nickname: friend.otherUserNickname,
                }))}
                isPending={createMulti.isPending}
                error={createMulti.isError ? createMulti.error : undefined}
                onSubmit={(userIds, title) =>
                  createMulti.mutate(
                    { userIds, title },
                    { onSuccess: (room) => navigate(`/chat/${room.id}`) },
                  )
                }
              />
            </CardBody>
          </Card>
        </QueryBoundary>
      </section>

      <section className="flex flex-col gap-3">
        <h2 className="text-lg font-semibold text-gray-900">Start a direct chat</h2>
        <QueryBoundary
          isLoading={friends.isLoading}
          isError={friends.isError}
          onRetry={() => friends.refetch()}
        >
          {friends.data && friends.data.length > 0 ? (
            <ul className="flex flex-col gap-2">
              {friends.data.map((friend) => (
                <li key={friend.id}>
                  <UserRow
                    userId={friend.otherUserId}
                    nickname={friend.otherUserNickname}
                    actions={
                      <Button
                        size="sm"
                        isLoading={openDirect.isPending}
                        onClick={() => startDirect(friend.otherUserId)}
                      >
                        Message
                      </Button>
                    }
                  />
                </li>
              ))}
            </ul>
          ) : (
            <EmptyState title="No friends yet" description="Add friends to start chatting." />
          )}
        </QueryBoundary>
      </section>
    </div>
  )
}
