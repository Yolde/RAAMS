import { Card, MessageBar, MessageBarBody, Spinner, Text, makeStyles, tokens } from '@fluentui/react-components'
import { ArcElement, BarElement, CategoryScale, Chart as ChartJS, Legend, LinearScale, Tooltip } from 'chart.js'
import { Bar, Doughnut } from 'react-chartjs-2'
import { getErrorMessage } from '../api/errors'
import { useActions, useAssessments } from '../api/queries'
import { useAuth } from '../auth/AuthProvider'

ChartJS.register(ArcElement, Tooltip, Legend, CategoryScale, LinearScale, BarElement)

const assessmentStatuses = ['Planned', 'Started', 'DraftReport', 'Completed', 'Deferred', 'Cancelled']
const actionStatuses = ['Open', 'Submitted', 'Approved', 'Closed', 'Reopened']
const assessmentColors = ['#0078d4', '#8764b8', '#ffb900', '#107c10', '#8a8886', '#d13438']
const actionColors = ['#0078d4', '#8764b8', '#ffb900', '#107c10', '#d13438']

const useStyles = makeStyles({
  page: { display: 'flex', flexDirection: 'column', gap: '24px' },
  tiles: { display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(190px, 1fr))', gap: '16px' },
  tile: { padding: '20px', gap: '8px' },
  charts: { display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(min(100%, 340px), 1fr))', gap: '16px' },
  chartCard: { padding: '20px', gap: '16px', minWidth: 0 },
  chart: { position: 'relative', height: '300px', width: '100%' },
  caption: { color: tokens.colorNeutralForeground3 },
})

export function DashboardPage() {
  const styles = useStyles()
  const { user } = useAuth()
  const assessments = useAssessments()
  const actions = useActions()
  const assessmentItems = assessments.data?.items
  const actionItems = actions.data?.items
  const now = new Date()
  const overdue = actionItems?.filter((action) =>
    action.targetDate && new Date(action.targetDate) < now && action.status !== 'Closed').length
  const open = actionItems?.filter((action) => action.status === 'Open' || action.status === 'Reopened').length
  const assessmentCounts = assessmentStatuses.map((status) =>
    assessmentItems?.filter((assessment) => assessment.status === status).length ?? 0)
  const actionCounts = actionStatuses.map((status) =>
    actionItems?.filter((action) => action.status === status).length ?? 0)
  const maskedFields = actionItems?.some((action) => action.targetDate === null || action.status === null)

  return (
    <section className={styles.page}>
      <div>
        <Text as="h1" size={700} weight="semibold">Executive Dashboard</Text>
        <Text>Welcome back, {user?.name}</Text>
      </div>
      {(assessments.isPending || actions.isPending) && <Spinner label="Loading dashboard..." />}
      {assessments.error && (
        <MessageBar intent="error"><MessageBarBody>Assessments: {getErrorMessage(assessments.error)}</MessageBarBody></MessageBar>
      )}
      {actions.error && (
        <MessageBar intent="error"><MessageBarBody>Actions: {getErrorMessage(actions.error)}</MessageBarBody></MessageBar>
      )}
      <div className={styles.tiles}>
        <Card className={styles.tile}>
          <Text>Total Assessments</Text>
          <Text size={700} weight="semibold">{assessments.data?.totalCount ?? '—'}</Text>
        </Card>
        <Card className={styles.tile}>
          <Text>Total Actions</Text>
          <Text size={700} weight="semibold">{actions.data?.totalCount ?? '—'}</Text>
        </Card>
        <Card className={styles.tile}>
          <Text>Overdue Actions</Text>
          <Text size={700} weight="semibold">{overdue ?? '—'}</Text>
          <Text size={200} className={styles.caption}>First page only</Text>
        </Card>
        <Card className={styles.tile}>
          <Text>Open Actions</Text>
          <Text size={700} weight="semibold">{open ?? '—'}</Text>
          <Text size={200} className={styles.caption}>First page only</Text>
        </Card>
      </div>
      <Text size={200} className={styles.caption}>
        Charts and action counts use only the first page of visible records; totals include all visible records.
        {maskedFields && ' Restricted dates or statuses can affect action counts and charts.'}
      </Text>
      <div className={styles.charts}>
        <Card className={styles.chartCard}>
          <Text as="h2" size={500} weight="semibold">Assessments by Status</Text>
          {assessmentItems && (assessmentCounts.some((count) => count > 0) ? (
            <div className={styles.chart}>
              <Doughnut
                data={{ labels: assessmentStatuses, datasets: [{ data: assessmentCounts, backgroundColor: assessmentColors }] }}
                options={{ responsive: true, maintainAspectRatio: false }}
                aria-label="Assessments by status on the first page"
                role="img"
              />
            </div>
          ) : <Text>No data available yet.</Text>)}
        </Card>
        <Card className={styles.chartCard}>
          <Text as="h2" size={500} weight="semibold">Actions by Status</Text>
          {actionItems && (actionCounts.some((count) => count > 0) ? (
            <div className={styles.chart}>
              <Bar
                data={{ labels: actionStatuses, datasets: [{ label: 'Actions', data: actionCounts, backgroundColor: actionColors }] }}
                options={{ responsive: true, maintainAspectRatio: false, scales: { y: { beginAtZero: true, ticks: { precision: 0 } } } }}
                aria-label="Actions by status on the first page"
                role="img"
              />
            </div>
          ) : <Text>No data available yet.{actionItems.some((action) => action.status === null) && ' Status information may be restricted.'}</Text>)}
        </Card>
      </div>
    </section>
  )
}
