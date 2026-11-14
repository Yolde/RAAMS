import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import {
  Title1, Text, Spinner, Table, TableHeader, TableRow, TableHeaderCell,
  TableBody, TableCell, Checkbox, MessageBar, MessageBarBody, Card
} from '@fluentui/react-components';
import { client } from '../api/client';

interface FieldPermission {
  id: string;
  roleId: string;
  roleName: string;
  entityType: string;
  fieldName: string;
  canView: boolean;
  canEdit: boolean;
}

export function AdminPermissionsPage() {
  const queryClient = useQueryClient();
  const [success, setSuccess] = useState(false);

  // Fetch all field permissions
  const { data: permissions = [], isLoading, isError } = useQuery<FieldPermission[]>({
    queryKey: ['field-permissions'],
    queryFn: async () => {
      const res = await client.get('/api/field-permissions');
      return res.data;
    }
  });

  // Mutation to update a permission
  const mutation = useMutation({
    mutationFn: async (payload: { roleId: string; entityType: string; fieldName: string; canView: boolean; canEdit: boolean }) => {
      await client.put('/api/field-permissions', payload);
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['field-permissions'] });
      setSuccess(true);
      setTimeout(() => setSuccess(false), 3000);
    },
    onError: (err: any) => {
      alert(`Failed to update: ${err.response?.data?.error || err.message}`);
    }
  });

  if (isLoading) return <Spinner label="Loading permissions..." />;
  if (isError) return <Text>Error loading permissions.</Text>;

  // Group permissions by EntityType and FieldName to build the rows
  const grouped = permissions.reduce((acc, p) => {
    const key = `${p.entityType}.${p.fieldName}`;
    if (!acc[key]) {
      acc[key] = { entityType: p.entityType, fieldName: p.fieldName, roles: [] };
    }
    acc[key].roles.push(p);
    return acc;
  }, {} as Record<string, { entityType: string; fieldName: string; roles: FieldPermission[] }>);

  const rows = Object.values(grouped);
  const allRoles = Array.from(new Set(permissions.map(p => p.roleName))).sort();

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '20px' }}>
      <Title1>Admin: Field-Level Permissions</Title1>
      
      {success && (
        <MessageBar intent="success">
          <MessageBarBody>Permission updated successfully.</MessageBarBody>
        </MessageBar>
      )}

      <Text>Toggle which roles can View or Edit specific fields. GRA and SuperAdmin always have full access.</Text>

      <Card style={{ padding: '16px', overflowX: 'auto' }}>
        <Table>
          <TableHeader>
            <TableRow>
              <TableHeaderCell>Entity</TableHeaderCell>
              <TableHeaderCell>Field</TableHeaderCell>
              {allRoles.map(role => (
                <TableHeaderCell key={role} style={{ textAlign: 'center' }}>
                  {role}
                </TableHeaderCell>
              ))}
            </TableRow>
          </TableHeader>
          <TableBody>
            {rows.map(row => (
              <TableRow key={`${row.entityType}.${row.fieldName}`}>
                <TableCell>{row.entityType}</TableCell>
                <TableCell>{row.fieldName}</TableCell>
                {allRoles.map(role => {
                  // Find the permission for this role, or default to false
                  const permission = row.roles.find(p => p.roleName === role);
                  const canView = permission?.canView ?? false;
                  const canEdit = permission?.canEdit ?? false;
                  const roleId = permission?.roleId ?? ''; 
                  // If the role has no permission record yet, we can't toggle it without a RoleId
                  const isClickable = !!roleId;

                  return (
                    <TableCell key={role} style={{ textAlign: 'center' }}>
                      {isClickable ? (
                        <div style={{ display: 'flex', flexDirection: 'column', gap: '4px', alignItems: 'center' }}>
                          <Checkbox 
                            label="View" 
                            checked={canView} 
                            onChange={(_, data) => {
                              mutation.mutate({
                                roleId, entityType: row.entityType, fieldName: row.fieldName,
                                canView: !!data.checked, canEdit
                              });
                            }}
                          />
                          <Checkbox 
                            label="Edit" 
                            checked={canEdit} 
                            onChange={(_, data) => {
                              mutation.mutate({
                                roleId, entityType: row.entityType, fieldName: row.fieldName,
                                canView, canEdit: !!data.checked
                              });
                            }}
                          />
                        </div>
                      ) : (
                        <Text size={200} italic>(no access)</Text>
                      )}
                    </TableCell>
                  );
                })}
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </Card>
    </div>
  );
}