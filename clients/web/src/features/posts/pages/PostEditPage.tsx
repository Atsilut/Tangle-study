import { useNavigate, useParams } from 'react-router-dom'
import { Card, CardBody, CardHeader } from '@/components/ui'
import { QueryBoundary } from '@/components/common/QueryBoundary'
import { useAuthStore } from '@/stores/authStore'
import { useUpdatePost, usePost } from '../hooks'
import { PostForm } from '../components/PostForm'
import type { Post } from '../api'

export function PostEditPage() {
  const { id } = useParams<{ id: string }>()
  const postId = Number(id)
  const { data: post, isLoading, isError, error, refetch } = usePost(
    Number.isFinite(postId) ? postId : null,
  )

  return (
    <div className="flex max-w-2xl flex-col gap-4">
      <h1 className="text-2xl font-bold text-gray-900">Edit post</h1>
      <QueryBoundary isLoading={isLoading} isError={isError} error={error} onRetry={() => refetch()}>
        {post && <EditForm post={post} />}
      </QueryBoundary>
    </div>
  )
}

function EditForm({ post }: { post: Post }) {
  const navigate = useNavigate()
  const currentUserId = useAuthStore((s) => s.userId)
  const updatePost = useUpdatePost()

  if (currentUserId !== post.authorId) {
    return <p className="text-sm text-gray-600">You can only edit your own posts.</p>
  }

  return (
    <Card>
      <CardHeader>
        <h2 className="text-sm font-semibold text-gray-900">Update your post</h2>
      </CardHeader>
      <CardBody>
        <PostForm
          initial={{ title: post.title, content: post.content }}
          initialLocation={post.location ?? null}
          existingMedia={post.media}
          enableMedia
          submitLabel="Save changes"
          isPending={updatePost.isPending}
          error={updatePost.error}
          onSubmit={(values) =>
            updatePost.mutate(
              {
                id: post.id,
                title: values.title,
                content: values.content,
                addMediaAssetIds: values.addMediaAssetIds,
                removeMediaAssetIds: values.removeMediaAssetIds,
                latitude: values.latitude,
                longitude: values.longitude,
                clearLocation: values.clearLocation,
              },
              { onSuccess: () => navigate(`/posts/${post.id}`) },
            )
          }
          onCancel={() => navigate(`/posts/${post.id}`)}
        />
      </CardBody>
    </Card>
  )
}
