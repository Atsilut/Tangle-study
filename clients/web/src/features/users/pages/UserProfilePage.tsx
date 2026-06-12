import { Link, useParams } from 'react-router-dom'
import { Avatar, Badge, Button, Card, CardBody, CardHeader } from '@/components/ui'
import { QueryBoundary } from '@/components/common/QueryBoundary'
import { useAuthStore } from '@/stores/authStore'
import { useSendFriendRequest, UserFriendsList } from '@/features/friends'
import { UserPostsList } from '@/features/posts'
import { useBlockUser, useMyBlocks, useUnblockUser } from '@/features/blocks'
import { useUser } from '../hooks'
import { friendsListVisibilityLabels } from '../labels'

export function UserProfilePage() {
  const { id } = useParams<{ id: string }>()
  const userId = Number(id)
  const currentUserId = useAuthStore((s) => s.userId)
  const isAuthenticated = useAuthStore((s) => s.isAuthenticated)
  const { data, isLoading, isError, refetch } = useUser(Number.isFinite(userId) ? userId : null)
  const sendRequest = useSendFriendRequest()
  const blockUser = useBlockUser()
  const unblockUser = useUnblockUser()
  const isSelf = currentUserId === userId
  const { data: myBlocks } = useMyBlocks({ enabled: isAuthenticated && !isSelf })
  const blockRecord = myBlocks?.find((b) => b.blockedUserId === userId)

  return (
    <div className="flex flex-col gap-4">
      <QueryBoundary isLoading={isLoading} isError={isError} onRetry={() => refetch()}>
        {data && (
          <Card>
            <CardHeader className="flex items-center gap-3">
              <Avatar name={data.nickname} size="lg" />
              <div>
                <h1 className="text-xl font-bold text-gray-900">{data.nickname}</h1>
                <p className="text-sm text-gray-500">{data.email}</p>
              </div>
              {isSelf ? (
                <Link to="/settings" className="ml-auto text-sm text-blue-600 hover:underline">
                  Edit profile
                </Link>
              ) : (
                isAuthenticated && (
                  <div className="ml-auto flex items-center gap-2">
                    {sendRequest.isSuccess ? (
                      <Badge color="green">Request sent</Badge>
                    ) : (
                      <Button
                        size="sm"
                        isLoading={sendRequest.isPending}
                        onClick={() => sendRequest.mutate(userId)}
                      >
                        Add friend
                      </Button>
                    )}
                    {blockRecord ? (
                      <Button
                        size="sm"
                        variant="secondary"
                        isLoading={unblockUser.isPending}
                        onClick={() => unblockUser.mutate(blockRecord.id)}
                      >
                        Unblock
                      </Button>
                    ) : (
                      <Button
                        size="sm"
                        variant="secondary"
                        isLoading={blockUser.isPending}
                        onClick={() => blockUser.mutate(userId)}
                      >
                        Block
                      </Button>
                    )}
                  </div>
                )
              )}
            </CardHeader>
            <CardBody className="flex flex-col gap-2 text-sm text-gray-600">
              <div className="flex items-center gap-2">
                <span className="text-gray-500">Friends list:</span>
                <Badge>{friendsListVisibilityLabels[data.friendsListVisibility]}</Badge>
              </div>
              <p className="text-xs text-gray-400">
                Joined {new Date(data.createdAt).toLocaleDateString()}
              </p>
            </CardBody>
          </Card>
        )}
      </QueryBoundary>
      {data && (
        <>
          <UserPostsList nickname={data.nickname} />
          {Number.isFinite(userId) && (
            <UserFriendsList
              userId={userId}
              nickname={data.nickname}
              visibility={data.friendsListVisibility}
              isSelf={isSelf}
            />
          )}
        </>
      )}
    </div>
  )
}
