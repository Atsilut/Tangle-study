import { type FormEvent, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { Button, ErrorState, FormField, Input } from '@/components/ui'
import { getErrorMessage } from '@/lib/apiError'
import { isNicknameAvailable } from '../api'
import { useLogin, useRegister } from '../hooks'
import { AuthCard } from '../components/AuthCard'

// Mirrors backend validation: password 8-32 chars with letters and numbers.
const passwordPattern = /^(?=.*[A-Za-z])(?=.*\d)[A-Za-z\d!@#$%^&*()_+=-]{8,32}$/

async function validateNickname(value: string): Promise<string | undefined> {
  const trimmed = value.trim()
  if (!trimmed) return 'Nickname is required.'

  const available = await isNicknameAvailable(trimmed)
  if (!available) return `A user with nickname '${trimmed}' already exists.`

  return undefined
}

export function RegisterPage() {
  const navigate = useNavigate()
  const registerMutation = useRegister()
  const login = useLogin()
  const [email, setEmail] = useState('')
  const [nickname, setNickname] = useState('')
  const [password, setPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [passwordError, setPasswordError] = useState<string>()
  const [confirmPasswordError, setConfirmPasswordError] = useState<string>()
  const [nicknameError, setNicknameError] = useState<string>()
  const [nicknameChecking, setNicknameChecking] = useState(false)

  const checkNickname = async (value: string) => {
    setNicknameChecking(true)
    try {
      const error = await validateNickname(value)
      setNicknameError(error)
      return error == null
    } finally {
      setNicknameChecking(false)
    }
  }

  const onSubmit = async (e: FormEvent) => {
    e.preventDefault()

    let hasError = false
    if (!passwordPattern.test(password)) {
      setPasswordError('Password must be 8-32 characters and include letters and numbers.')
      hasError = true
    } else {
      setPasswordError(undefined)
    }

    if (password !== confirmPassword) {
      setConfirmPasswordError('Passwords do not match.')
      hasError = true
    } else {
      setConfirmPasswordError(undefined)
    }

    const nicknameOk = await checkNickname(nickname)
    if (!nicknameOk) hasError = true

    if (hasError) return

    registerMutation.mutate(
      { email, password, nickname: nickname.trim() },
      {
        onSuccess: () =>
          login.mutate({ email, password }, { onSuccess: () => navigate('/', { replace: true }) }),
        onError: (error) => {
          const message = getErrorMessage(error)
          if (message.includes('nickname')) setNicknameError(message)
        },
      },
    )
  }

  const isPending = registerMutation.isPending || login.isPending || nicknameChecking

  return (
    <AuthCard
      title="Create account"
      footer={
        <>
          Already have an account?{' '}
          <Link to="/login" className="font-medium text-blue-600 hover:underline">
            Sign in
          </Link>
        </>
      }
    >
      <form className="flex flex-col gap-4" onSubmit={onSubmit} noValidate>
        {registerMutation.isError && !nicknameError && (
          <ErrorState
            title="Registration failed"
            message={getErrorMessage(registerMutation.error)}
          />
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
        <FormField
          label="Password"
          required
          error={passwordError}
          hint="8-32 characters, with letters and numbers."
        >
          {({ id, describedBy, invalid }) => (
            <Input
              id={id}
              type="password"
              autoComplete="new-password"
              aria-describedby={describedBy}
              value={password}
              invalid={invalid}
              onChange={(e) => {
                setPassword(e.target.value)
                setPasswordError(undefined)
                if (confirmPasswordError && e.target.value === confirmPassword) {
                  setConfirmPasswordError(undefined)
                }
              }}
              required
            />
          )}
        </FormField>
        <FormField label="Confirm password" required error={confirmPasswordError}>
          {({ id, describedBy, invalid }) => (
            <Input
              id={id}
              type="password"
              autoComplete="new-password"
              aria-describedby={describedBy}
              value={confirmPassword}
              invalid={invalid}
              onChange={(e) => {
                setConfirmPassword(e.target.value)
                setConfirmPasswordError(undefined)
              }}
              required
            />
          )}
        </FormField>
        <FormField label="Nickname" required error={nicknameError}>
          {({ id, describedBy, invalid }) => (
            <Input
              id={id}
              autoComplete="nickname"
              aria-describedby={describedBy}
              value={nickname}
              invalid={invalid}
              onChange={(e) => {
                setNickname(e.target.value)
                setNicknameError(undefined)
              }}
              onBlur={() => {
                if (nickname.trim()) void checkNickname(nickname)
              }}
              required
            />
          )}
        </FormField>
        <Button type="submit" isLoading={isPending}>
          Create account
        </Button>
      </form>
    </AuthCard>
  )
}
