import { Link } from 'react-router-dom'
import { Button, Card, CardBody, EmptyState } from '@/components/ui'
import { useAuthStore } from '@/stores/authStore'

const features = [
  {
    title: 'Community',
    description: 'Share posts, comment, and keep up with friends and groups.',
  },
  {
    title: 'Real-time chat',
    description: 'Message one-on-one or in group rooms with live updates.',
  },
  {
    title: 'Memory map',
    description: 'Explore location-based memories and shared places.',
  },
] as const

export function HomePage() {
  const isAuthenticated = useAuthStore((s) => s.isAuthenticated)

  if (isAuthenticated) {
    return (
      <div className="flex flex-col gap-6">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">Home</h1>
          <p className="text-sm text-gray-600">Posts from your network appear here.</p>
        </div>

        <EmptyState
          title="Your feed is empty"
          description="When friends and groups start posting, you will see their updates here."
        />
      </div>
    )
  }

  return (
    <div className="flex flex-col gap-8">
      <section className="flex flex-col gap-4">
        <div className="flex flex-col gap-2">
          <h1 className="text-3xl font-bold tracking-tight text-gray-900">
            Connect, share, and explore together
          </h1>
          <p className="max-w-2xl text-base text-gray-600">
            Tangle brings community posts, real-time chat, and a memory map into one place for
            staying close to the people and places that matter.
          </p>
        </div>

        <div className="flex flex-wrap items-center gap-3">
          <Link to="/register">
            <Button>Get started</Button>
          </Link>
          <Link to="/login">
            <Button variant="secondary">Log in</Button>
          </Link>
        </div>
      </section>

      <section className="grid gap-4 sm:grid-cols-3">
        {features.map((feature) => (
          <Card key={feature.title}>
            <CardBody className="flex flex-col gap-2 py-4">
              <h2 className="text-sm font-semibold text-gray-900">{feature.title}</h2>
              <p className="text-sm text-gray-600">{feature.description}</p>
            </CardBody>
          </Card>
        ))}
      </section>
    </div>
  )
}
