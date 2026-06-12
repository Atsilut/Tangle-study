import { useMutation } from '@tanstack/react-query'
import { useAuthStore } from '@/stores/authStore'
import { login, register, type LoginRequest, type RegisterRequest } from './api'

export function useLogin() {
  const setToken = useAuthStore((s) => s.setToken)
  return useMutation({
    mutationFn: (body: LoginRequest) => login(body),
    onSuccess: (data) => setToken(data.accessToken),
  })
}

export function useRegister() {
  return useMutation({
    mutationFn: (body: RegisterRequest) => register(body),
  })
}
