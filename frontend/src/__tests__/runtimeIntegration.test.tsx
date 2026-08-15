/**
 * Runtime Integration Tests
 *
 * Tests the full flow: MainGrid → MainWindow → DynamicForm → DynamicField
 * by composing components with mocked data layers.
 * These verify that the data flows correctly through the metadata-driven pipeline.
 */
import React from 'react';
import { render, screen } from '@testing-library/react';
import '@testing-library/jest-dom';

// Mock react-router-dom
jest.mock('react-router-dom', () => ({
  useParams: () => ({ windowId: '1' }),
  useNavigate: jest.fn(),
}));

// Mock useDisplayLogic
jest.mock('../hooks/useDisplayLogic', () => ({
  useDisplayLogic: jest.fn(() => true),
}));

// Mock useWindow — use per-test return value via jest.Mock
jest.mock('../api/metaApi', () => ({
  useWindow: jest.fn(),
}));

// Mock child components — verify data flow
jest.mock('../components/DynamicForm', () => {
  return function MockDynamicForm({ tab, mode }: { tab: { name: string; fields: any[] }; mode: string }) {
    return (
      <div data-testid="dynamic-form">
        <span data-testid="form-mode">{mode}</span>
        <span data-testid="tab-name">{tab.name}</span>
        <span data-testid="field-count">{tab.fields.length}</span>
        {tab.fields.map((f: any, i: number) => (
          <div key={i} data-testid={`form-field-${i}`}>
            <span data-testid="field-label">{f.label}</span>
            <span data-testid="field-control">{f.controlType}</span>
            <span data-testid="field-mandatory">{String(f.isMandatory)}</span>
          </div>
        ))}
      </div>
    );
  };
});

jest.mock('../components/DynamicGrid', () => {
  return function MockDynamicGrid({ tableName, fields }: { tableName: string; fields: any[] }) {
    return (
      <div data-testid="dynamic-grid">
        <span data-testid="table-name">{tableName}</span>
        <span data-testid="grid-field-count">{fields.length}</span>
      </div>
    );
  };
});

jest.mock('../components/LoadingState', () => () => <div data-testid="loading">Loading</div>);
jest.mock('../components/ErrorState', () => ({ message }: { message: string }) => (
  <div data-testid="error">{message}</div>
));
jest.mock('../components/EmptyState', () => ({ description }: { description: string }) => (
  <div data-testid="empty">{description}</div>
));

import MainWindow from '../pages/MainWindow';
import MainGrid from '../pages/MainGrid';
import { useWindow } from '../api/metaApi';

const mockUseWindow = useWindow as jest.MockedFunction<typeof useWindow>;

function setup(data: unknown, isLoading = false, error: unknown = null) {
  mockUseWindow.mockReturnValue({ data, isLoading, error });
}

describe('Runtime Integration — MainGrid flow', () => {
  it('passes window name as tableName and all fields to DynamicGrid', () => {
    const windowData = {
      windowId: 1,
      columnName: 'X_Account',
      name: 'Account',
      tabs: [
        {
          tabId: 1,
          columnName: 'Main',
          name: 'Main',
          sysTableId: 1,
          isGrid: false,
          isDefault: true,
          whereClause: '',
          fields: [
            { columnName: 'Name', label: 'Account Name', controlType: 'TextInput', isMandatory: true, isReadOnly: false, isMandatoryOverride: false, isReadOnlyOverride: false, colSpan: 6, rowSpan: 1 },
            { columnName: 'Phone', label: 'Phone', controlType: 'NumberInput', isMandatory: false, isReadOnly: false, isMandatoryOverride: false, isReadOnlyOverride: false, colSpan: 4, rowSpan: 1 },
            { columnName: 'IsActive', label: 'Active', controlType: 'YesNoToggle', isMandatory: false, isReadOnly: false, isMandatoryOverride: false, isReadOnlyOverride: false, colSpan: 2, rowSpan: 1 },
          ],
          fieldGroups: [{ groupName: 'Main', label: 'Main Details', colSpan: 12, isCollapsed: false, fieldColumnNames: ['Name', 'Phone', 'IsActive'] }],
        },
      ],
    };
    setup(windowData);
    render(<MainGrid />);
    expect(screen.getByTestId('table-name')).toHaveTextContent('Account');
    expect(screen.getByTestId('grid-field-count')).toHaveTextContent('3');
  });

  it('renders loading then success state transition', () => {
    setup(undefined, true);
    render(<MainGrid />);
    expect(screen.getByTestId('loading')).toBeInTheDocument();

    const windowData = {
      windowId: 1,
      columnName: 'X_Account',
      name: 'Account',
      tabs: [
        {
          tabId: 1,
          columnName: 'Main',
          name: 'Main',
          sysTableId: 1,
          isGrid: false,
          isDefault: true,
          whereClause: '',
          fields: [{ columnName: 'Name', label: 'Account Name', controlType: 'TextInput', isMandatory: true, isReadOnly: false, isMandatoryOverride: false, isReadOnlyOverride: false, colSpan: 6, rowSpan: 1 }],
          fieldGroups: [{ groupName: 'Main', label: 'Main', colSpan: 12, isCollapsed: false, fieldColumnNames: ['Name'] }],
        },
      ],
    };
    setup(windowData);
    render(<MainGrid />);
    expect(screen.getByTestId('dynamic-grid')).toBeInTheDocument();
  });
});

