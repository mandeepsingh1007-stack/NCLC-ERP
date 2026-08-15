/**
 * DynamicForm, DynamicGrid, LookupField Component Tests
 *
 * Tests form rendering, mode behavior, grid columns, and lookup rendering.
 * useDisplayLogic is mocked for DynamicField integration.
 * useDataTable is mocked to avoid API calls.
 * useLookup is mocked to avoid API calls.
 */
import React from 'react';
import { render, screen } from '@testing-library/react';
import '@testing-library/jest-dom';

// Mock antd icons to avoid ESM issues with @ant-design/colors
jest.mock('@ant-design/icons', () => ({
  SearchOutlined: (props: unknown) => <span {...props} />,
  ReloadOutlined: (props: unknown) => <span {...props} />,
}));

// Mock useDisplayLogic to always show fields
jest.mock('../hooks/useDisplayLogic', () => ({
  useDisplayLogic: jest.fn(() => true),
}));

// Mock useDataTable for DynamicGrid
jest.mock('../api/dataApi', () => {
  const mockFn = jest.fn();
  return {
    useDataTable: mockFn,
    dataKeys: { all: ['data'], table: jest.fn(), item: jest.fn() },
  };
});

// Mock useLookup for LookupField
jest.mock('../api/lookupApi', () => ({
  useLookup: jest.fn(() => ({
    data: {
      items: [],
      totalItems: 0,
      pagination: { page: 1, pageSize: 50, totalItems: 0, totalPages: 0 },
    },
    isLoading: false,
    error: null,
  })),
}));

import DynamicForm from '../components/DynamicForm';
import DynamicGrid from '../components/DynamicGrid';
import LookupField from '../components/LookupField';
import { useDataTable } from '../api/dataApi';
import { useLookup } from '../api/lookupApi';
import type { TabContract, FieldContract } from '../api/contracts/window';
import type { LookupItem } from '../api/contracts/lookup';

const mockUseDataTable = useDataTable as jest.MockedFunction<typeof useDataTable>;
const mockUseLookup = useLookup as jest.MockedFunction<typeof useLookup>;

const defaultDataTableResult = {
  data: { items: [], pagination: { page: 1, pageSize: 50, totalItems: 0, totalPages: 0 } },
  isLoading: false,
  error: null,
  refetch: () => Promise.resolve({ data: null }),
};

function setupDataMock(result: ReturnType<typeof useDataTable>) {
  mockUseDataTable.mockReturnValue(result);
}

function setupLookupMock(result: ReturnType<typeof useLookup>) {
  mockUseLookup.mockReturnValue(result);
}

function makeTab(overrides: Partial<TabContract> = {}): TabContract {
  return {
    tabId: 1,
    columnName: 'Main',
    name: 'Main',
    sysTableId: 1,
    isGrid: false,
    isDefault: true,
    whereClause: '',
    fields: [
      {
        columnName: 'Name',
        label: 'Name',
        controlType: 'TextInput',
        isMandatory: true,
        isReadOnly: false,
        isMandatoryOverride: false,
        isReadOnlyOverride: false,
        colSpan: 6,
        rowSpan: 1,
      } as FieldContract,
    ],
    fieldGroups: [
      {
        groupName: 'Main',
        label: 'Main',
        colSpan: 12,
        isCollapsed: false,
        fieldColumnNames: ['Name'],
      },
    ],
    ...overrides,
  };
}

describe('DynamicForm', () => {
  beforeEach(() => {
    setupDataMock(defaultDataTableResult);
  });
  it('renders a create form with submit and cancel buttons', () => {
    render(<DynamicForm tab={makeTab()} mode="create" />);
    expect(screen.getByRole('button', { name: /create/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /cancel/i })).toBeInTheDocument();
  });

  it('renders an edit form with save button', () => {
    render(<DynamicForm tab={makeTab()} mode="edit" />);
    expect(screen.getByRole('button', { name: /save/i })).toBeInTheDocument();
  });

  it('hides buttons in view mode', () => {
    const { container } = render(<DynamicForm tab={makeTab()} mode="view" />);
    const buttons = container.querySelectorAll('button');
    expect(buttons.length).toBe(0);
  });

  it('renders form with initial data', () => {
    const { container } = render(
      <DynamicForm tab={makeTab()} mode="edit" initialData={{ Name: 'Test Name' }} />,
    );
    const form = container.querySelector('form');
    expect(form).toBeInTheDocument();
  });

  it('shows loading state', () => {
    const { container } = render(<DynamicForm tab={makeTab()} mode="create" loading={true} />);
    const spin = container.querySelector('.ant-spin');
    expect(spin).toBeInTheDocument();
  });

  it('shows error state', () => {
    const { container } = render(
      <DynamicForm tab={makeTab()} mode="create" error={new Error('Load failed')} />,
    );
    expect(screen.getByText(/failed to load/i)).toBeInTheDocument();
  });

  it('shows empty state when no fields', () => {
    render(<DynamicForm tab={makeTab({ fields: [], fieldGroups: [] })} mode="create" />);
    expect(screen.getByText(/no fields/i)).toBeInTheDocument();
  });
});

