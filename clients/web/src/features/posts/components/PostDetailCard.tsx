import { Link } from 'react-router-dom'
import { Avatar, Button, Card, CardBody, CardHeader } from '@/components/ui'
import { EditedTimestamp } from '@/components/common/EditedTimestamp'
import { MediaGallery } from '@/features/media'
import type { Post } from '@/features/posts/api'

export interface PostDetailCardProps {
  post: Post
  isAuthor: boolean
  editUrl?: string
  onDelete?: () => void
}

export function PostDetailCard({ post, isAuthor, editUrl, onDelete }: PostDetailCardProps) {
  return (
    <Card>
      <CardHeader className="flex items-center gap-3">
        <Avatar name={post.authorNickname} />
        <div>
          <Link
            to={`/users/${post.authorId}`}
            className="text-sm font-medium text-gray-900 hover:underline"
          >
            {post.authorNickname}
          </Link>
          <EditedTimestamp createdAt={post.createdAt} updatedAt={post.updatedAt} />
        </div>
        {isAuthor && (editUrl || onDelete) && (
          <div className="ml-auto flex gap-2">
            {editUrl && (
              <Link to={editUrl}>
                <Button size="sm" variant="secondary">
                  Edit
                </Button>
              </Link>
            )}
            {onDelete && (
              <Button size="sm" variant="danger" onClick={onDelete}>
                Delete
              </Button>
            )}
          </div>
        )}
      </CardHeader>
      <CardBody className="flex flex-col gap-3">
        <h1 className="text-xl font-bold text-gray-900">{post.title}</h1>
        <p className="whitespace-pre-wrap text-sm text-gray-700">{post.content}</p>
        <MediaGallery assets={post.media} />
      </CardBody>
    </Card>
  )
}