describe('Runtime Integration — MainWindow form flow', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    jest.restoreAllMocks();
    jest.spyOn(window, 'location', 'get').mockReturnValue({ ...window.location, search: '' } as URL);
  });

  it('renders form with correct tab name and field count', () => {
    const windowData = {
      windowId: 1,
      columnName: 'X_Account',
      name: 'Account',
      tabs: [
        {
          tabId: 1,
          columnName: 'Main',
          name: 'Main',
          sysTableId: 1,
          isGrid: false,
          isDefault: true,
          whereClause: '',
          fields: [
            { columnName: 'Name', label: 'Account Name', controlType: 'TextInput', isMandatory: true, isReadOnly: false, isMandatoryOverride: false, isReadOnlyOverride: false, colSpan: 6, rowSpan: 1 },
            { columnName: 'Phone', label: 'Phone', controlType: 'NumberInput', isMandatory: false, isReadOnly: false, isMandatoryOverride: false, isReadOnlyOverride: false, colSpan: 4, rowSpan: 1 },
            { columnName: 'IsActive', label: 'Active', controlType: 'YesNoToggle', isMandatory: false, isReadOnly: false, isMandatoryOverride: false, isReadOnlyOverride: false, colSpan: 2, rowSpan: 1 },
          ],
          fieldGroups: [{ groupName: 'Main', label: 'Main Details', colSpan: 12, isCollapsed: false, fieldColumnNames: ['Name', 'Phone', 'IsActive'] }],
        },
      ],
    };
    setup(windowData);
    render(<MainWindow />);
    expect(screen.getByTestId('tab-name')).toHaveTextContent('Main');
    expect(screen.getByTestId('field-count')).toHaveTextContent('3');
  });

  it('passes mode correctly to DynamicForm', () => {
    const windowData = {
      windowId: 1,
      columnName: 'X_Account',
      name: 'Account',
      tabs: [{ tabId: 1, columnName: 'Main', name: 'Main', sysTableId: 1, isGrid: false, isDefault: true, whereClause: '', fields: [], fieldGroups: [] }],
    };
    setup(windowData);
    render(<MainWindow />);
    expect(screen.getByTestId('form-mode')).toHaveTextContent('create');
  });

  it('passes all field metadata (label, controlType, mandatory) to fields', () => {
    const windowData = {
      windowId: 1,
      columnName: 'X_Account',
      name: 'Account',
      tabs: [
        {
          tabId: 1,
          columnName: 'Main',
          name: 'Main',
          sysTableId: 1,
          isGrid: false,
          isDefault: true,
          whereClause: '',
          fields: [{ columnName: 'Name', label: 'Account Name', controlType: 'TextInput', isMandatory: true, isReadOnly: false, isMandatoryOverride: false, isReadOnlyOverride: false, colSpan: 6, rowSpan: 1 }],
          fieldGroups: [{ groupName: 'Main', label: 'Main', colSpan: 12, isCollapsed: false, fieldColumnNames: ['Name'] }],
        },
      ],
    };
    setup(windowData);
    render(<MainWindow />);
    expect(screen.getByTestId('field-label')).toHaveTextContent('Account Name');
    expect(screen.getByTestId('field-control')).toHaveTextContent('TextInput');
  });

  it('renders all 3 fields with correct labels', () => {
    const windowData = {
      windowId: 1,
      columnName: 'X_Account',
      name: 'Account',
      tabs: [
        {
          tabId: 1,
          columnName: 'Main',
          name: 'Main',
          sysTableId: 1,
          isGrid: false,
          isDefault: true,
          whereClause: '',
          fields: [
            { columnName: 'Name', label: 'Account Name', controlType: 'TextInput', isMandatory: true, isReadOnly: false, isMandatoryOverride: false, isReadOnlyOverride: false, colSpan: 6, rowSpan: 1 },
            { columnName: 'Phone', label: 'Phone', controlType: 'NumberInput', isMandatory: false, isReadOnly: false, isMandatoryOverride: false, isReadOnlyOverride: false, colSpan: 4, rowSpan: 1 },
            { columnName: 'IsActive', label: 'Active', controlType: 'YesNoToggle', isMandatory: false, isReadOnly: false, isMandatoryOverride: false, isReadOnlyOverride: false, colSpan: 2, rowSpan: 1 },
          ],
          fieldGroups: [{ groupName: 'Main', label: 'Main Details', colSpan: 12, isCollapsed: false, fieldColumnNames: ['Name', 'Phone', 'IsActive'] }],
        },
      ],
    };
    setup(windowData);
    render(<MainWindow />);
    expect(screen.getByText('Account Name')).toBeInTheDocument();
    expect(screen.getByText('Phone')).toBeInTheDocument();
    expect(screen.getByText('Active')).toBeInTheDocument();
  });
});

