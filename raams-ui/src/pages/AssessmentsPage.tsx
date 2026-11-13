import {
  Badge, Button, DataGrid, DataGridBody, DataGridCell, DataGridHeader, DataGridHeaderCell,
  DataGridRow, Input, MessageBar, MessageBarBody, Spinner, TableCellLayout, Text,
  createTableColumn, makeStyles,
} from '@fluentui/react-components'
import type { TableColumnDefinition } from '@fluentui/react-components'
import { useMemo, useState } from 'react'
import { useNavigate } from 'react-router-dom' // <-- NEW IMPORT
import { getErrorMessage } from '../api/errors'
import { useAssessments } from '../api/queries'
import type { Assessment } from '../api/types'
import { usePermissions } from '../context/RoleContext'
import { CreateAssessmentModal } from './CreateAssessmentModal'

const useStyles = makeStyles({
  page: { display: 'flex', flexDirection: 'column', gap: '20px' },
  header: { display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: '16px', flexWrap: 'wrap' },
  search: { maxWidth: '360px' },
  table: { overflowX: 'auto' },
  grid: { minWidth: '800px' },
  pagination: { display: 'flex', alignItems: 'center', gap: '12px', flexWrap: 'wrap' },
  clickableRow: { cursor: 'pointer', ':hover': { backgroundColor: '#f5f5f5' } }, // <-- NEW STYLE
})

function StatusBadge({ status }: { status: string }) {
  switch (status) {
    case 'Planned': return <Badge color="brand">{status}</Badge>
    case 'Started': return <Badge color="important">{status}</Badge>
    case 'DraftReport': return <Badge color="warning">{status}</Badge>
    case 'Completed': return <Badge color="success">{status}</Badge>
    default: return <Badge appearance="ghost">{status}</Badge>
  }
}

const columns: TableColumnDefinition<Assessment>[] = [
  createTableColumn({ columnId: 'reference', renderHeaderCell: () => 'Reference',
    renderCell: (item) => <TableCellLayout>{item.referenceNumber}</TableCellLayout> }),
  createTableColumn({ columnId: 'name', renderHeaderCell: () => 'Name',
    renderCell: (item) => <TableCellLayout>{item.name}</TableCellLayout> }),
  createTableColumn({ columnId: 'function', renderHeaderCell: () => 'Function',
    renderCell: (item) => <TableCellLayout>{item.function}</TableCellLayout> }),
  createTableColumn({ columnId: 'quarter', renderHeaderCell: () => 'Quarter/Year',
    renderCell: (item) => <TableCellLayout>{item.quarter} {item.year}</TableCellLayout> }),
  createTableColumn({ columnId: 'leadAuditor', renderHeaderCell: () => 'Lead Auditor',
    renderCell: (item) => <TableCellLayout>{item.leadAuditorName}</TableCellLayout> }),
  createTableColumn({ columnId: 'status', renderHeaderCell: () => 'Status',
    renderCell: (item) => <TableCellLayout><StatusBadge status={item.status} /></TableCellLayout> }),
]

export function AssessmentsPage() {
  const styles = useStyles()
  const navigate = useNavigate() // <-- NEW NAVIGATE HOOK
  const { hasPermission, hasRole } = usePermissions()
  const [page, setPage] = useState(1)
  const [search, setSearch] = useState('')
  const [modalOpen, setModalOpen] = useState(false)
  const [success, setSuccess] = useState<string | null>(null)
  const query = useAssessments({ page, pageSize: 20 })
  
  const items = useMemo(() => {
    const term = search.trim().toLocaleLowerCase()
    return (query.data?.items ?? []).filter((item) =>
      item.name.toLocaleLowerCase().includes(term) || item.referenceNumber.toLocaleLowerCase().includes(term))
  }, [query.data, search])

  return (
    <section className={styles.page}>
      <div className={styles.header}>
        <Text as="h1" size={700} weight="semibold">Assessments</Text>
        {(hasPermission('Assessment.Create') || hasRole('GRA')) &&
          <Button appearance="primary" onClick={() => { setSuccess(null); setModalOpen(true) }}>Create Assessment</Button>}
      </div>
      {success && <MessageBar intent="success"><MessageBarBody>Assessment {success} created.</MessageBarBody></MessageBar>}
      <Input className={styles.search} aria-label="Search current page by name or reference"
        placeholder="Search name or reference" value={search} onChange={(_, data) => setSearch(data.value)} />
      <Text size={200}>Search filters the current page.</Text>
      {query.isPending && <Spinner label="Loading assessments..." />}
      {query.error && <MessageBar intent="error"><MessageBarBody>{getErrorMessage(query.error)}</MessageBarBody></MessageBar>}
      {query.data && (
        <>
          {items.length === 0 && <Text>No assessments found on this page.</Text>}
          <div className={styles.table}>
            <DataGrid className={styles.grid} items={items} columns={columns} getRowId={(item) => item.id}>
              <DataGridHeader>
                <DataGridRow>{({ renderHeaderCell }) => <DataGridHeaderCell>{renderHeaderCell()}</DataGridHeaderCell>}</DataGridRow>
              </DataGridHeader>
              <DataGridBody<Assessment>>
                {({ item, rowId }) => (
                  <DataGridRow<Assessment> 
                    key={rowId} 
                    className={styles.clickableRow} 
                    onClick={() => navigate(`/assessments/${item.id}`)} // <-- CLICK EVENT
                  >
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
      <CreateAssessmentModal open={modalOpen} onClose={() => setModalOpen(false)} onCreated={setSuccess} />
    </section>
  )
}