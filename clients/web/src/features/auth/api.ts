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

// GET /api/join/nickname-available?nickname= -> 200 { available }
export async function isNicknameAvailable(nickname: string): Promise<boolean> {
  const res = await api.get<{ available: boolean }>('/join/nickname-available', {
    params: { nickname },
  })
  return res.data.available
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
