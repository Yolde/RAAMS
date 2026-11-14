import { AdminPermissionsPage } from './pages/AdminPermissionsPage'
import { EvidencePage } from './pages/EvidencePage'
import { FluentProvider, webLightTheme } from '@fluentui/react-components'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import type { ReactNode } from 'react'
import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import { AuthProvider, useAuth } from './auth/AuthProvider'
import { Layout } from './components/Layout'
import { RoleProvider } from './context/RoleContext'
import { LoginPage } from './pages/LoginPage'
import { AssessmentsPage } from './pages/AssessmentsPage'
import { AssessmentDetailPage } from './pages/AssessmentDetailPage' // <-- NEW IMPORT
import { ActionsPage } from './pages/ActionsPage'
import { DashboardPage } from './pages/DashboardPage'

const queryClient = new QueryClient()
const customTheme = {
  ...webLightTheme,
  colorBrandBackground: '#02B702',
  colorBrandBackgroundHover: '#029602',
  colorBrandForeground1: '#02B702',
  colorBrandForeground2: '#F76B38',
  colorPaletteBrandForeground1: '#02B702',
  colorNeutralBackground1: '#FFFFFF',
  colorNeutralBackground2: '#F5F5F5',
}

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
      <FluentProvider theme={customTheme}>
        <AuthProvider>
          <BrowserRouter>
            <RoleProvider>
              <Routes>
                <Route path="/" element={<Home />} />
                <Route path="/login" element={<LoginPage />} />
                <Route path="/dashboard" element={<ProtectedRoute><DashboardPage /></ProtectedRoute>} />
                <Route path="/admin/permissions" element={<ProtectedRoute><AdminPermissionsPage /></ProtectedRoute>} />
                {/* Assessment Routes */}
                <Route path="/assessments" element={<ProtectedRoute><AssessmentsPage /></ProtectedRoute>} />
                <Route path="/assessments/:id" element={<ProtectedRoute><AssessmentDetailPage /></ProtectedRoute>} /> {/* <-- NEW ROUTE */}
                
                <Route path="/actions" element={<ProtectedRoute><ActionsPage /></ProtectedRoute>} />
                <Route path="/reviews" element={<ProtectedRoute><div>Reviews coming soon</div></ProtectedRoute>} />
                <Route path="/evidence" element={<ProtectedRoute><EvidencePage /></ProtectedRoute>} />
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