import { useQuery } from '@tanstack/react-query'

// Minimal scaffold landing page. Hits /health through the dev proxy to confirm
// the Vite -> Nginx -> API wiring works. Feature pages replace this later.
async function fetchHealth(): Promise<string> {
  const res = await fetch('/health')
  return res.ok ? 'healthy' : `unhealthy (${res.status})`
}

export function HomePage() {
  const { data, isLoading, isError } = useQuery({
    queryKey: ['health'],
    queryFn: fetchHealth,
  })

  const status = isLoading ? 'checking…' : isError ? 'unreachable' : data

  return (
    <main className="mx-auto flex min-h-screen max-w-2xl flex-col justify-center gap-4 p-6">
      <h1 className="text-3xl font-bold">Tangle</h1>
      <p className="text-gray-600">Phase 6 web client scaffold.</p>
      <p className="text-sm">
        API health: <span className="font-mono">{status}</span>
      </p>
    </main>
  )
}
