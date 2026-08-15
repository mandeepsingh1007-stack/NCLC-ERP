/**
 * MainWindow — renders a form view for a specific window by ID.
 *
 * Loads window metadata via useWindow, then renders DynamicForm with
 * the resolved tab and fields. Supports create/edit/view modes.
 */
import React, { useEffect, useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { Card, Button, Tabs, type TabsProps } from 'antd';
import { ArrowLeftOutlined } from '@ant-design/icons';
import type { TabContract } from '../api/contracts/window';
import { useWindow } from '../api/metaApi';
import DynamicForm from '../components/DynamicForm';
import LoadingState from '../components/LoadingState';
import ErrorState from '../components/ErrorState';
import EmptyState from '../components/EmptyState';
import type { FormMode } from '../components/DynamicForm';

const MainWindow: React.FC = () => {
  const { windowId } = useParams<{ windowId: string }>();
  const navigate = useNavigate();
  const [mode, setMode] = useState<FormMode>('create');

  const windowIdNum = windowId ? Number(windowId) : 0;

  const { data: windowMeta, isLoading, error } = useWindow(windowIdNum);

  // Detect mode from URL query params (e.g., ?mode=edit&id=123)
  useEffect(() => {
    const params = new URLSearchParams(window.location.search);
    const m = params.get('mode');
    if (m === 'edit' || m === 'view') setMode(m);
  }, []);

  const tab = windowMeta?.tabs?.[0];

  if (isLoading) return <LoadingState />;
  if (error) return <ErrorState message="Failed to load window metadata." />;
  if (!windowMeta || windowMeta.tabs.length === 0) return <EmptyState description="No window metadata found." />;

  const tabItems: TabsProps['items'] = windowMeta.tabs.map((t: TabContract) => ({
    key: t.columnName,
    label: t.name,
    children: (
      <DynamicForm
        tab={t}
        mode={mode}
      />
    ),
  }));

  return (
    <Card>
      {/* Back button */}
      <Button
        icon={<ArrowLeftOutlined />}
        onClick={() => navigate(-1)}
        style={{ marginBottom: 16 }}
      >
        Back
      </Button>

      <h2 style={{ marginBottom: 16 }}>{windowMeta.name}</h2>

      {windowMeta.tabs.length > 1 ? (
        <Tabs defaultActiveKey={tab?.columnName} items={tabItems} />
      ) : (
        <DynamicForm tab={tab!} mode={mode} />
      )}
    </Card>
  );
};

export default MainWindow;
