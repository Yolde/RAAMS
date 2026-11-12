import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useAuth } from '../auth/AuthProvider'
import { client } from './client'
import type {
  Action, ActionFilter, Assessment, AssessmentFilter, CommentAction,
  CreateAssessment, Me, PaginatedResult, SubmitAction,
} from './types'

export function useMe() {
  const { token } = useAuth()
  return useQuery({
    queryKey: ['me', token],
    queryFn: async ({ signal }) => (await client.get<Me>('/api/auth/me', { signal })).data,
    enabled: !!token,
    retry: false,
  })
}

export function useAssessments(filters?: AssessmentFilter) {
  const { token } = useAuth()
  return useQuery({
    queryKey: ['assessments', token, filters],
    queryFn: async ({ signal }) =>
      (await client.get<PaginatedResult<Assessment>>('/api/assessments', { params: filters, signal })).data,
    enabled: !!token,
    staleTime: 30_000,
  })
}

export function useAssessment(id: string) {
  const { token } = useAuth()
  return useQuery({
    queryKey: ['assessment', token, id],
    queryFn: async ({ signal }) =>
      (await client.get<Assessment>(`/api/assessments/${encodeURIComponent(id)}`, { signal })).data,
    enabled: !!token && !!id,
  })
}

export function useCreateAssessment() {
  const queryClient = useQueryClient()
  const { token } = useAuth()
  return useMutation({
    mutationFn: async (dto: CreateAssessment) =>
      (await client.post<Assessment>('/api/assessments', dto)).data,
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['assessments', token] })
    },
  })
}

export function useActions(filters?: ActionFilter) {
  const { token } = useAuth()
  return useQuery({
    queryKey: ['actions', token, filters],
    queryFn: async ({ signal }) =>
      (await client.get<PaginatedResult<Action>>('/api/actions', { params: filters, signal })).data,
    enabled: !!token,
    staleTime: 30_000,
  })
}

export function useAction(id: string) {
  const { token } = useAuth()
  return useQuery({
    queryKey: ['action', token, id],
    queryFn: async ({ signal }) =>
      (await client.get<Action>(`/api/actions/${encodeURIComponent(id)}`, { signal })).data,
    enabled: !!token && !!id,
  })
}

function useActionTransition<T extends { id: string }>(
  operation: 'submit' | 'approve' | 'reject' | 'reopen',
  body: (value: T) => object,
) {
  const queryClient = useQueryClient()
  const { token } = useAuth()
  return useMutation({
    mutationFn: async (value: T) =>
      (await client.post<Action>(`/api/actions/${encodeURIComponent(value.id)}/${operation}`, body(value))).data,
    onSuccess: async (_, value) => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['actions', token] }),
        queryClient.invalidateQueries({ queryKey: ['action', token, value.id] }),
      ])
    },
  })
}

export function useSubmitAction() {
  return useActionTransition<SubmitAction>('submit', ({ actionTaken }) => ({ actionTaken }))
}

export function useApproveAction() {
  return useActionTransition<CommentAction>('approve', ({ comments }) => ({ comments }))
}

export function useRejectAction() {
  return useActionTransition<CommentAction>('reject', ({ comments }) => ({ comments }))
}

export function useReopenAction() {
  return useActionTransition<CommentAction>('reopen', ({ comments }) => ({ comments }))
}
