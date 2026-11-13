import {
  Button, Dialog, DialogActions, DialogBody, DialogContent, DialogSurface,
  DialogTitle, Field, Input, Textarea, Dropdown, Option, Spinner
} from '@fluentui/react-components'
import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { client } from '../api/client'

interface Props {
  findingId: string
  assessmentId: string
  open: boolean
  onClose: () => void
}

export function AddActionModal({ findingId, assessmentId, open, onClose }: Props) {
  const queryClient = useQueryClient()
  
  // State for form fields
  const [actionDescription, setActionDescription] = useState('')
  const [actionRequired, setActionRequired] = useState('')
  const [actionPartyId, setActionPartyId] = useState('')
  const [auditeeId, setAuditeeId] = useState('')
  const [targetDate, setTargetDate] = useState('')
  const [reportable, setReportable] = useState('NonCEO')

  // Fetch users for dropdowns
  const { data: users = [] } = useQuery({
    queryKey: ['users'],
    queryFn: async () => {
      const res = await client.get('/api/users')
      return res.data
    }
  })

   const mutation = useMutation({
    mutationFn: async () => {
      // Create a safe ISO date string for C# DateOnly
      const formattedDate = new Date(targetDate).toISOString().split('T')[0];

      const response = await client.post(`/api/actions`, {
        findingId,
        referenceNumber: `A-${crypto.randomUUID().slice(0, 8).toUpperCase()}`,
        actionDescription,
        actionRequired,
        actionPartyId,
        auditeeId,
        targetDate: formattedDate, // <-- Using the formatted date here
        reportable
      })
      return response.data
    },
    // ...
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['assessment', assessmentId] })
      onClose()
    },
    onError: (error: any) => {
      alert(`Failed to save: ${error.response?.data?.message || error.message}`)
    }
  })

  return (
    <Dialog open={open} onOpenChange={(_, data) => !data.open && onClose()}>
      <DialogSurface>
        <DialogBody>
          <DialogTitle>Add Action</DialogTitle>
          <DialogContent style={{ display: 'flex', flexDirection: 'column', gap: '16px' }}>
            
            <Field label="Action Description" required>
              <Textarea value={actionDescription} onChange={(e) => setActionDescription(e.target.value)} />
            </Field>

            <Field label="Action Required" required>
              <Textarea value={actionRequired} onChange={(e) => setActionRequired(e.target.value)} />
            </Field>
            
            <Field label="Action Party" required>
              <Dropdown onOptionSelect={(_, d) => setActionPartyId(d.optionValue as string)}>
                {users.map((u: any) => <Option key={u.id} value={u.id}>{u.displayName}</Option>)}
              </Dropdown>
            </Field>

            <Field label="Auditee" required>
              <Dropdown onOptionSelect={(_, d) => setAuditeeId(d.optionValue as string)}>
                {users.map((u: any) => <Option key={u.id} value={u.id}>{u.displayName}</Option>)}
              </Dropdown>
            </Field>

            <Field label="Target Date" required>
              <Input type="date" value={targetDate} onChange={(e) => setTargetDate(e.target.value)} />
            </Field>

            <Field label="Reportable" required>
              <Dropdown value={reportable} onOptionSelect={(_, d) => setReportable(d.optionValue as string)}>
                <Option value="NonCEO">NonCEO</Option>
                <Option value="CEO">CEO</Option>
              </Dropdown>
            </Field>

          </DialogContent>
          <DialogActions>
            <Button appearance="secondary" onClick={onClose}>Cancel</Button>
            <Button appearance="primary" onClick={() => mutation.mutate()} disabled={mutation.isPending}>
              {mutation.isPending ? <Spinner size="tiny" /> : 'Save Action'}
            </Button>
          </DialogActions>
        </DialogBody>
      </DialogSurface>
    </Dialog>
  )
}