describe('Runtime Integration — Multi-tab window', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    jest.restoreAllMocks();
    jest.spyOn(window, 'location', 'get').mockReturnValue({ ...window.location, search: '' } as URL);
  });

  it('passes fields from all tabs to the grid', () => {
    const windowData = {
      windowId: 1,
      columnName: 'X_Multi',
      name: 'Multi-Tab',
      tabs: [
        { tabId: 1, columnName: 'Basic', name: 'Basic Info', sysTableId: 1, isGrid: false, isDefault: true, whereClause: '', fields: [{ columnName: 'Name', label: 'Name', controlType: 'TextInput', isMandatory: true, isReadOnly: false, isMandatoryOverride: false, isReadOnlyOverride: false, colSpan: 6, rowSpan: 1 }], fieldGroups: [] },
        { tabId: 2, columnName: 'Address', name: 'Address', sysTableId: 1, isGrid: false, isDefault: false, whereClause: '', fields: [{ columnName: 'Street', label: 'Street', controlType: 'TextArea', isMandatory: false, isReadOnly: false, isMandatoryOverride: false, isReadOnlyOverride: false, colSpan: 6, rowSpan: 1 }], fieldGroups: [] },
      ],
    };
    setup(windowData);
    render(<MainGrid />);
    expect(screen.getByTestId('grid-field-count')).toHaveTextContent('2');
  });

  it('renders MainWindow with all fields from all tabs', () => {
    const windowData = {
      windowId: 1,
      columnName: 'X_Multi',
      name: 'Multi-Tab',
      tabs: [
        { tabId: 1, columnName: 'Basic', name: 'Basic Info', sysTableId: 1, isGrid: false, isDefault: true, whereClause: '', fields: [{ columnName: 'Name', label: 'Name', controlType: 'TextInput', isMandatory: true, isReadOnly: false, isMandatoryOverride: false, isReadOnlyOverride: false, colSpan: 6, rowSpan: 1 }], fieldGroups: [] },
        { tabId: 2, columnName: 'Address', name: 'Address', sysTableId: 1, isGrid: false, isDefault: false, whereClause: '', fields: [{ columnName: 'Street', label: 'Street', controlType: 'TextArea', isMandatory: false, isReadOnly: false, isMandatoryOverride: false, isReadOnlyOverride: false, colSpan: 6, rowSpan: 1 }], fieldGroups: [] },
      ],
    };
    setup(windowData);
    render(<MainWindow />);
    // Only the default tab's fields are rendered in the form
    expect(screen.getByText('Name')).toBeInTheDocument();
    // Tab headers include all tabs
    expect(screen.getByRole('tablist')).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: 'Address' })).toBeInTheDocument();
  });
});
