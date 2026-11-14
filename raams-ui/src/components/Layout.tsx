import { Avatar, Badge, Button, MessageBar, MessageBarBody, Spinner, Text, makeStyles } from '@fluentui/react-components'
import type { ReactNode } from 'react'
import { NavLink } from 'react-router-dom'
import { useAuth } from '../auth/AuthProvider'
import { usePermissions } from '../context/RoleContext'
import { RaamsLogo } from './RaamsLogo'

const useStyles = makeStyles({
  shell: { minHeight: '100vh', display: 'flex', flexDirection: 'column', backgroundColor: '#F5F5F5' },
  header: { display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: '16px', padding: '16px 24px', backgroundColor: '#02B702', borderBottom: '3px solid #F76B38', color: '#FFFFFF', flexWrap: 'wrap' },
  branding: { display: 'flex', alignItems: 'center', gap: '12px', minWidth: 0 },
  brandText: { display: 'flex', flexDirection: 'column', minWidth: 0, color: '#FFFFFF' },
  tagline: { color: '#FFFFFF', opacity: 0.9 },
  identity: { display: 'flex', alignItems: 'center', gap: '10px', flexWrap: 'wrap' },
  role: { backgroundColor: '#FFD400', color: '#1A1A1A' },
  logout: { color: '#FFFFFF', borderTopColor: '#FFFFFF', borderRightColor: '#FFFFFF', borderBottomColor: '#FFFFFF', borderLeftColor: '#FFFFFF', backgroundColor: 'transparent', ':hover': { color: '#FFFFFF', backgroundColor: '#007A02' }, ':active': { color: '#FFFFFF', backgroundColor: '#006502' } },
  body: { display: 'flex', flex: 1, '@media (max-width: 700px)': { flexDirection: 'column' } },
  sidebar: { width: '220px', padding: '20px', backgroundColor: '#fff', display: 'flex', flexDirection: 'column', gap: '12px', '@media (max-width: 700px)': { width: '100%' } },
  link: { color: '#424242', textDecorationLine: 'none', padding: '8px', ':hover': { color: '#02B702', textDecorationLine: 'underline' } },
  activeLink: { color: '#02B702', fontWeight: '600' },
  main: { flex: 1, padding: '32px', minWidth: 0 },
  status: { minHeight: '100vh', display: 'flex', justifyContent: 'center', alignItems: 'center', gap: '16px', flexDirection: 'column', padding: '24px' },
})

export function Layout({ children }: { children: ReactNode }) {
  const styles = useStyles()
  const { logout } = useAuth()
  const { me, roles, hasRole, hasPermission, isLoading, error, retry } = usePermissions()
  const admin = hasRole('GRA') || hasRole('SuperAdmin')
  
  if (isLoading) return <div className={styles.status}><Spinner label="Loading your permissions..." /></div>
  if (error || !me) {
    return (
      <div className={styles.status}>
        <MessageBar intent="error">
          <MessageBarBody>Could not load your profile. Check the API connection and accept the local HTTPS certificate.</MessageBarBody>
        </MessageBar>
        <Button onClick={retry}>Retry</Button>
        <Button onClick={logout}>Sign out</Button>
      </div>
    )
  }
  
  const links = [
    { to: '/dashboard', label: 'Dashboard', visible: true },
    { to: '/assessments', label: 'Assessments', visible: admin || hasPermission('Assessment.View') },
    { to: '/actions', label: 'Actions', visible: admin || hasPermission('Action.View') },
    { to: '/reviews', label: 'Reviews', visible: admin || hasPermission('Review.Create') },
    { to: '/evidence', label: 'Evidence', visible: admin || hasPermission('Evidence.View') },
    { to: '/admin/permissions', label: 'Admin Permissions', visible: admin }, // <-- ADDED HERE
  ]
  
  return (
    <div className={styles.shell}>
      <header className={styles.header}>
        <div className={styles.branding}>
          <RaamsLogo size={40} />
          <div className={styles.brandText}>
            <Text size={600} weight="bold">RAAMS</Text>
            <Text className={styles.tagline} size={200}>Renaissance Assurance &amp; Action Management System</Text>
          </div>
        </div>
        <div className={styles.identity}>
          <Avatar name={me.displayName} />
          <Text>{me.displayName}</Text>
          {roles.map((role) => <Badge className={styles.role} key={role}>{role}</Badge>)}
          <Button className={styles.logout} onClick={logout}>Log out</Button>
        </div>
      </header>
      <div className={styles.body}>
        <nav className={styles.sidebar} aria-label="Main navigation">
          {links.filter((link) => link.visible).map((link) => (
            <NavLink className={({ isActive }) => isActive ? `${styles.link} ${styles.activeLink}` : styles.link}
              key={link.to} to={link.to}>{link.label}</NavLink>
          ))}
        </nav>
        <main className={styles.main}>{children}</main>
      </div>
    </div>
  )
}