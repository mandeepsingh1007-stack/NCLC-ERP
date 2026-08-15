/**
 * DynamicForm — renders a form entirely from window/tab metadata.
 *
 * Mapping logic:
 * 1. Render ungrouped fields at top level
 * 2. Render each FieldGroup as collapsible section
 * 3. Each field evaluated for displayLogic, readOnlyLogic, mandatoryLogic
 * 4. ControlType mapped to Ant Design component via control registry
 */
import React, { useEffect, useState } from 'react';
import { Form, Button, Row, Col } from 'antd';
import type { TabContract } from '../api/contracts/window';
import DynamicField from './DynamicField';
import FieldGroup from './FieldGroup';
import LoadingState from './LoadingState';
import ErrorState from './ErrorState';
import EmptyState from './EmptyState';

export type FormMode = 'create' | 'edit' | 'view';

interface Props {
  tab: TabContract;
  initialData?: Record<string, unknown>;
  mode?: FormMode;
  loading?: boolean;
  error?: unknown;
  onSubmit?: (data: Record<string, unknown>) => Promise<void>;
}

const DynamicForm: React.FC<Props> = ({
  tab,
  initialData,
  mode = 'create',
  loading = false,
  error,
  onSubmit,
}) => {
  const [form] = Form.useForm();
  const [formData, setFormData] = useState<Record<string, unknown>>(initialData ?? {});
  const [submitting, setSubmitting] = useState(false);

  // Set initial values
  useEffect(() => {
    if (initialData) {
      form.setFieldsValue(initialData);
      setFormData(initialData);
    }
  }, [initialData, form]);

  const handleFinish = async (values: Record<string, unknown>) => {
    if (!onSubmit) return;
    setSubmitting(true);
    try {
      await onSubmit(values);
    } finally {
      setSubmitting(false);
    }
  };

  if (loading) return <LoadingState />;
  if (error) return <ErrorState message="Failed to load form data." />;
  if (tab.fields.length === 0) return <EmptyState description="No fields defined for this tab." />;

  const context = {
    userId: null,
    tenantId: null,
    orgId: null,
    timestamp: null,
    userName: null,
  };

  // Separate grouped vs ungrouped fields
  const groupedFieldNames = new Set<string>();
  tab.fieldGroups.forEach((g) => g.fieldColumnNames.forEach((n) => groupedFieldNames.add(n)));
  const ungroupedFields = tab.fields.filter((f) => !groupedFieldNames.has(f.columnName));

  const handleLookupChange = (columnName: string, value: string | number) => {
    form.setFieldValue(columnName, value);
    setFormData((prev) => ({ ...prev, [columnName]: value }));
  };

  return (
    <Form
      form={form}
      layout="vertical"
      onFinish={handleFinish}
      initialValues={initialData}
    >
      {/* Ungrouped fields */}
      {ungroupedFields.length > 0 && (
        <Row gutter={[16, 16]} style={{ marginBottom: 16 }}>
          {ungroupedFields.map((field) => (
            <Col key={field.columnName} span={24 / (field.colSpan > 0 ? field.colSpan : 1)}>
              <DynamicField
                field={field}
                form={form}
                formData={formData}
                context={context}
                onLookupChange={handleLookupChange}
              />
            </Col>
          ))}
        </Row>
      )}

      {/* Grouped fields */}
      {tab.fieldGroups.map((group) => (
        <FieldGroup
            key={group.groupName}
            group={group}
            fields={tab.fields}
            form={form}
            formData={formData}
            context={context}
            onLookupChange={handleLookupChange}
        />
      ))}

      {/* Action buttons */}
      {mode !== 'view' && (
        <Row gutter={[16, 16]} style={{ marginTop: 16 }}>
          <Col span={24}>
            <Button type="primary" htmlType="submit" loading={submitting}>
              {mode === 'create' ? 'Create' : 'Save'}
            </Button>
            <Button
              style={{ marginLeft: 8 }}
              onClick={() => form.resetFields()}
            >
              Cancel
            </Button>
          </Col>
        </Row>
      )}
    </Form>
  );
};

export default DynamicForm;
