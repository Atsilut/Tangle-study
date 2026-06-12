import { Link } from 'react-router-dom'
import { EditedTimestamp } from '@/components/common/EditedTimestamp'
import { Avatar, Badge, Card } from '@/components/ui'
import type { Post } from '../api'

export interface PostCardProps {
  post: Post
}

// Compact list item linking to the post detail. Reused by feed and profile.
export function PostCard({ post }: PostCardProps) {
  return (
    <Link to={`/posts/${post.id}`} className="block">
      <Card className="px-4 py-3 hover:bg-gray-50">
        <div className="flex items-center gap-2">
          <Avatar name={post.authorNickname} size="sm" />
          <span className="text-sm font-medium text-gray-900">{post.authorNickname}</span>
          <EditedTimestamp
            createdAt={post.createdAt}
            updatedAt={post.updatedAt}
            variant="date"
          />
          {post.media.length > 0 && (
            <Badge color="blue">
              {post.media.length} media
            </Badge>
          )}
        </div>
        <h3 className="mt-2 text-base font-semibold text-gray-900">{post.title}</h3>
        <p className="mt-1 line-clamp-2 text-sm text-gray-600">{post.content}</p>
      </Card>
    </Link>
  )
}
