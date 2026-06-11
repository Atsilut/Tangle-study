import { Link } from 'react-router-dom'

export function NotFoundPage() {
  return (
    <main className="mx-auto flex min-h-screen max-w-2xl flex-col justify-center gap-4 p-6">
      <h1 className="text-2xl font-bold">404</h1>
      <p className="text-gray-600">This page does not exist.</p>
      <Link className="text-blue-600 underline" to="/">
        Back home
      </Link>
    </main>
  )
}
