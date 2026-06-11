import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  createBoard,
  createBoardPost,
  deleteBoard,
  getBoardPost,
  getBoardPosts,
  getBoards,
  updateBoard,
  type CreateBoardPostRequest,
  type CreateBoardRequest,
  type UpdateBoardRequest,
} from './boardsApi'

export const boardKeys = {
  all: ['boards'] as const,
  list: (groupId: number) => [...boardKeys.all, 'group', groupId] as const,
  posts: (groupId: number, boardId: number) =>
    [...boardKeys.all, 'group', groupId, 'board', boardId, 'posts'] as const,
  post: (groupId: number, boardId: number, postId: number) =>
    [...boardKeys.all, 'group', groupId, 'board', boardId, 'post', postId] as const,
}

export function useBoards(groupId: number | null) {
  return useQuery({
    queryKey: boardKeys.list(groupId ?? -1),
    queryFn: () => getBoards(groupId as number),
    enabled: groupId != null,
  })
}

export function useCreateBoard(groupId: number) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (body: CreateBoardRequest) => createBoard(groupId, body),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: boardKeys.list(groupId) }),
  })
}

export function useUpdateBoard(groupId: number) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ boardId, body }: { boardId: number; body: UpdateBoardRequest }) =>
      updateBoard(groupId, boardId, body),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: boardKeys.list(groupId) }),
  })
}

export function useDeleteBoard(groupId: number) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (boardId: number) => deleteBoard(groupId, boardId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: boardKeys.list(groupId) }),
  })
}

export function useBoardPosts(groupId: number | null, boardId: number | null) {
  return useQuery({
    queryKey: boardKeys.posts(groupId ?? -1, boardId ?? -1),
    queryFn: () => getBoardPosts(groupId as number, boardId as number),
    enabled: groupId != null && boardId != null,
  })
}

export function useBoardPost(
  groupId: number | null,
  boardId: number | null,
  postId: number | null,
) {
  return useQuery({
    queryKey: boardKeys.post(groupId ?? -1, boardId ?? -1, postId ?? -1),
    queryFn: () => getBoardPost(groupId as number, boardId as number, postId as number),
    enabled: groupId != null && boardId != null && postId != null,
  })
}

export function useCreateBoardPost(groupId: number, boardId: number) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (body: CreateBoardPostRequest) => createBoardPost(groupId, boardId, body),
    onSuccess: () =>
      queryClient.invalidateQueries({ queryKey: boardKeys.posts(groupId, boardId) }),
  })
}
