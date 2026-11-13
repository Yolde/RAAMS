import {
  Badge, Button, DataGrid, DataGridBody, DataGridCell, DataGridHeader, DataGridHeaderCell,
  DataGridRow, Input, Spinner, TableCellLayout, Text, createTableColumn, makeStyles
} from '@fluentui/react-components'
import type { TableColumnDefinition } from '@fluentui/react-components'
import { useMemo, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { client } from '../api/client'

const useStyles = makeStyles({
  page: { display: 'flex', flexDirection: 'column', gap: '20px' },
  header: { display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: '16px', flexWrap: 'wrap' },
  search: { maxWidth: '360px' },
  table: { overflowX: 'auto' },
  grid: { minWidth: '1000px' },
  pagination: { display: 'flex', alignItems: 'center', gap: '12px', flexWrap: 'wrap' },
})

const columns: TableColumnDefinition<any>[] = [
  createTableColumn({ columnId: 'fileName', renderHeaderCell: () => 'File Name',
    renderCell: (item) => <TableCellLayout>{item.fileName}</TableCellLayout> }),
  createTableColumn({ columnId: 'assessment', renderHeaderCell: () => 'Assessment',
    renderCell: (item) => <TableCellLayout>{item.assessmentReference}</TableCellLayout> }),
  createTableColumn({ columnId: 'type', renderHeaderCell: () => 'Type',
    renderCell: (item) => <TableCellLayout>{item.contentType}</TableCellLayout> }),
  createTableColumn({ columnId: 'size', renderHeaderCell: () => 'Size',
    renderCell: (item) => <TableCellLayout>{(item.sizeBytes / 1024).toFixed(1)} KB</TableCellLayout> }),
  createTableColumn({ columnId: 'uploadedBy', renderHeaderCell: () => 'Uploaded By',
    renderCell: (item) => <TableCellLayout>{item.uploadedByName}</TableCellLayout> }),
  createTableColumn({ columnId: 'date', renderHeaderCell: () => 'Date',
    renderCell: (item) => <TableCellLayout>{new Date(item.uploadedAt).toLocaleDateString()}</TableCellLayout> }),
  createTableColumn({ columnId: 'actions', renderHeaderCell: () => 'Actions',
    renderCell: (item) => (
      <Button size="small" onClick={async () => {
        try {
          const response = await client.get(
            `/api/evidence/${item.id}/download`,
            { responseType: 'blob' }
          );
          const url = window.URL.createObjectURL(new Blob([response.data]));
          const link = document.createElement('a');
          link.href = url;
          link.setAttribute('download', item.fileName);
          document.body.appendChild(link);
          link.click();
          link.remove();
          window.URL.revokeObjectURL(url);
        } catch (error: any) {
          alert(`Download failed: ${error.response?.status} ${error.response?.statusText}`);
        }
      }}>
        Download
      </Button>
    ) 
  }),
]

export function EvidencePage() {
  const styles = useStyles()
  const [page, setPage] = useState(1)
  const [search, setSearch] = useState('')

  // Fetch all evidence globally
  const query = useQuery({
    queryKey: ['evidence-global', page],
    queryFn: async () => {
      const res = await client.get(`/api/evidence?page=${page}&pageSize=20`)
      return res.data
    }
  })

  const items = useMemo(() => {
    const term = search.trim().toLocaleLowerCase()
    return (query.data?.items ?? []).filter((item: any) =>
      item.fileName.toLocaleLowerCase().includes(term) ||
      (item.assessmentReference && item.assessmentReference.toLocaleLowerCase().includes(term))
    )
  }, [query.data, search])

  return (
    <section className={styles.page}>
      <div className={styles.header}>
        <Text as="h1" size={700} weight="semibold">Global Evidence</Text>
      </div>
      
      <Input className={styles.search} aria-label="Search evidence" 
        placeholder="Search by file name or assessment reference" 
        value={search} onChange={(_, data) => setSearch(data.value)} 
      />

      {query.isPending && <Spinner label="Loading evidence..." />}
      {query.error && <Text>Error loading evidence.</Text>}
      
      {query.data && (
        <>
          {items.length === 0 && <Text>No evidence found.</Text>}
          <div className={styles.table}>
            <DataGrid className={styles.grid} items={items} columns={columns} getRowId={(item) => item.id}>
              <DataGridHeader>
                <DataGridRow>{({ renderHeaderCell }) => <DataGridHeaderCell>{renderHeaderCell()}</DataGridHeaderCell>}</DataGridRow>
              </DataGridHeader>
              <DataGridBody<any>>
                {({ item, rowId }) => (
                  <DataGridRow<any> key={rowId}>
                    {({ renderCell }) => <DataGridCell>{renderCell(item)}</DataGridCell>}
                  </DataGridRow>
                )}
              </DataGridBody>
            </DataGrid>
          </div>
          <div className={styles.pagination}>
            <Button disabled={page <= 1} onClick={() => setPage((v) => v - 1)}>Previous</Button>
            <Text>Page {query.data.page} of {Math.max(query.data.totalPages, 1)} ({query.data.totalCount} total)</Text>
            <Button disabled={page >= query.data.totalPages} onClick={() => setPage((v) => v + 1)}>Next</Button>
          </div>
        </>
      )}
    </section>
  )
}