describe('DynamicGrid', () => {
  beforeEach(() => {
    setupDataMock(defaultDataTableResult);
  });
  const gridFields: FieldContract[] = [
    {
      columnName: 'Name',
      label: 'Full Name',
      controlType: 'TextInput',
      isMandatory: false,
      isReadOnly: false,
      isMandatoryOverride: false,
      isReadOnlyOverride: false,
      colSpan: 6,
      rowSpan: 1,
    },
    {
      columnName: 'Count',
      label: 'Count',
      controlType: 'NumberInput',
      isMandatory: false,
      isReadOnly: false,
      isMandatoryOverride: false,
      isReadOnlyOverride: false,
      colSpan: 4,
      rowSpan: 1,
    },
  ];

  it('renders search input and refresh button', () => {
    render(<DynamicGrid tableName="X_Test" fields={gridFields} />);
    expect(screen.getByPlaceholderText(/search/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /refresh/i })).toBeInTheDocument();
  });

  it('renders column headers from field metadata', () => {
    const { container } = render(<DynamicGrid tableName="X_Test" fields={gridFields} />);
    // Column headers render as <th> elements
    const headers = container.querySelectorAll('th');
    const headerTexts = Array.from(headers).map(h => h.textContent);
    expect(headerTexts).toContain('Full Name');
    expect(headerTexts).toContain('Count');
  });

  it('renders empty table when no data', () => {
    const { container } = render(<DynamicGrid tableName="X_Test" fields={gridFields} />);
    const table = container.querySelector('table');
    expect(table).toBeInTheDocument();
  });

  it('passes table name to useDataTable', () => {
    render(<DynamicGrid tableName="X_Contacts" fields={gridFields} />);
    expect(mockUseDataTable).toHaveBeenCalledWith('X_Contacts', expect.any(Object));
  });
});

describe('LookupField', () => {
  it('renders a loading spinner when loading', () => {
    mockUseLookup.mockReturnValue({
      data: null,
      isLoading: true,
      error: null,
    });
    const { container } = render(
      <LookupField
        columnName="AccountId"
        reference={{ name: 'X_Account', validationType: 'table' }}
      />,
    );
    const spin = container.querySelector('.ant-spin');
    expect(spin).toBeInTheDocument();
  });

  it('renders error state on lookup error', () => {
    mockUseLookup.mockReturnValue({
      data: null,
      isLoading: false,
      error: new Error('Lookup failed'),
    });
    const { container } = render(
      <LookupField
        columnName="AccountId"
        reference={{ name: 'X_Account', validationType: 'table' }}
      />,
    );
    const alert = container.querySelector('.ant-alert-error');
    expect(alert).toBeInTheDocument();
  });

  it('renders a Select with lookup items', () => {
    mockUseLookup.mockReturnValue({
      data: {
        items: [
          { value: 1, display: 'Account A' },
          { value: 2, display: 'Account B' },
        ] as LookupItem[],
        totalItems: 2,
        pagination: { page: 1, pageSize: 50, totalItems: 2, totalPages: 1 },
      },
      isLoading: false,
      error: null,
    });
    const { container } = render(
      <LookupField
        columnName="Status"
        reference={{ name: 'Status', validationType: 'list' }}
      />,
    );
    const select = container.querySelector('.ant-select');
    expect(select).toBeInTheDocument();
  });

  it('renders table reference with search', () => {
    mockUseLookup.mockReturnValue({
      data: {
        items: [] as LookupItem[],
        totalItems: 0,
        pagination: { page: 1, pageSize: 50, totalItems: 0, totalPages: 0 },
      },
      isLoading: false,
      error: null,
    });
    const { container } = render(
      <LookupField
        columnName="AccountId"
        reference={{ name: 'X_Account', validationType: 'table' }}
        search={true}
      />,
    );
    const select = container.querySelector('.ant-select');
    expect(select).toBeInTheDocument();
  });

  it('renders empty select when no items', () => {
    mockUseLookup.mockReturnValue({
      data: {
        items: [] as LookupItem[],
        totalItems: 0,
        pagination: { page: 1, pageSize: 50, totalItems: 0, totalPages: 0 },
      },
      isLoading: false,
      error: null,
    });
    const { container } = render(
      <LookupField
        columnName="Status"
        reference={{ name: 'Status', validationType: 'list' }}
      />,
    );
    const select = container.querySelector('.ant-select');
    expect(select).toBeInTheDocument();
  });
});
