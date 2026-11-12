import axios from 'axios'
import { clearToken, getToken } from '../auth/tokenStorage'

export const client = axios.create({
  baseURL: 'https://localhost:7014',
  withCredentials: false,
})

client.interceptors.request.use((config) => {
  const token = getToken()
  if (token) config.headers.set('Authorization', `Bearer ${token}`)
  return config
})

client.interceptors.response.use(
  (response) => response,
  (error: unknown) => {
    if (axios.isAxiosError(error) && error.response?.status === 401) {
      const requestAuthorization = error.config?.headers?.get('Authorization')
      const currentToken = getToken()
      if (currentToken && requestAuthorization === `Bearer ${currentToken}`) {
        clearToken()
        window.location.assign('/login')
      }
    }
    return Promise.reject(error)
  },
)
