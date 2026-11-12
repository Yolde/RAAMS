import {
  Badge, Button, DataGrid, DataGridBody, DataGridCell, DataGridHeader, DataGridHeaderCell,
  DataGridRow, Dialog, DialogActions, DialogBody, DialogContent, DialogSurface, DialogTitle,
  Field, Input, MessageBar, MessageBarBody, Spinner, TableCellLayout, Text, Textarea,
  createTableColumn, makeStyles, tokens,
} from '@fluentui/react-components'
import type { TableColumnDefinition } from '@fluentui/react-components'
import { useMemo, useState } from 'react'
import type { FormEvent } from 'react'
import { getErrorMessage } from '../api/errors'
import { useActions, useApproveAction, useRejectAction, useReopenAction, useSubmitAction } from '../api/queries'
import type { Action } from '../api/types'
import { usePermissions } from '../context/RoleContext'

type Operation = 'submit' | 'approve' | 'reject' | 'reopen'
interface Selection {
  operation: Operation
  action: Action
}

const operationLabels: Record<Operation, string> = {
  submit: 'Submit',
  approve: 'Approve',
  reject: 'Reject',
  reopen: 'Reopen',
}

const useStyles = makeStyles({
  page: { display: 'flex', flexDirection: 'column', gap: '20px' },
  search: { maxWidth: '360px' },
  table: { overflowX: 'auto' },
  grid: { minWidth: '1100px' },
  restricted: { color: tokens.colorNeutralForegroundDisabled },
  buttons: { display: 'flex', gap: '8px', flexWrap: 'wrap' },
  pagination: { display: 'flex', alignItems: 'center', gap: '12px', flexWrap: 'wrap' },
  dialogContent: { display: 'flex', flexDirection: 'column', gap: '12px' },
})

function StatusBadge({ status }: { status: string }) {
  switch (status) {
    case 'Open': return <Badge color="brand">{status}</Badge>
    case 'Submitted': return <Badge color="important">{status}</Badge>
    case 'Approved': return <Badge color="warning">{status}</Badge>
    case 'Closed': return <Badge color="success">{status}</Badge>
    case 'Reopened': return <Badge color="danger">{status}</Badge>
    default: return <Badge appearance="ghost">{status}</Badge>
  }
}

