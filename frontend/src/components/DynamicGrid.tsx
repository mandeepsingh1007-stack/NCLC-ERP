/**
 * DynamicGrid — renders a data table entirely from metadata.
 *
 * Features:
 * - Paginated table from DataTableResponse
 * - Column headers from field metadata (label, colSpan)
 * - Sortable columns (delegated to backend via sortBy/sortDir params)
 * - Filterable (backend receives JSON filter AST)
 * - Responsive sizing via colSpan
 */
import React, { useMemo, useState } from 'react';
import { Table, Input, Button } from 'antd';
import type { ColumnsType } from 'antd/es/table/interface';
import { SearchOutlined, ReloadOutlined } from '@ant-design/icons';
import type { FieldContract } from '../api/contracts/window';
import { useDataTable } from '../api/dataApi';

interface Props {
  tableName: string;
  fields: FieldContract[];
  initialFilter?: string;
  refreshInterval?: number; // ms, 0 = no auto-refresh
  onRowClick?: (record: Record<string, unknown>) => void;
}

const DynamicGrid: React.FC<Props> = ({
  tableName,
  fields,
  initialFilter,
  refreshInterval = 0,
  onRowClick,
}) => {
  const [page, setPage] = useState(1);
  const [pageSize] = useState(50);
  const [sortBy, setSortBy] = useState<string | undefined>(undefined);
  const [sortDir, setSortDir] = useState<'asc' | 'desc' | undefined>(undefined);
  const [searchText, setSearchText] = useState('');

  const filterParam = useMemo(() => {
    if (!searchText) return initialFilter ?? undefined;
    // Build a simple "like" filter on all text fields
    const textFields = fields.filter((f) => f.controlType === 'TextInput' || f.controlType === 'TextArea' || f.controlType === 'Email');
    if (textFields.length === 0) return initialFilter ?? undefined;
    const conditions = textFields.map((f) => `like ${f.columnName} '%'`).join('&&');
    return [initialFilter, `(${conditions})`].filter(Boolean).join('&&') ?? `(${conditions})`;
  }, [searchText, initialFilter, fields]);

  const { data, isLoading, error, refetch } = useDataTable(tableName, {
    page,
    pageSize,
    sortBy,
    sortDir,
    filter: filterParam,
  });

  const columns = useMemo((): ColumnsType<Record<string, unknown>> => {
    return fields
      .map((field) => ({
        title: field.label,
        dataIndex: field.columnName,
        key: field.columnName,
        width: 120,
        ellipsis: true,
        sorter: field.controlType !== 'TextInput' && field.controlType !== 'TextArea' && field.controlType !== 'Email' && field.controlType !== 'URL' && field.controlType !== 'Password',
        onSorterChange: (order: 'asc' | 'desc' | null) => {
          setSortBy(field.columnName);
          setSortDir(order ?? undefined);
        },
        render: (_value: unknown, record: Record<string, unknown>) => {
          const val = record[field.columnName];
          return val ?? '';
        },
      }));
  }, [fields]);

  const handleRefresh = () => {
    refetch();
  };

  if (error) {
    return <div style={{ padding: 16, color: '#ff4d4f' }}>Failed to load data.</div>;
  }

  return (
    <div>
      {/* Toolbar */}
      <div style={{ display: 'flex', gap: 8, marginBottom: 16 }}>
        <Input
          placeholder="Search..."
          prefix={<SearchOutlined />}
          style={{ width: 250 }}
          value={searchText}
          onChange={(e) => { setSearchText(e.target.value); setPage(1); }}
          allowClear
        />
        <Button icon={<ReloadOutlined />} onClick={handleRefresh}>
          Refresh
        </Button>
      </div>

      <Table
        columns={columns}
        dataSource={data?.items ?? []}
        rowKey={(_: Record<string, unknown>, index?: number) => `row-${index ?? 0}`}
        loading={isLoading}
        pagination={{
          current: page,
          pageSize,
          total: data?.pagination.totalItems ?? 0,
          onChange: (p) => setPage(p),
          showSizeChanger: false,
        }}
        onRow={(record: Record<string, unknown>) => ({
          onClick: onRowClick ? () => onRowClick(record) : undefined,
          style: { cursor: onRowClick ? 'pointer' : 'default' },
        })}
        scroll={{ x: true }}
        size="small"
      />
    </div>
  );
};

export default DynamicGrid;
