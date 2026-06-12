import { type FormEvent, useState } from 'react'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import { Button, ErrorState, FormField, Input } from '@/components/ui'
import { getErrorMessage } from '@/lib/apiError'
import { useLogin } from '../hooks'
import { AuthCard } from '../components/AuthCard'

interface LocationState {
  from?: { pathname: string }
}

export function LoginPage() {
  const navigate = useNavigate()
  const location = useLocation()
  const login = useLogin()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')

  const redirectTo = (location.state as LocationState | null)?.from?.pathname ?? '/'

  const onSubmit = (e: FormEvent) => {
    e.preventDefault()
    login.mutate(
      { email, password },
      { onSuccess: () => navigate(redirectTo, { replace: true }) },
    )
  }

  return (
    <AuthCard
      title="Sign in"
      footer={
        <>
          No account?{' '}
          <Link to="/register" className="font-medium text-blue-600 hover:underline">
            Sign up
          </Link>
        </>
      }
    >
      <form className="flex flex-col gap-4" onSubmit={onSubmit} noValidate>
        {login.isError && (
          <ErrorState title="Login failed" message={getErrorMessage(login.error, 'Invalid email or password.')} />
        )}
        <FormField label="Email" required>
          {({ id, invalid }) => (
            <Input
              id={id}
              type="email"
              autoComplete="email"
              value={email}
              invalid={invalid}
              onChange={(e) => setEmail(e.target.value)}
              required
            />
          )}
        </FormField>
        <FormField label="Password" required>
          {({ id, invalid }) => (
            <Input
              id={id}
              type="password"
              autoComplete="current-password"
              value={password}
              invalid={invalid}
              onChange={(e) => setPassword(e.target.value)}
              required
            />
          )}
        </FormField>
        <Button type="submit" isLoading={login.isPending}>
          Sign in
        </Button>
      </form>
    </AuthCard>
  )
}