export function ActionsPage() {
  const styles = useStyles()
  const { hasPermission, hasRole } = usePermissions()
  const [page, setPage] = useState(1)
  const [search, setSearch] = useState('')
  const [selection, setSelection] = useState<Selection | null>(null)
  const [message, setMessage] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [success, setSuccess] = useState<string | null>(null)
  const query = useActions({ page, pageSize: 20 })
  const submitMutation = useSubmitAction()
  const approveMutation = useApproveAction()
  const rejectMutation = useRejectAction()
  const reopenMutation = useReopenAction()
  const pending = submitMutation.isPending || approveMutation.isPending ||
    rejectMutation.isPending || reopenMutation.isPending
  const canSubmit = hasPermission('Action.Submit') || hasRole('GRA')
  const canApprove = hasPermission('Action.Approve') || hasRole('GRA')
  const canReopen = hasPermission('Action.Reopen') || hasRole('GRA')

  const items = useMemo(() => {
    const term = search.trim().toLocaleLowerCase()
    return (query.data?.items ?? []).filter((item) =>
      item.referenceNumber.toLocaleLowerCase().includes(term) ||
      item.actionDescription.toLocaleLowerCase().includes(term))
  }, [query.data, search])

  function open(operation: Operation, action: Action) {
    setSelection({ operation, action })
    setMessage('')
    setError(null)
    setSuccess(null)
  }

  function close() {
    if (pending) return
    setSelection(null)
    setMessage('')
    setError(null)
  }

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!selection || pending) return
    setError(null)
    const { operation, action } = selection
    if (operation === 'submit' && !message.trim()) {
      setError('Action Taken is required.')
      return
    }
    try {
      if (operation === 'submit') {
        await submitMutation.mutateAsync({ id: action.id, actionTaken: message.trim() })
      } else {
        const dto = { id: action.id, comments: message.trim() || null }
        if (operation === 'approve') await approveMutation.mutateAsync(dto)
        if (operation === 'reject') await rejectMutation.mutateAsync(dto)
        if (operation === 'reopen') await reopenMutation.mutateAsync(dto)
      }
      setSuccess(`${operationLabels[operation]} completed for ${action.referenceNumber}.`)
      setSelection(null)
      setMessage('')
    } catch (reason) {
      setError(getErrorMessage(reason))
    }
  }

  const columns: TableColumnDefinition<Action>[] = [
    createTableColumn({ columnId: 'reference', renderHeaderCell: () => 'Reference',
      renderCell: (item) => <TableCellLayout>{item.referenceNumber}</TableCellLayout> }),
    createTableColumn({ columnId: 'description', renderHeaderCell: () => 'Description',
      renderCell: (item) => <TableCellLayout>{item.actionDescription}</TableCellLayout> }),
    createTableColumn({ columnId: 'actionParty', renderHeaderCell: () => 'Action Party',
      renderCell: (item) => <TableCellLayout>{item.actionPartyName}</TableCellLayout> }),
    createTableColumn({ columnId: 'auditee', renderHeaderCell: () => 'Auditee',
      renderCell: (item) => <TableCellLayout>{item.auditeeName}</TableCellLayout> }),
    createTableColumn({ columnId: 'targetDate', renderHeaderCell: () => 'Target Date',
      renderCell: (item) => <TableCellLayout>{item.targetDate ?? <Text className={styles.restricted}>(restricted)</Text>}</TableCellLayout> }),
    createTableColumn({ columnId: 'status', renderHeaderCell: () => 'Status',
      renderCell: (item) => <TableCellLayout>{item.status === null
        ? <Text className={styles.restricted}>(restricted)</Text>
        : <StatusBadge status={item.status} />}</TableCellLayout> }),
    createTableColumn({ columnId: 'actions', renderHeaderCell: () => 'Actions',
      renderCell: (item) => (
        <div className={styles.buttons}>
          {(item.status === 'Open' || item.status === 'Reopened') && canSubmit &&
            <Button size="small" onClick={() => open('submit', item)}>Submit</Button>}
          {item.status === 'Submitted' && canApprove && (
            <>
              <Button size="small" onClick={() => open('approve', item)}>Approve</Button>
              <Button size="small" onClick={() => open('reject', item)}>Reject</Button>
            </>
          )}
          {item.status === 'Closed' && canReopen &&
            <Button size="small" onClick={() => open('reopen', item)}>Reopen</Button>}
        </div>
      ) }),
  ]

  return (
    <section className={styles.page}>
      <Text as="h1" size={700} weight="semibold">Actions</Text>
      {success && <MessageBar intent="success"><MessageBarBody>{success}</MessageBarBody></MessageBar>}
      <Input className={styles.search} aria-label="Search current page by reference or description"
        placeholder="Search reference or description" value={search} onChange={(_, data) => setSearch(data.value)} />
      <Text size={200}>Search filters the current page.</Text>
      {query.isPending && <Spinner label="Loading actions..." />}
      {query.error && <MessageBar intent="error"><MessageBarBody>{getErrorMessage(query.error)}</MessageBarBody></MessageBar>}
      {query.data && (
        <>
          {query.data.totalCount === 0 && <Text>No actions found.</Text>}
          {query.data.totalCount > 0 && items.length === 0 && <Text>No matching actions on this page.</Text>}
          <div className={styles.table}>
            <DataGrid className={styles.grid} items={items} columns={columns} getRowId={(item) => item.id}>
              <DataGridHeader>
                <DataGridRow>{({ renderHeaderCell }) => <DataGridHeaderCell>{renderHeaderCell()}</DataGridHeaderCell>}</DataGridRow>
              </DataGridHeader>
              <DataGridBody<Action>>
                {({ item, rowId }) => (
                  <DataGridRow<Action> key={rowId}>
                    {({ renderCell }) => <DataGridCell>{renderCell(item)}</DataGridCell>}
                  </DataGridRow>
                )}
              </DataGridBody>
            </DataGrid>
          </div>
          <div className={styles.pagination}>
            <Button disabled={page <= 1} onClick={() => setPage((value) => value - 1)}>Previous</Button>
            <Text>Page {query.data.page} of {Math.max(query.data.totalPages, 1)} ({query.data.totalCount} total)</Text>
            <Button disabled={page >= query.data.totalPages} onClick={() => setPage((value) => value + 1)}>Next</Button>
          </div>
        </>
      )}
      <Dialog open={selection !== null} onOpenChange={(_, data) => { if (!data.open) close() }}>
        <DialogSurface>
          {selection && (
            <form onSubmit={submit}>
              <DialogBody>
                <DialogTitle>{operationLabels[selection.operation]} {selection.action.referenceNumber}</DialogTitle>
                <DialogContent className={styles.dialogContent}>
                  {error && <MessageBar intent="error"><MessageBarBody>{error}</MessageBarBody></MessageBar>}
                  <Field label={selection.operation === 'submit' ? 'Action Taken' : selection.operation === 'reopen' ? 'Reason (optional)' : 'Comments (optional)'}
                    required={selection.operation === 'submit'}>
                    <Textarea value={message} onChange={(_, data) => setMessage(data.value)}
                      required={selection.operation === 'submit'} maxLength={selection.operation === 'submit' ? 4000 : 500} />
                  </Field>
                </DialogContent>
                <DialogActions>
                  <Button type="button" onClick={close} disabled={pending}>Cancel</Button>
                  <Button appearance="primary" type="submit" disabled={pending}>
                    {pending ? 'Saving...' : operationLabels[selection.operation]}
                  </Button>
                </DialogActions>
              </DialogBody>
            </form>
          )}
        </DialogSurface>
      </Dialog>
    </section>
  )
}
