import { Link, useParams } from 'react-router-dom'
import { Avatar, Badge, Card, CardBody, CardHeader } from '@/components/ui'
import { QueryBoundary } from '@/components/common/QueryBoundary'
import { EditedTimestamp } from '@/components/common/EditedTimestamp'
import { CommentSection } from '@/features/comments'
import { useBoardPost } from '../boardsHooks'

export function GroupBoardPostDetailPage() {
  const { id, boardId, postId } = useParams<{
    id: string
    boardId: string
    postId: string
  }>()
  const groupId = Number(id)
  const board = Number(boardId)
  const post = Number(postId)
  const valid = Number.isFinite(groupId) && Number.isFinite(board) && Number.isFinite(post)
  const { data, isLoading, isError, refetch } = useBoardPost(
    valid ? groupId : null,
    valid ? board : null,
    valid ? post : null,
  )

  return (
    <div className="flex max-w-2xl flex-col gap-4">
      <Link
        to={`/groups/${groupId}/boards/${board}`}
        className="text-sm text-blue-600 hover:underline"
      >
        Back to board
      </Link>
      <QueryBoundary isLoading={isLoading} isError={isError} onRetry={() => refetch()}>
        {data && (
          <Card>
            <CardHeader className="flex items-center gap-3">
              <Avatar name={data.authorNickname} />
              <div>
                <Link
                  to={`/users/${data.authorId}`}
                  className="text-sm font-medium text-gray-900 hover:underline"
                >
                  {data.authorNickname}
                </Link>
                <EditedTimestamp createdAt={data.createdAt} updatedAt={data.updatedAt} />
              </div>
            </CardHeader>
            <CardBody className="flex flex-col gap-3">
              <h1 className="text-xl font-bold text-gray-900">{data.title}</h1>
              <p className="whitespace-pre-wrap text-sm text-gray-700">{data.content}</p>
              {data.media.length > 0 && (
                <Badge color="blue">{data.media.length} media attached</Badge>
              )}
            </CardBody>
          </Card>
        )}
      </QueryBoundary>

      {data && <CommentSection postId={data.id} />}
    </div>
  )
}
