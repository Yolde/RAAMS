import { Button, Card, Field, MessageBar, MessageBarBody, Text, Textarea, makeStyles } from '@fluentui/react-components'
import { useState } from 'react'
import type { FormEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import { useAuth } from '../auth/AuthProvider'

const useStyles = makeStyles({
  page: { minHeight: '100vh', display: 'flex', alignItems: 'center', justifyContent: 'center', padding: '24px', backgroundColor: '#f5f7fa' },
  card: { width: '100%', maxWidth: '480px', padding: '32px', gap: '20px' },
  hint: { overflowWrap: 'anywhere' },
})

export function LoginPage() {
  const styles = useStyles()
  const navigate = useNavigate()
  const { login } = useAuth()
  const [token, setToken] = useState('')
  const [error, setError] = useState<string | null>(null)

  const submit = (event: FormEvent) => {
    event.preventDefault()
    try {
      login(token)
      navigate('/dashboard', { replace: true })
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Unable to sign in.')
    }
  }

  return (
    <main className={styles.page}>
      <Card className={styles.card}>
        <Text size={700} weight="semibold">RAAMS</Text>
        <Text>Development sign in</Text>
        <form onSubmit={submit}>
          <Field label="Paste your dev JWT token (from RAAMS.API/token.txt)" required>
            <Textarea value={token} onChange={(_, data) => setToken(data.value)} rows={5} autoComplete="off" />
          </Field>
          {error && <MessageBar intent="error"><MessageBarBody>{error}</MessageBarBody></MessageBar>}
          <Button appearance="primary" type="submit">Sign In</Button>
        </form>
        <Text className={styles.hint} size={200}>
          Generate with: dotnet user-jwts create --name &lt;guid&gt; --role GRA --role SuperAdmin
        </Text>
        <Text size={200}>Add --claim email=you@example.com; the backend requires an email claim.</Text>
      </Card>
    </main>
  )
}
