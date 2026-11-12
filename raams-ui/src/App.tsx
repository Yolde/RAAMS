import { FluentProvider, webLightTheme } from '@fluentui/react-components'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import type { ReactNode } from 'react'
import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import { AuthProvider, useAuth } from './auth/AuthProvider'
import { Layout } from './components/Layout'
import { RoleProvider } from './context/RoleContext'
import { LoginPage } from './pages/LoginPage'

const queryClient = new QueryClient()

function ProtectedRoute({ children }: { children: ReactNode }) {
  const { isAuthenticated } = useAuth()
  return isAuthenticated ? <Layout>{children}</Layout> : <Navigate to="/login" replace />
}

function Home() {
  const { isAuthenticated } = useAuth()
  return <Navigate to={isAuthenticated ? '/dashboard' : '/login'} replace />
}

function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <FluentProvider theme={webLightTheme}>
        <AuthProvider>
          <BrowserRouter>
            <RoleProvider>
              <Routes>
                <Route path="/" element={<Home />} />
                <Route path="/login" element={<LoginPage />} />
                <Route path="/dashboard" element={<ProtectedRoute><div>Dashboard coming soon</div></ProtectedRoute>} />
                <Route path="/assessments" element={<ProtectedRoute><div>Assessments coming in 8.3</div></ProtectedRoute>} />
                <Route path="/actions" element={<ProtectedRoute><div>Actions coming in 8.4</div></ProtectedRoute>} />
                <Route path="/reviews" element={<ProtectedRoute><div>Reviews coming soon</div></ProtectedRoute>} />
                <Route path="/evidence" element={<ProtectedRoute><div>Evidence coming soon</div></ProtectedRoute>} />
                <Route path="*" element={<Home />} />
              </Routes>
            </RoleProvider>
          </BrowserRouter>
        </AuthProvider>
      </FluentProvider>
    </QueryClientProvider>
  )
}

export default App
