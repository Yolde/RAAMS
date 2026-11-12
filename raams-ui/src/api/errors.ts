import axios from 'axios'

export function getErrorMessage(error: unknown): string {
  if (axios.isAxiosError(error)) {
    const data: unknown = error.response?.data
    if (typeof data === 'object' && data !== null) {
      if ('error' in data && typeof data.error === 'string') return data.error
      if ('errors' in data && typeof data.errors === 'object' && data.errors !== null) {
        const messages = Object.values(data.errors).flatMap((value: unknown) =>
          Array.isArray(value) ? value.filter((message): message is string => typeof message === 'string') : [],
        )
        if (messages.length) return messages.join(' ')
      }
      if ('title' in data && typeof data.title === 'string') return data.title
    }
    if (error.response?.status === 403) return 'You do not have permission to perform this action.'
    if (error.response) return `Request failed (HTTP ${error.response.status}).`
    return 'Unable to reach the API. Check the connection and local HTTPS certificate.'
  }
  return error instanceof Error ? error.message : 'An unexpected error occurred.'
}
