import { api } from '@/lib/apiClient'

export interface RegisterRequest {
  email: string
  password: string
  nickname: string
}

export interface LoginRequest {
  email: string
  password: string
}

export interface LoginResponse {
  accessToken: string
}

// POST /api/join -> 201 Created (empty body)
export async function register(body: RegisterRequest): Promise<void> {
  await api.post('/join', body)
}

// POST /api/login -> 200 { accessToken } | 401
export async function login(body: LoginRequest): Promise<LoginResponse> {
  const res = await api.post<LoginResponse>('/login', body)
  return res.data
}
