import { useNavigate } from 'react-router-dom'
import { Card, CardBody, CardHeader } from '@/components/ui'
import { useCreatePost } from '../hooks'
import { PostForm } from '../components/PostForm'

export function PostCreatePage() {
  const navigate = useNavigate()
  const createPost = useCreatePost()

  return (
    <div className="flex max-w-2xl flex-col gap-4">
      <h1 className="text-2xl font-bold text-gray-900">New post</h1>
      <Card>
        <CardHeader>
          <h2 className="text-sm font-semibold text-gray-900">Write a post</h2>
        </CardHeader>
        <CardBody>
          <PostForm
            submitLabel="Publish"
            isPending={createPost.isPending}
            error={createPost.error}
            enableMedia
            onSubmit={(values) =>
              createPost.mutate(values, { onSuccess: () => navigate('/posts') })
            }
            onCancel={() => navigate('/posts')}
          />
        </CardBody>
      </Card>
    </div>
  )
}
