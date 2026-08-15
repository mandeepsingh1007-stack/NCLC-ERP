/**
 * FieldGroup — renders a collapsible section for grouped fields.
 */
import React from 'react';
import { Collapse, Row, Col } from 'antd';
import type { FieldGroupContract } from '../api/contracts/window';
import DynamicField from './DynamicField';
import type { FieldContract } from '../api/contracts/window';

interface Props {
  group: FieldGroupContract;
  fields: FieldContract[];
  form: unknown;
  formData: Record<string, unknown>;
  context: { userId: string | null; tenantId: string | null; orgId: string | null; timestamp: string | null; userName: string | null };
  lookupDataMap?: Map<string, { value: string | number; display: string }[]>;
  onLookupChange?: (columnName: string, value: string | number) => void;
}

const FieldGroup: React.FC<Props> = ({
  group,
  fields,
  form,
  formData,
  context,
  lookupDataMap,
  onLookupChange,
}) => {
  const groupFields = fields.filter((f) => f.fieldGroup === group.groupName);

  if (groupFields.length === 0) return null;

  return (
    <Collapse
      defaultActiveKey={!group.isCollapsed ? [group.groupName] : []}
      items={[
        {
          key: group.groupName,
          label: group.label,
          children: (
            <Row gutter={[16, 16]}>
              {groupFields.map((field) => (
                <Col key={field.columnName} span={24 / (group.colSpan > 0 ? group.colSpan : 1)}>
                  <DynamicField
                    field={field}
                    form={form}
                    formData={formData}
                    context={context}
                    lookupData={lookupDataMap?.get(field.columnName)}
                    onLookupChange={onLookupChange}
                  />
                </Col>
              ))}
            </Row>
          ),
          showArrow: true,
        },
      ]}
    />
  );
};

export default FieldGroup;
