import { useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { 
  Button, 
  Spinner, 
  Text, 
  Title1, 
  Title3, 
  Card, 
  CardHeader, 
  Badge, 
  Table, 
  TableHeader, 
  TableRow, 
  TableHeaderCell, 
  TableBody, 
  TableCell 
} from '@fluentui/react-components';
import { ArrowLeft24Regular } from '@fluentui/react-icons';
import { client } from '../api/client';
import { CreateFindingModal } from './CreateFindingModal';
import { AddActionModal } from './AddActionModal';
import { UploadEvidenceModal } from './UploadEvidenceModal';

export function AssessmentDetailPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [findingModalOpen, setFindingModalOpen] = useState(false);
  const [actionFindingId, setActionFindingId] = useState<string | null>(null);
  const [uploadModalOpen, setUploadModalOpen] = useState(false);

  const { data: assessment, isLoading, isError } = useQuery({
    queryKey: ['assessment', id],
    queryFn: async () => {
      const response = await client.get(`/api/assessments/${id}`);
      return response.data;
    },
    enabled: !!id,
  });

  const { data: evidenceList = [] } = useQuery({
    queryKey: ['evidence', id],
    queryFn: async () => {
      const res = await client.get(`/api/assessments/${id}/escalation-emails`);
      return res.data;
    },
    enabled: !!id,
  });

  if (isLoading) return <Spinner label="Loading assessment..." />;
  if (isError || !assessment) return <Text>Error loading assessment.</Text>;

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '24px' }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: '16px' }}>
        <Button icon={<ArrowLeft24Regular />} appearance="subtle" onClick={() => navigate('/assessments')} />
        <Title1>{assessment.name}</Title1>
        <Badge appearance="filled" color="brand">{assessment.status}</Badge>
      </div>

      <Card>
        <CardHeader header={<Title3>Overview</Title3>} />
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: '16px', padding: '16px' }}>
          <div>
            <Text weight="semibold" block>Reference Number</Text>
            <Text>{assessment.referenceNumber}</Text>
          </div>
          <div>
            <Text weight="semibold" block>Function</Text>
            <Text>{assessment.function}</Text>
          </div>
          <div>
            <Text weight="semibold" block>Quarter / Year</Text>
            <Text>{assessment.quarter} / {assessment.year}</Text>
          </div>
          <div>
            <Text weight="semibold" block>Line of Defence</Text>
            <Text>{assessment.lineOfDefence}</Text>
          </div>
          <div>
            <Text weight="semibold" block>Opinion</Text>
            <Text>{assessment.opinion}</Text>
          </div>
          <div>
            <Text weight="semibold" block>Lead Auditor</Text>
            <Text>{assessment.leadAuditorName}</Text>
          </div>
        </div>
      </Card>

      <Card>
        <CardHeader 
          header={<Title3>Findings</Title3>} 
          action={<Button appearance="primary" onClick={() => setFindingModalOpen(true)}>Add Finding</Button>}
        />
        <Table>
          <TableHeader>
            <TableRow>
              <TableHeaderCell>Ref</TableHeaderCell>
              <TableHeaderCell>Description</TableHeaderCell>
              <TableHeaderCell>Severity</TableHeaderCell>
              <TableHeaderCell>Status</TableHeaderCell>
              <TableHeaderCell>Actions</TableHeaderCell>
            </TableRow>
          </TableHeader>
          <TableBody>
            {assessment.findings?.map((finding: any) => (
              <TableRow key={finding.id}>
                <TableCell>{finding.referenceNumber}</TableCell>
                <TableCell>{finding.description}</TableCell>
                <TableCell>{finding.severity}</TableCell>
                <TableCell>{finding.status}</TableCell>
                <TableCell>
                  <div style={{ display: 'flex', flexDirection: 'column', gap: '4px', alignItems: 'flex-start' }}>
                    {finding.actions?.map((action: any) => (
                      <Text key={action.id} size={200}>
                        {action.referenceNumber} — {action.actionPartyName} ({action.status})
                      </Text>
                    ))}
                    <Button size="small" onClick={() => setActionFindingId(finding.id)}>Add Action</Button>
                  </div>
                </TableCell>
              </TableRow>
            ))}
            
            {(!assessment.findings || assessment.findings.length === 0) && (
              <TableRow>
                <TableCell colSpan={5} style={{ textAlign: 'center', padding: '20px' }}>
                  <Text italic>No findings recorded yet.</Text>
                </TableCell>
              </TableRow>
            )}
          </TableBody>
        </Table>
      </Card>

      <Card>
        <CardHeader 
          header={<Title3>Escalation Evidence</Title3>} 
          action={<Button appearance="primary" onClick={() => setUploadModalOpen(true)}>Upload File</Button>}
        />
        <Table>
          <TableHeader>
            <TableRow>
              <TableHeaderCell>File Name</TableHeaderCell>
              <TableHeaderCell>Type</TableHeaderCell>
              <TableHeaderCell>Size</TableHeaderCell>
              <TableHeaderCell>Uploaded By</TableHeaderCell>
              <TableHeaderCell>Date</TableHeaderCell>
            </TableRow>
          </TableHeader>
          <TableBody>
            {evidenceList.map((file: any) => (
              <TableRow key={file.id}>
                <TableCell>{file.fileName}</TableCell>
                <TableCell>{file.contentType}</TableCell>
                <TableCell>{(file.sizeBytes / 1024).toFixed(1)} KB</TableCell>
                <TableCell>{file.uploadedByName}</TableCell>
                <TableCell>{new Date(file.uploadedAt).toLocaleDateString()}</TableCell>
              </TableRow>
            ))}
            {evidenceList.length === 0 && (
              <TableRow>
                <TableCell colSpan={5} style={{ textAlign: 'center', padding: '20px' }}>
                  <Text italic>No escalation evidence uploaded yet.</Text>
                </TableCell>
              </TableRow>
            )}
          </TableBody>
        </Table>
      </Card>

      <AddActionModal
        assessmentId={id!}
        findingId={actionFindingId ?? undefined} /* <-- Changed this line */
        open={actionFindingId !== null}
        onClose={() => setActionFindingId(null)}
      />

      <CreateFindingModal 
        assessmentId={id!} 
        open={findingModalOpen} 
        onClose={() => setFindingModalOpen(false)} 
      />

      <UploadEvidenceModal 
        assessmentId={id!} 
        open={uploadModalOpen} 
        onClose={() => setUploadModalOpen(false)} 
      />
    </div>
  );
}