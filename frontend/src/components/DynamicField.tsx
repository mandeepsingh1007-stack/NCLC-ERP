/**
 * DynamicField — renders a single form field based on metadata ControlType.
 */
import React from 'react';
import { Input, InputNumber, Switch, Select, DatePicker, Form as AntForm, type SelectProps } from 'antd';
import type { Rule } from 'antd/lib/form';
import type { FieldContract } from '../api/contracts/window';
import { resolveUnknownControlType } from '../utils/controlTypeMap';
import { useDisplayLogic } from '../hooks/useDisplayLogic';

interface Props {
  field: FieldContract;
  form: unknown; // from useForm() — cast to avoid circular dep
  formData: Record<string, unknown>;
  context: { userId: string | null; tenantId: string | null; orgId: string | null; timestamp: string | null; userName: string | null };
  lookupData?: { value: string | number; display: string }[];
  onLookupChange?: (columnName: string, value: string | number) => void;
}

const DynamicField: React.FC<Props> = ({
  field,
  formData,
  context,
  lookupData,
  onLookupChange,
}) => {
  // Evaluate displayLogic — if false, field is hidden
  const isVisible = useDisplayLogic(field.displayLogic, context, formData);

  // Evaluate readOnlyLogic
  const isOverriddenReadOnly = useDisplayLogic(field.readOnlyLogic, context, formData);
  const isReadOnly = field.isReadOnly || isOverriddenReadOnly;

  // Evaluate mandatoryLogic
  const isOverriddenMandatory = useDisplayLogic(field.mandatoryLogic, context, formData);
  const isMandatory = field.isMandatory || isOverriddenMandatory;

  if (!isVisible) return null;

  const fieldName = field.columnName;
  const rules: Rule[] = [];
  if (isMandatory) rules.push({ required: true, message: `${field.label} is mandatory.` });

  const commonProps = {
    name: fieldName,
    label: field.label,
    tooltip: field.help,
    rules,
    disabled: isReadOnly,
  };

  let inputComponent: React.ReactElement;

  switch (resolveUnknownControlType(field.controlType)) {
    case 'TextInput':
      inputComponent = <Input {...commonProps} />;
      break;
    case 'TextArea':
      inputComponent = <Input.TextArea {...commonProps} rows={3} />;
      break;
    case 'NumberInput':
      inputComponent = <InputNumber {...commonProps} style={{ width: '100%' }} />;
      break;
    case 'YesNoToggle':
      inputComponent = <Switch checkedChildren="Yes" unCheckedChildren="No" {...commonProps} />;
      break;
    case 'Email':
      inputComponent = <Input {...commonProps} type="email" />;
      break;
    case 'URL':
      inputComponent = <Input {...commonProps} type="url" />;
      break;
    case 'Password':
      inputComponent = <Input.Password {...commonProps} />;
      break;
    case 'DateInput':
    case 'Date':
    case 'DateTime':
    case 'Time':
      inputComponent = <DatePicker {...commonProps} showTime={field.controlType === 'DateTime'} style={{ width: '100%' }} />;
      break;
    case 'ListDropdown':
    case 'MultiSelect': {
      const selectMode = field.controlType === 'MultiSelect' ? 'multiple' : undefined;
      const options: SelectProps['options'] = (lookupData ?? []).map((item) => ({
        value: item.value,
        label: item.display,
      }));
      inputComponent = (
        <Select
          {...commonProps}
          mode={selectMode}
          options={options}
          placeholder={`Select ${field.label}`}
          allowClear
        />
      );
      break;
    }
    case 'TableLookup':
    case 'SearchPopup': {
      // Remote search lookup — render AutoComplete with data from hook
      const options: SelectProps['options'] = (lookupData ?? []).map((item) => ({
        value: String(item.value),
        label: item.display,
      }));
      inputComponent = (
        <Select
          {...commonProps}
          options={options}
          placeholder={`Search ${field.label}`}
          allowClear
          showSearch
          filterOption={(input, opt) => String(opt?.label ?? '').toLowerCase().includes(input.toLowerCase())}
          onChange={(val) => onLookupChange?.(fieldName, val)}
        />
      );
      break;
    }
    default:
      // Fallback for unknown control types — controlled error, not crash
      inputComponent = <Input placeholder={`Unknown control: ${field.controlType}`} {...commonProps} />;
  }

  return (
    <AntForm.Item {...commonProps}>
      {inputComponent}
    </AntForm.Item>
  );
};

export default DynamicField;
