import {
  Button, Dialog, DialogActions, DialogBody, DialogContent, DialogSurface,
  DialogTitle, Field, Input, Spinner, MessageBar, MessageBarBody
} from '@fluentui/react-components'
import { useState } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { client } from '../api/client'

interface Props {
  assessmentId: string
  open: boolean
  onClose: () => void
}

export function UploadEvidenceModal({ assessmentId, open, onClose }: Props) {
  const queryClient = useQueryClient()
  const [file, setFile] = useState<File | null>(null)
  const [notes, setNotes] = useState('')
  const [error, setError] = useState<string | null>(null)

  const mutation = useMutation({
    mutationFn: async () => {
      if (!file) throw new Error('File is required')

      const formData = new FormData()
      formData.append('File', file)       // matches IFormFile File
      formData.append('Notes', notes)     // matches string? Notes

      const response = await client.post(
        `/api/assessments/${assessmentId}/escalation-email`,
        formData,
        { headers: { 'Content-Type': 'multipart/form-data' } }
      )
      return response.data
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['evidence', assessmentId] })
      setFile(null)
      setNotes('')
      setError(null)
      onClose()
    },
    onError: (err: any) => {
      setError(err.response?.data?.error || err.response?.data?.message || err.message)
    },
  })

  return (
    <Dialog open={open} onOpenChange={(_, data) => !data.open && onClose()}>
      <DialogSurface>
        <DialogBody>
          <DialogTitle>Upload Escalation Evidence</DialogTitle>
          <DialogContent style={{ display: 'flex', flexDirection: 'column', gap: '16px' }}>
            {error && (
              <MessageBar intent="error">
                <MessageBarBody>{error}</MessageBarBody>
              </MessageBar>
            )}

            <Field label="File (max 20 MB)" required>
              <Input type="file" onChange={(e) => setFile(e.target.files?.[0] || null)} />
            </Field>

            <Field label="Notes (Optional)">
              <Input value={notes} onChange={(e) => setNotes(e.target.value)} />
            </Field>
          </DialogContent>
          <DialogActions>
            <Button appearance="secondary" onClick={onClose}>Cancel</Button>
            <Button
              appearance="primary"
              onClick={() => mutation.mutate()}
              disabled={!file || mutation.isPending}
            >
              {mutation.isPending ? <Spinner size="tiny" /> : 'Upload'}
            </Button>
          </DialogActions>
        </DialogBody>
      </DialogSurface>
    </Dialog>
  )
}