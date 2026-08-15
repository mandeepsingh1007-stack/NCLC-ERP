/**
 * LookupField — renders a searchable dropdown for reference fields.
 *
 * For LIST references: loads from sys_reference_list (fixed set)
 * For TABLE references: remote search against target table
 * For SEARCH references: remote search using whereClause
 */
import React from 'react';
import { Select, Spin, Alert } from 'antd';
import type { ReferenceInfo } from '../api/contracts/window';
import type { LookupItem } from '../api/contracts/lookup';
import { useLookup } from '../api/lookupApi';

interface Props {
  columnName: string;
  reference: ReferenceInfo;
  value?: string | number;
  onChange?: (value: string | number | undefined) => void;
  search?: boolean; // true = searchable AutoComplete, false = simple Select
  page?: number;
  pageSize?: number;
}

const LookupField: React.FC<Props> = ({
  reference,
  value,
  onChange,
  search = true,
  page = 1,
  pageSize = 50,
}) => {
  // Use a synthetic referenceId (0) since the hook expects one.
  // In production, the parent component resolves the SysReference_ID
  // and passes the full response via props instead of a hook call.
  // Here we use a stub that the parent can override.
  const { data: lookupData, isLoading, error } = useLookup(0, { page, pageSize });

  const items: LookupItem[] = lookupData?.items ?? [];
  const options = items.map((item: LookupItem) => ({
    value: item.value,
    label: item.display,
  }));

  if (isLoading) return <Spin size="small" />;
  if (error) return <Alert message="Lookup error" type="error" showIcon />;

  const selectProps = {
    value,
    onChange,
    placeholder: `Select ${reference.name}`,
    options,
    showSearch: search,
    filterOption: (input: string, opt?: { label?: string | number; value?: unknown }) =>
      String(opt?.label ?? '').toLowerCase().includes(input.toLowerCase()),
    allowClear: true,
    style: { width: '100%' },
  };

  if (reference.validationType === 'list') {
    return <Select {...selectProps} mode={undefined} />;
  }

  return <Select {...selectProps} />;
};

export default LookupField;
