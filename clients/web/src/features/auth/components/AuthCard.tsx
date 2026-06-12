import type { ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { Card, CardBody } from '@/components/ui'

export interface AuthCardProps {
  title: string
  children: ReactNode
  footer: ReactNode
}

export function AuthCard({ title, children, footer }: AuthCardProps) {
  return (
    <main className="flex min-h-screen items-center justify-center bg-gray-50 p-4">
      <div className="w-full max-w-sm">
        <Link to="/" className="mb-6 block text-center text-2xl font-bold text-gray-900">
          Tangle
        </Link>
        <Card>
          <CardBody className="flex flex-col gap-4">
            <h1 className="text-lg font-semibold text-gray-900">{title}</h1>
            {children}
          </CardBody>
        </Card>
        <p className="mt-4 text-center text-sm text-gray-600">{footer}</p>
      </div>
    </main>
  )
}
