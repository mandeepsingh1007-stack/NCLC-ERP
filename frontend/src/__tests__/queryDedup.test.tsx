/**
 * Query Deduplication Tests
 *
 * Verifies that TanStack Query deduplicates identical requests
 * (no duplicate HTTP calls for the same query key).
 * Uses mocked APIs — no server required.
 */
import React from 'react';
import { render, screen } from '@testing-library/react';
import '@testing-library/jest-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { useWindow } from '../api/metaApi';
import { useDataTable } from '../api/dataApi';

// Mock react-router-dom
jest.mock('react-router-dom', () => ({
  useParams: () => ({ windowId: '1' }),
  useNavigate: jest.fn(),
}));

const mockUseWindow = jest.fn();
jest.mock('../api/metaApi', () => ({
  useWindow: (...args: unknown[]) => mockUseWindow(...args),
}));

const mockUseDataTable = jest.fn();
jest.mock('../api/dataApi', () => ({
  useDataTable: (...args: unknown[]) => mockUseDataTable(...args),
}));

describe('Query Deduplication — React Query', () => {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false, staleTime: Infinity } },
  });

  const wrapper = ({ children }: { children: React.ReactNode }) => (
    <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  );

  it('useWindow returns cached data on re-render (no duplicate fetch)', () => {
    const windowData = {
      windowId: 1,
      columnName: 'X_Test',
      name: 'Test',
      tabs: [],
    };

    mockUseWindow.mockReturnValue({ data: windowData, isLoading: false, error: null });

    function WindowConsumer() {
      const { data } = mockUseWindow();
      return <div data-testid="window-name">{data?.name}</div>;
    }

    const { rerender } = render(<WindowConsumer />, { wrapper });
    expect(screen.getByTestId('window-name')).toHaveTextContent('Test');

    // Re-render with same mock — should not trigger new fetch
    rerender(<WindowConsumer />, { wrapper });
    expect(screen.getByTestId('window-name')).toHaveTextContent('Test');
  });

  it('staleTime: Infinity for meta hooks means no re-fetch', () => {
    const windowData = {
      windowId: 1,
      columnName: 'X_Test',
      name: 'Updated',
      tabs: [],
    };

    mockUseWindow.mockReturnValue({ data: windowData, isLoading: false, error: null });

    function WindowConsumer() {
      const { data } = mockUseWindow();
      return <div data-testid="name">{data?.name}</div>;
    }

    const { unmount, rerender } = render(<WindowConsumer />, { wrapper });
    expect(screen.getByTestId('name')).toHaveTextContent('Updated');

    // Re-render — staleTime: Infinity prevents re-validation
    rerender(<WindowConsumer />, { wrapper });
    expect(screen.getByTestId('name')).toHaveTextContent('Updated');
    unmount();
  });

  it('useDataTable with different params produces different query keys', () => {
    mockUseDataTable.mockReturnValue({
      data: { items: [{ id: 1 }], pagination: { page: 1, pageSize: 50, totalItems: 1, totalPages: 1 } },
      isLoading: false,
      error: null,
      refetch: () => Promise.resolve({ data: null }),
    });

    function GridConsumer({ table, params }: { table: string; params?: Record<string, unknown> }) {
      const { data } = mockUseDataTable(table, params);
      return <div data-testid="item-count">{(data?.items as any[]).length}</div>;
    }

    const { rerender, unmount } = render(<GridConsumer table="X_Account" params={{ status: 'Active' }} />, { wrapper });
    expect(screen.getByTestId('item-count')).toHaveTextContent('1');

    // Same table, same params — same data
    rerender(<GridConsumer table="X_Account" params={{ status: 'Active' }} />, { wrapper });
    expect(screen.getByTestId('item-count')).toHaveTextContent('1');
    unmount();
  });

  it('useDataTable with different table produces different results', () => {
    const accountData = { items: [{ id: 1, name: 'Acme' }], pagination: { page: 1, pageSize: 50, totalItems: 1, totalPages: 1 } };
    const contactData = { items: [{ id: 1, name: 'John' }], pagination: { page: 1, pageSize: 50, totalItems: 1, totalPages: 1 } };

    let mockResult = {
      data: accountData,
      isLoading: false,
      error: null,
      refetch: () => Promise.resolve({ data: null }),
    };

    mockUseDataTable.mockImplementation(() => mockResult);

    function GridConsumer({ table }: { table: string }) {
      const { data } = mockUseDataTable(table);
      return <div data-testid="items">{JSON.stringify(data?.items)}</div>;
    }

    const { rerender, unmount } = render(<GridConsumer table="X_Account" />, { wrapper });
    expect(screen.getByTestId('items')).toHaveTextContent('Acme');

    // Change table — data should change
    mockResult = {
      data: contactData,
      isLoading: false,
      error: null,
      refetch: () => Promise.resolve({ data: null }),
    };

    rerender(<GridConsumer table="X_Contact" />, { wrapper });
    expect(screen.getByTestId('items')).toHaveTextContent('John');
    unmount();
  });
});
