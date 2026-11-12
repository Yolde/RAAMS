import { Avatar, Badge, Button, MessageBar, MessageBarBody, Spinner, Text, makeStyles } from '@fluentui/react-components'
import type { ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { useAuth } from '../auth/AuthProvider'
import { usePermissions } from '../context/RoleContext'

const useStyles = makeStyles({
  shell: { minHeight: '100vh', display: 'flex', flexDirection: 'column', backgroundColor: '#f5f7fa' },
  header: { display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: '16px', padding: '16px 24px', backgroundColor: '#fff', borderBottom: '1px solid #e1e5eb', flexWrap: 'wrap' },
  identity: { display: 'flex', alignItems: 'center', gap: '10px', flexWrap: 'wrap' },
  body: { display: 'flex', flex: 1, '@media (max-width: 700px)': { flexDirection: 'column' } },
  sidebar: { width: '220px', padding: '20px', backgroundColor: '#fff', display: 'flex', flexDirection: 'column', gap: '12px', '@media (max-width: 700px)': { width: '100%' } },
  link: { color: '#155a9c', textDecorationLine: 'none', padding: '8px', ':hover': { textDecorationLine: 'underline' } },
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
  ]
  return (
    <div className={styles.shell}>
      <header className={styles.header}>
        <Text size={600} weight="bold">RAAMS</Text>
        <div className={styles.identity}>
          <Avatar name={me.displayName} />
          <Text>{me.displayName}</Text>
          {roles.map((role) => <Badge key={role} appearance="tint">{role}</Badge>)}
          <Button onClick={logout}>Log out</Button>
        </div>
      </header>
      <div className={styles.body}>
        <nav className={styles.sidebar} aria-label="Main navigation">
          {links.filter((link) => link.visible).map((link) => (
            <Link className={styles.link} key={link.to} to={link.to}>{link.label}</Link>
          ))}
        </nav>
        <main className={styles.main}>{children}</main>
      </div>
    </div>
  )
}
