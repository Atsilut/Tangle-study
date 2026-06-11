import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  createPost,
  deletePost,
  getPost,
  getPosts,
  getPostsByNickname,
  updatePost,
  type CreatePostRequest,
  type UpdatePostRequest,
} from './api'

export const postKeys = {
  all: ['posts'] as const,
  lists: () => [...postKeys.all, 'list'] as const,
  byNickname: (nickname: string) => [...postKeys.lists(), 'nickname', nickname] as const,
  details: () => [...postKeys.all, 'detail'] as const,
  detail: (id: number) => [...postKeys.details(), id] as const,
}

export function usePosts() {
  return useQuery({ queryKey: postKeys.lists(), queryFn: getPosts })
}

export function usePostsByNickname(nickname: string | null) {
  return useQuery({
    queryKey: postKeys.byNickname(nickname ?? ''),
    queryFn: () => getPostsByNickname(nickname as string),
    enabled: nickname != null && nickname.length > 0,
  })
}

export function usePost(id: number | null) {
  return useQuery({
    queryKey: postKeys.detail(id ?? -1),
    queryFn: () => getPost(id as number),
    enabled: id != null,
  })
}

export function useCreatePost() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (body: CreatePostRequest) => createPost(body),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: postKeys.lists() }),
  })
}

export function useUpdatePost() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (body: UpdatePostRequest) => updatePost(body),
    onSuccess: (_data, variables) => {
      queryClient.invalidateQueries({ queryKey: postKeys.detail(variables.id) })
      queryClient.invalidateQueries({ queryKey: postKeys.lists() })
    },
  })
}

export function useDeletePost() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: number) => deletePost(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: postKeys.lists() }),
  })
}
