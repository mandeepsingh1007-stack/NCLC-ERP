/**
 * TypeScript contracts matching the C# API response shapes.
 * Based on WindowMetadataBuilder.cs and Phase 3 API contracts.
 */

export type ControlType =
  | 'TextInput'
  | 'TextArea'
  | 'NumberInput'
  | 'DateInput'
  | 'YesNoToggle'
  | 'ListDropdown'
  | 'TableLookup'
  | 'SearchPopup'
  | 'MultiSelect'
  | 'Email'
  | 'URL'
  | 'Password'
  | 'RichText'
  | 'Image'
  | 'FileUpload'
  | 'Date'
  | 'Time'
  | 'DateTime';

export interface ReferenceInfo {
  name: string;
  validationType: string;
}

export interface FieldContract {
  columnName: string;
  label: string;
  help?: string;
  controlType: ControlType;
  isMandatory: boolean;
  isReadOnly: boolean;
  isMandatoryOverride: boolean;
  isReadOnlyOverride: boolean;
  colSpan: number;
  rowSpan: number;
  defaultValue?: string;
  displayLogic?: string;
  readOnlyLogic?: string;
  mandatoryLogic?: string;
  fieldGroup?: string;
  sysReference?: ReferenceInfo;
  fieldLength?: number;
}

export interface FieldGroupContract {
  groupName: string;
  label: string;
  colSpan: number;
  isCollapsed: boolean;
  fieldColumnNames: string[];
}

export interface TabContract {
  tabId: number;
  columnName: string;
  name: string;
  sysTableId: number;
  isGrid: boolean;
  isDefaultTab: boolean;
  whereClause?: string;
  fields: FieldContract[];
  fieldGroups: FieldGroupContract[];
}

export interface WindowContract {
  windowId: number;
  columnName: string;
  name: string;
  description?: string;
  help?: string;
  tabs: TabContract[];
}

export interface WindowsListResponse {
  windows: {
    windowId: number;
    columnName: string;
    name: string;
    description?: string;
  }[];
}

export interface MenuContract {
  menuId: number;
  columnName: string;
  name: string;
  icon?: string;
  sequence?: number;
  parentId: number | null;
  windowId: number | null;
  processId: number | null;
  isSeparator: boolean;
  children: MenuContract[];
}

export interface MenuHierarchyResponse {
  items: MenuContract[];
}
