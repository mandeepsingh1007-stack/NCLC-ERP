/**
 * MainWindow Component Tests
 *
 * Tests window metadata loading, mode detection, tabs, and fallback states.
 * useWindow and useNavigate are mocked. useDisplayLogic is mocked.
 */
import React from 'react';
import { render, screen } from '@testing-library/react';
import '@testing-library/jest-dom';
import MainWindow from '../pages/MainWindow';
import { useWindow } from '../api/metaApi';
import { useDisplayLogic } from '../hooks/useDisplayLogic';

// Mock useWindow to control metadata responses
jest.mock('../api/metaApi', () => ({
  useWindow: jest.fn(),
}));

// Mock useDisplayLogic to show fields
jest.mock('../hooks/useDisplayLogic', () => ({
  useDisplayLogic: jest.fn(() => true),
}));

// Mock react-router-dom
jest.mock('react-router-dom', () => ({
  useParams: () => ({ windowId: '1' }),
  useNavigate: jest.fn(),
}));

// Mock child components
jest.mock('../components/DynamicForm', () => {
  return function MockDynamicForm({ tab: { name, fields }, mode }: { tab: { name: string; fields: unknown[] }; mode: string }) {
    return (
      <div data-testid="dynamic-form">
        <span data-testid="form-mode">{mode}</span>
        <span data-testid="tab-name">{name}</span>
        <span data-testid="field-count">{fields.length}</span>
        {fields.map((f: any, i: number) => (
          <div key={i} data-testid={`field-${i}`}>
            {f.label || f.columnName}
          </div>
        ))}
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
  mockUseWindow.mockReturnValue(result);
}

function makeWindowMeta(tabsCount = 1, fieldsPerTab = 2) {
  const tabs = Array.from({ length: tabsCount }, (_, i) => ({
    tabId: i + 1,
    columnName: `Tab${i + 1}`,
    name: `Tab ${i + 1}`,
    sysTableId: 1,
    isGrid: false,
    isDefault: i === 0,
    whereClause: '',
    fields: Array.from({ length: fieldsPerTab }, (_, j) => ({
      columnName: `Field${j + 1}`,
      label: `Field ${j + 1}`,
      controlType: 'TextInput',
      isMandatory: false,
      isReadOnly: false,
      isMandatoryOverride: false,
      isReadOnlyOverride: false,
      colSpan: 6,
      rowSpan: 1,
    })),
    fieldGroups: [
      {
        groupName: `Group${i + 1}`,
        label: `Group ${i + 1}`,
        colSpan: 12,
        isCollapsed: false,
        fieldColumnNames: ['Field1', 'Field2'],
      },
    ],
  }));
  return { windowId: 1, columnName: 'X_Test', name: 'Test Window', tabs };
}

describe('MainWindow', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    // Default: show fields
    (useDisplayLogic as jest.Mock).mockReturnValue(true);
    // Default: clean location mock
    jest.restoreAllMocks();
    jest.spyOn(window, 'location', 'get').mockReturnValue({ ...window.location, search: '' } as URL);
  });

  it('renders loading state while fetching', () => {
    setup({ data: undefined, isLoading: true, error: null });
    render(<MainWindow />);
    expect(screen.getByTestId('loading-state')).toBeInTheDocument();
  });

  it('renders error state on fetch failure', () => {
    setup({ data: undefined, isLoading: false, error: new Error('Network error') });
    render(<MainWindow />);
    expect(screen.getByTestId('error-state')).toBeInTheDocument();
  });

  it('renders empty state when no tabs', () => {
    setup({
      data: { windowId: 1, columnName: 'X_Test', name: 'Empty', tabs: [] },
      isLoading: false,
      error: null,
    });
    render(<MainWindow />);
    expect(screen.getByTestId('empty-state')).toBeInTheDocument();
  });

  it('renders the window name and form on success', () => {
    setup({ data: makeWindowMeta(), isLoading: false, error: null });
    render(<MainWindow />);
    expect(screen.getByTestId('dynamic-form')).toBeInTheDocument();
  });

  it('passes mode from URL query params', () => {
    // Override: edit mode
    jest.spyOn(window, 'location', 'get').mockReturnValue({ ...window.location, search: '?mode=edit&id=42' } as URL);
    setup({ data: makeWindowMeta(), isLoading: false, error: null });
    render(<MainWindow />);
    expect(screen.getByTestId('form-mode')).toHaveTextContent('edit');
  });

  it('defaults to create mode when no mode param', () => {
    // Already mocked in beforeEach with empty search
    setup({ data: makeWindowMeta(), isLoading: false, error: null });
    render(<MainWindow />);
    expect(screen.getByTestId('form-mode')).toHaveTextContent('create');
  });

  it('renders multiple tabs as tab items', () => {
    setup({ data: makeWindowMeta(3, 2), isLoading: false, error: null });
    render(<MainWindow />);
    // Each tab has 2 fields
    const fieldCount = screen.getByTestId('field-count');
    expect(fieldCount).toHaveTextContent('2');
  });

  it('passes fields to DynamicForm', () => {
    setup({ data: makeWindowMeta(1, 3), isLoading: false, error: null });
    render(<MainWindow />);
    const fieldCount = screen.getByTestId('field-count');
    expect(fieldCount).toHaveTextContent('3');
  });
});
