/**
 * MainGrid Component Tests
 *
 * Tests grid rendering with window metadata, loading/error/empty states,
 * and row-click navigation. useWindow is mocked. useDisplayLogic is mocked.
 */
import React from 'react';
import { render, screen } from '@testing-library/react';
import '@testing-library/jest-dom';
import MainGrid from '../pages/MainGrid';
import { useWindow } from '../api/metaApi';
import { useDisplayLogic } from '../hooks/useDisplayLogic';

// Mock useWindow
jest.mock('../api/metaApi', () => ({
  useWindow: jest.fn(),
}));

// Mock useDisplayLogic
jest.mock('../hooks/useDisplayLogic', () => ({
  useDisplayLogic: jest.fn(() => true),
}));

// Mock react-router-dom
jest.mock('react-router-dom', () => ({
  useParams: () => ({ windowId: '1' }),
  useNavigate: jest.fn(),
}));

// Mock child components
jest.mock('../components/DynamicGrid', () => {
  return function MockDynamicGrid({ tableName, fields, onRowClick }: {
    tableName: string;
    fields: unknown[];
    onRowClick?: () => void;
  }) {
    return (
      <div data-testid="dynamic-grid">
        <span data-testid="table-name">{tableName}</span>
        <span data-testid="field-count">{fields.length}</span>
        <button data-testid="row-click-btn" onClick={onRowClick}>Click Row</button>
      </div>
    );
  };
});

jest.mock('../components/LoadingState', () => () => <div data-testid="loading-state">Loading...</div>);
jest.mock('../components/ErrorState', () => ({ message }: { message: string }) => (
  <div data-testid="error-state">{message}</div>
));
jest.mock('../components/EmptyState', () => ({ description }: { description: string }) => (
  <div data-testid="empty-state">{description}</div>
));

const mockUseWindow = useWindow as jest.MockedFunction<typeof useWindow>;

function setup(result: { data?: unknown; isLoading: boolean; error: unknown }) {
  (useWindow as jest.Mock).mockReturnValue(result);
}

function makeWindowMeta(fieldCount = 3) {
  return {
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
        fields: Array.from({ length: fieldCount }, (_, i) => ({
          columnName: [`Name`, `Phone`, `Email`][i] || `Field${i + 1}`,
          label: [`Name`, `Phone`, `Email`][i] || `Field ${i + 1}`,
          controlType: 'TextInput',
          isMandatory: false,
          isReadOnly: false,
          isMandatoryOverride: false,
          isReadOnlyOverride: false,
          colSpan: 6,
          rowSpan: 1,
        })),
        fieldGroups: [{
          groupName: 'Main',
          label: 'Main',
          colSpan: 12,
          isCollapsed: false,
          fieldColumnNames: ['Name'],
        }],
      },
    ],
  };
}

describe('MainGrid', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    (useDisplayLogic as jest.Mock).mockReturnValue(true);
  });

  it('renders loading state while fetching', () => {
    setup({ data: undefined, isLoading: true, error: null });
    render(<MainGrid />);
    expect(screen.getByTestId('loading-state')).toBeInTheDocument();
  });

  it('renders error state on fetch failure', () => {
    setup({ data: undefined, isLoading: false, error: new Error('Failed') });
    render(<MainGrid />);
    expect(screen.getByTestId('error-state')).toBeInTheDocument();
  });

  it('renders empty state when no tabs', () => {
    setup({ data: { windowId: 1, columnName: 'X_Test', name: 'Empty', tabs: [] }, isLoading: false, error: null });
    render(<MainGrid />);
    expect(screen.getByTestId('empty-state')).toHaveTextContent('No window metadata found');
  });

  it('renders empty state when no fields', () => {
    setup({ data: { windowId: 1, columnName: 'X_Test', name: 'NoFields', tabs: [{ tabId: 1, columnName: 'Main', name: 'Main', sysTableId: 1, isGrid: false, isDefault: true, whereClause: '', fields: [], fieldGroups: [] }] }, isLoading: false, error: null });
    render(<MainGrid />);
    expect(screen.getByTestId('empty-state')).toHaveTextContent('No fields defined');
  });

  it('passes window name as tableName', () => {
    setup({ data: makeWindowMeta(), isLoading: false, error: null });
    render(<MainGrid />);
    expect(screen.getByTestId('table-name')).toHaveTextContent('Account');
  });

  it('collects all fields from all tabs', () => {
    const windowMeta = {
      windowId: 1,
      columnName: 'X_Multi',
      name: 'Multi-Tab',
      tabs: [
        { tabId: 1, columnName: 'Tab1', name: 'Tab 1', sysTableId: 1, isGrid: false, isDefault: true, whereClause: '', fields: [{ columnName: 'A', label: 'A', controlType: 'TextInput', isMandatory: false, isReadOnly: false, isMandatoryOverride: false, isReadOnlyOverride: false, colSpan: 6, rowSpan: 1 }], fieldGroups: [] },
        { tabId: 2, columnName: 'Tab2', name: 'Tab 2', sysTableId: 1, isGrid: false, isDefault: false, whereClause: '', fields: [{ columnName: 'B', label: 'B', controlType: 'TextInput', isMandatory: false, isReadOnly: false, isMandatoryOverride: false, isReadOnlyOverride: false, colSpan: 6, rowSpan: 1 }], fieldGroups: [] },
      ],
    };
    setup({ data: windowMeta, isLoading: false, error: null });
    render(<MainGrid />);
    const count = screen.getByTestId('field-count');
    expect(count).toHaveTextContent('2');
  });

  it('renders DynamicGrid with onRowClick handler', () => {
    setup({ data: makeWindowMeta(), isLoading: false, error: null });
    render(<MainGrid />);
    expect(screen.getByTestId('row-click-btn')).toBeInTheDocument();
  });
});
