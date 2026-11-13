import {
  Button, Dialog, DialogActions, DialogBody, DialogContent, DialogSurface,
  DialogTitle, Field, Input, Textarea, Dropdown, Option, Spinner
} from '@fluentui/react-components'
import { useState } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { client } from '../api/client'

interface Props {
  assessmentId: string
  open: boolean
  onClose: () => void
}

export function CreateFindingModal({ assessmentId, open, onClose }: Props) {
  const queryClient = useQueryClient()
  const [description, setDescription] = useState('')
  const [rootCause, setRootCause] = useState('')
  const [recommendation, setRecommendation] = useState('')
  const [severity, setSeverity] = useState('High')

  const mutation = useMutation({
    mutationFn: async () => {
      const response = await client.post(`/api/findings`, {
        assessmentId: assessmentId,
        referenceNumber: `F-${crypto.randomUUID().slice(0, 8).toUpperCase()}`, // <-- ADDED REF NUMBER
        description,
        rootCause,
        recommendation,
        severity
      })
      return response.data
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['assessment', assessmentId] })
      setDescription('')
      setRootCause('')
      setRecommendation('')
      setSeverity('High')
      onClose()
    },
    onError: (error: any) => {
      console.error(error)
      alert(`Failed to save: ${error.response?.data?.message || error.message}`)
    }
  })

  return (
    <Dialog open={open} onOpenChange={(_, data) => !data.open && onClose()}>
      <DialogSurface>
        <DialogBody>
          <DialogTitle>Add Finding</DialogTitle>
          <DialogContent style={{ display: 'flex', flexDirection: 'column', gap: '16px' }}>
            <Field label="Description" required>
              <Textarea value={description} onChange={(e) => setDescription(e.target.value)} />
            </Field>
            <Field label="Root Cause" required>
              <Textarea value={rootCause} onChange={(e) => setRootCause(e.target.value)} />
            </Field>
            <Field label="Recommendation" required>
              <Textarea value={recommendation} onChange={(e) => setRecommendation(e.target.value)} />
            </Field>
            <Field label="Severity" required>
              <Dropdown value={severity} onOptionSelect={(_, data) => setSeverity(data.optionValue as string)}>
                <Option>Low</Option>
                <Option>Medium</Option>
                <Option>High</Option>
                <Option>Critical</Option>
              </Dropdown>
            </Field>
          </DialogContent>
          <DialogActions>
            <Button appearance="secondary" onClick={onClose}>Cancel</Button>
            <Button appearance="primary" onClick={() => mutation.mutate()} disabled={mutation.isPending}>
              {mutation.isPending ? <Spinner size="tiny" /> : 'Save Finding'}
            </Button>
          </DialogActions>
        </DialogBody>
      </DialogSurface>
    </Dialog>
  )
}