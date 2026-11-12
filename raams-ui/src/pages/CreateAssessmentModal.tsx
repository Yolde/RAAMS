import {
  Button, Dialog, DialogActions, DialogBody, DialogContent, DialogSurface, DialogTitle,
  Dropdown, Field, Input, MessageBar, MessageBarBody, Option, Textarea, makeStyles,
} from '@fluentui/react-components'
import { useState } from 'react'
import type { FormEvent } from 'react'
import { getErrorMessage } from '../api/errors'
import { useCreateAssessment } from '../api/queries'

const quarters = ['Q1', 'Q2', 'Q3', 'Q4']
const linesOfDefence = ['LOD1', 'LOD2', 'LOD3']
const guidPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i
const emptyGuid = '00000000-0000-0000-0000-000000000000'

const useStyles = makeStyles({
  fields: { display: 'flex', flexDirection: 'column', gap: '14px' },
  surface: { maxHeight: '90vh', overflowY: 'auto' },
})

interface Props {
  open: boolean
  onClose: () => void
  onCreated: (reference: string) => void
}

export function CreateAssessmentModal({ open, onClose, onCreated }: Props) {
  const styles = useStyles()
  const mutation = useCreateAssessment()
  const [name, setName] = useState('')
  const [scope, setScope] = useState('')
  const [functionName, setFunctionName] = useState('')
  const [quarter, setQuarter] = useState('Q1')
  const [lineOfDefence, setLineOfDefence] = useState('LOD1')
  const [year, setYear] = useState(String(new Date().getFullYear()))
  const [leadAuditorId, setLeadAuditorId] = useState('')
  const [coordinatorId, setCoordinatorId] = useState('')
  const [error, setError] = useState<string | null>(null)

  function close() {
    if (mutation.isPending) return
    setName('')
    setScope('')
    setFunctionName('')
    setQuarter('Q1')
    setLineOfDefence('LOD1')
    setYear(String(new Date().getFullYear()))
    setLeadAuditorId('')
    setCoordinatorId('')
    setError(null)
    mutation.reset()
    onClose()
  }

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (mutation.isPending) return
    setError(null)
    const lead = leadAuditorId.trim()
    const coordinator = coordinatorId.trim()
    const validGuid = (id: string) => guidPattern.test(id) && id.toLowerCase() !== emptyGuid
    if (!name.trim() || !scope.trim() || !functionName.trim()) {
      setError('Name, scope and function are required.')
      return
    }
    if (!validGuid(lead) || !validGuid(coordinator)) {
      setError('Lead auditor and coordinator IDs must be valid, non-empty GUIDs.')
      return
    }
    const numericYear = Number(year)
    if (!Number.isInteger(numericYear) || numericYear < 2000 || numericYear > 2100) {
      setError('Year must be between 2000 and 2100.')
      return
    }
    try {
      const created = await mutation.mutateAsync({
        name: name.trim(), scope: scope.trim(), function: functionName.trim(), quarter, year: numericYear, lineOfDefence,
        leadAuditorId: lead, coordinatorId: coordinator,
      })
      onCreated(created.referenceNumber)
      setName('')
      setScope('')
      setFunctionName('')
      setQuarter('Q1')
      setLineOfDefence('LOD1')
      setYear(String(new Date().getFullYear()))
      setLeadAuditorId('')
      setCoordinatorId('')
      setError(null)
      mutation.reset()
      onClose()
    } catch (reason) {
      setError(getErrorMessage(reason))
    }
  }

  return (
    <Dialog open={open} onOpenChange={(_, data) => { if (!data.open) close() }}>
      <DialogSurface className={styles.surface}>
        <form onSubmit={submit}>
          <DialogBody>
            <DialogTitle>Create Assessment</DialogTitle>
            <DialogContent className={styles.fields}>
              {error && <MessageBar intent="error"><MessageBarBody>{error}</MessageBarBody></MessageBar>}
              <Field label="Name" required>
                <Input required maxLength={200} value={name} onChange={(_, data) => setName(data.value)} />
              </Field>
              <Field label="Scope" required>
                <Textarea required maxLength={2000} value={scope} onChange={(_, data) => setScope(data.value)} />
              </Field>
              <Field label="Function" required>
                <Input required maxLength={100} value={functionName} onChange={(_, data) => setFunctionName(data.value)} />
              </Field>
              <Field label="Quarter" required>
                <Dropdown value={quarter} selectedOptions={[quarter]} onOptionSelect={(_, data) => {
                  if (data.optionValue) setQuarter(data.optionValue)
                }}>
                  {quarters.map((option) => <Option key={option} value={option}>{option}</Option>)}
                </Dropdown>
              </Field>
              <Field label="Line of Defence" required>
                <Dropdown value={lineOfDefence} selectedOptions={[lineOfDefence]} onOptionSelect={(_, data) => {
                  if (data.optionValue) setLineOfDefence(data.optionValue)
                }}>
                  {linesOfDefence.map((option) => <Option key={option} value={option}>{option}</Option>)}
                </Dropdown>
              </Field>
              <Field label="Year" required>
                <Input type="number" min={2000} max={2100} required value={year} onChange={(_, data) => setYear(data.value)} />
              </Field>
              <Field label="Lead Auditor ID" hint="For testing, use Harun's RAAMS user ID." required>
                <Input required value={leadAuditorId} onChange={(_, data) => setLeadAuditorId(data.value)} />
              </Field>
              <Field label="Coordinator ID" hint="For testing, use Harun's RAAMS user ID." required>
                <Input required value={coordinatorId} onChange={(_, data) => setCoordinatorId(data.value)} />
              </Field>
            </DialogContent>
            <DialogActions>
              <Button type="button" onClick={close} disabled={mutation.isPending}>Cancel</Button>
              <Button appearance="primary" type="submit" disabled={mutation.isPending}>
                {mutation.isPending ? 'Creating...' : 'Create Assessment'}
              </Button>
            </DialogActions>
          </DialogBody>
        </form>
      </DialogSurface>
    </Dialog>
  )
}
