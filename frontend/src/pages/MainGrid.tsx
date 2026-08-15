/**
 * MainGrid — renders a data grid view for a specific table/window.
 *
 * Loads window metadata to resolve the field list, then renders
 * DynamicGrid with pagination, sorting, and search.
 */
import React from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { Card, Button } from 'antd';
import { ArrowLeftOutlined } from '@ant-design/icons';
import type { TabContract } from '../api/contracts/window';
import { useWindow } from '../api/metaApi';
import DynamicGrid from '../components/DynamicGrid';
import LoadingState from '../components/LoadingState';
import ErrorState from '../components/ErrorState';
import EmptyState from '../components/EmptyState';

const MainGrid: React.FC = () => {
  const { windowId } = useParams<{ windowId: string }>();
  const navigate = useNavigate();
  const windowIdNum = windowId ? Number(windowId) : 0;

  const { data: windowMeta, isLoading, error } = useWindow(windowIdNum);

  if (isLoading) return <LoadingState />;
  if (error) return <ErrorState message="Failed to load window metadata." />;
  if (!windowMeta || windowMeta.tabs.length === 0) return <EmptyState description="No window metadata found." />;

  // Collect all fields from all tabs
  const allFields = windowMeta.tabs.flatMap((t: TabContract) => t.fields);
  if (allFields.length === 0) return <EmptyState description="No fields defined for this window." />;

  const handleRowClick = (_record: Record<string, unknown>) => {
    // Navigate to edit view for the clicked record
    // In production, pass record ID as query param
    navigate(`/window/${windowId}?mode=edit`);
  };

  return (
    <Card>
      <Button
        icon={<ArrowLeftOutlined />}
        onClick={() => navigate(-1)}
        style={{ marginBottom: 16 }}
      >
        Back
      </Button>

      <h2 style={{ marginBottom: 16 }}>{windowMeta.name}</h2>

      <DynamicGrid
        tableName={windowMeta.name}
        fields={allFields}
        onRowClick={handleRowClick}
      />
    </Card>
  );
};

export default MainGrid;
